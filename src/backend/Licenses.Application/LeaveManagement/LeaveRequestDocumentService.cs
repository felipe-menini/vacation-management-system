using System.Security.Cryptography;
using Licenses.Application.Authorization;
using Licenses.Domain.LeaveManagement;

namespace Licenses.Application.LeaveManagement;

public sealed class LeaveRequestDocumentService(ILeaveRequestRepository repository, IPrivateDocumentStorage storage, AuthorizationService authorization, ICurrentActor currentActor, TimeProvider timeProvider)
{
    public const long DefaultMaxUploadSizeBytes = 10 * 1024 * 1024;
    private static readonly Dictionary<string, string> ExtensionToContentType = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png"
    };

    public async Task<IReadOnlyList<LeaveRequestDocumentDto>?> ListAsync(Guid leaveRequestId, CancellationToken cancellationToken)
    {
        var actorId = RequireActor();
        var request = await repository.GetAsync(leaveRequestId, tracking: false, cancellationToken);
        if (request is null || !await CanReadDocumentsAsync(actorId, request, cancellationToken)) return null;
        return await ToDtosAsync(await repository.ListDocumentsByRequestIdAsync(leaveRequestId, cancellationToken), cancellationToken);
    }

    public async Task<LeaveRequestDocumentDto?> UploadAsync(Guid leaveRequestId, string originalFileName, string? declaredContentType, Stream content, long? declaredLength, long maxUploadSizeBytes, CancellationToken cancellationToken)
    {
        var actorId = RequireActor();
        var request = await repository.GetAsync(leaveRequestId, tracking: false, cancellationToken);
        if (request is null || request.UserId != actorId) return null;
        if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeaveDocumentsUploadSelf, cancellationToken)) throw new UnauthorizedAccessException("Actor cannot upload own leave request documents.");
        if (declaredLength is <= 0) throw new ArgumentException("File is empty.", nameof(declaredLength));
        if (declaredLength > maxUploadSizeBytes) throw new ArgumentException("File exceeds the configured maximum upload size.", nameof(declaredLength));

        var validation = await ValidateFileAsync(originalFileName, declaredContentType, content, maxUploadSizeBytes, cancellationToken);
        var storageKey = LeaveRequestDocument.CreateOpaqueStorageKey(validation.Extension.TrimStart('.'));
        await using var buffer = new MemoryStream(validation.ContentBytes, writable: false);
        var stored = await storage.StoreAsync(storageKey, buffer, cancellationToken);
        var document = LeaveRequestDocument.Create(request.Id, LeaveRequestDocumentKind.MedicalCertificate, originalFileName, validation.ContentType, stored.SizeBytes, stored.StorageKey, stored.Sha256, actorId, UtcNow());
        await repository.AddDocumentAsync(document, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return (await ToDtosAsync([document], cancellationToken)).Single();
    }

    public async Task<(LeaveRequestDocumentDto Metadata, PrivateDocumentReadStream Content)?> OpenContentAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var actorId = RequireActor();
        var document = await repository.GetDocumentAsync(documentId, tracking: false, cancellationToken);
        if (document is null) return null;
        var request = await repository.GetAsync(document.LeaveRequestId, tracking: false, cancellationToken);
        if (request is null || !await CanReadDocumentsAsync(actorId, request, cancellationToken)) return null;
        var dto = (await ToDtosAsync([document], cancellationToken)).Single();
        return (dto, await storage.OpenReadAsync(document.StorageKey, document.ContentType, cancellationToken));
    }

    private async Task<bool> CanReadDocumentsAsync(Guid actorId, LeaveRequest request, CancellationToken cancellationToken)
    {
        if (request.UserId == actorId) return await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeaveDocumentsReadSelf, cancellationToken);
        return await authorization.CanUserPerformAsync(actorId, PermissionCodes.LeaveDocumentsRead, request.OrgUnitId, cancellationToken);
    }

    private async Task<List<LeaveRequestDocumentDto>> ToDtosAsync(IEnumerable<LeaveRequestDocument> documents, CancellationToken cancellationToken)
    {
        var result = new List<LeaveRequestDocumentDto>();
        foreach (var document in documents)
        {
            var uploader = await repository.GetUserAsync(document.UploadedByUserId, cancellationToken);
            result.Add(new(document.Id, document.LeaveRequestId, ToKind(document.Kind), document.OriginalFileName, document.ContentType, document.SizeBytes, document.Sha256, document.UploadedByUserId, uploader?.DisplayName, document.CreatedAtUtc));
        }
        return result;
    }

    public static async Task<ValidatedFile> ValidateFileAsync(string originalFileName, string? declaredContentType, Stream content, long maxUploadSizeBytes, CancellationToken cancellationToken)
    {
        if (maxUploadSizeBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxUploadSizeBytes));
        var extension = Path.GetExtension(originalFileName ?? string.Empty).ToLowerInvariant();
        if (!ExtensionToContentType.TryGetValue(extension, out var expectedContentType)) throw new ArgumentException("Unsupported file extension.", nameof(originalFileName));
        if (!string.IsNullOrWhiteSpace(declaredContentType) && !IsCompatibleDeclaredContentType(expectedContentType, declaredContentType)) throw new ArgumentException("File extension and content type do not match.", nameof(declaredContentType));

        await using var buffer = new MemoryStream();
        var copyBuffer = new byte[81920];
        int read;
        long total = 0;
        while ((read = await content.ReadAsync(copyBuffer.AsMemory(0, copyBuffer.Length), cancellationToken)) > 0)
        {
            total += read;
            if (total > maxUploadSizeBytes) throw new ArgumentException("File exceeds the configured maximum upload size.", nameof(content));
            buffer.Write(copyBuffer, 0, read);
        }
        if (total == 0) throw new ArgumentException("File is empty.", nameof(content));
        var bytes = buffer.ToArray();
        if (!HasExpectedSignature(bytes, expectedContentType)) throw new ArgumentException("File signature does not match the supported content type.", nameof(content));
        return new ValidatedFile(extension, expectedContentType, bytes);
    }

    private static bool IsCompatibleDeclaredContentType(string expected, string declared)
    {
        var normalized = declared.Split(';')[0].Trim().ToLowerInvariant();
        return expected == normalized || (expected == "image/jpeg" && normalized == "image/pjpeg");
    }

    private static bool HasExpectedSignature(byte[] bytes, string contentType) => contentType switch
    {
        "application/pdf" => bytes.Length >= 5 && bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46 && bytes[4] == 0x2D,
        "image/jpeg" => bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
        "image/png" => bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A,
        _ => false
    };

    private Guid RequireActor() => currentActor.UserId ?? throw new UnauthorizedAccessException("Actor is required.");
    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;
    public static string ToKind(LeaveRequestDocumentKind value) => value switch { LeaveRequestDocumentKind.MedicalCertificate => "MEDICAL_CERTIFICATE", _ => throw new ArgumentOutOfRangeException(nameof(value)) };
    public static LeaveRequestDocumentKind FromKind(string value) => value switch { "MEDICAL_CERTIFICATE" => LeaveRequestDocumentKind.MedicalCertificate, _ => throw new ArgumentOutOfRangeException(nameof(value)) };
}

public sealed record ValidatedFile(string Extension, string ContentType, byte[] ContentBytes);
