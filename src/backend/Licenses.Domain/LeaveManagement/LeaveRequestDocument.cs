using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Licenses.Domain.LeaveManagement;

public enum LeaveRequestDocumentKind { MedicalCertificate = 1 }

public sealed class LeaveRequestDocument
{
    public const int OriginalFileNameMaxLength = 255;
    public const int ContentTypeMaxLength = 100;
    public const int StorageKeyMaxLength = 256;
    public const int Sha256HexLength = 64;

    private LeaveRequestDocument() { }

    private LeaveRequestDocument(Guid id, Guid leaveRequestId, LeaveRequestDocumentKind kind, string originalFileName, string contentType, long sizeBytes, string storageKey, string sha256, Guid uploadedByUserId, DateTime createdAtUtc)
    {
        Id = id == Guid.Empty ? throw new ArgumentException("Id is required.", nameof(id)) : id;
        LeaveRequestId = leaveRequestId == Guid.Empty ? throw new ArgumentException("LeaveRequestId is required.", nameof(leaveRequestId)) : leaveRequestId;
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind), "Document kind is invalid.");
        Kind = kind;
        OriginalFileName = SanitizeOriginalFileName(originalFileName);
        ContentType = RequireText(contentType, ContentTypeMaxLength, nameof(contentType));
        if (sizeBytes <= 0) throw new ArgumentOutOfRangeException(nameof(sizeBytes), "SizeBytes must be positive.");
        SizeBytes = sizeBytes;
        StorageKey = RequireText(storageKey, StorageKeyMaxLength, nameof(storageKey));
        Sha256 = NormalizeSha256(sha256);
        UploadedByUserId = uploadedByUserId == Guid.Empty ? throw new ArgumentException("UploadedByUserId is required.", nameof(uploadedByUserId)) : uploadedByUserId;
        CreatedAtUtc = EnsureUtc(createdAtUtc, nameof(createdAtUtc));
    }

    public Guid Id { get; private set; }
    public Guid LeaveRequestId { get; private set; }
    public LeaveRequestDocumentKind Kind { get; private set; }
    public string OriginalFileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public string StorageKey { get; private set; } = string.Empty;
    public string Sha256 { get; private set; } = string.Empty;
    public Guid UploadedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public static LeaveRequestDocument Create(Guid leaveRequestId, LeaveRequestDocumentKind kind, string originalFileName, string contentType, long sizeBytes, string storageKey, string sha256, Guid uploadedByUserId, DateTime createdAtUtc) =>
        new(Guid.NewGuid(), leaveRequestId, kind, originalFileName, contentType, sizeBytes, storageKey, sha256, uploadedByUserId, createdAtUtc);

    public static string CreateOpaqueStorageKey(string extension)
    {
        var normalizedExtension = extension.Trim().TrimStart('.').ToLowerInvariant();
        if (normalizedExtension is not ("pdf" or "jpg" or "jpeg" or "png")) throw new ArgumentException("Unsupported storage extension.", nameof(extension));
        return $"leave-request-documents/{Guid.NewGuid():N}.{normalizedExtension}";
    }

    public static string SanitizeOriginalFileName(string value)
    {
        var fileName = Path.GetFileName(value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(fileName)) fileName = "document";
        fileName = Regex.Replace(fileName, @"[\p{Cc}<>:""/\\|?*]+", "_");
        if (fileName.Length > OriginalFileNameMaxLength) fileName = fileName[..OriginalFileNameMaxLength];
        return fileName;
    }

    private static string NormalizeSha256(string value)
    {
        var normalized = RequireText(value, Sha256HexLength, nameof(value)).ToLowerInvariant();
        if (normalized.Length != Sha256HexLength || normalized.Any(c => !Uri.IsHexDigit(c))) throw new ArgumentException("SHA-256 must be a 64-character hexadecimal value.", nameof(value));
        return normalized;
    }

    private static string RequireText(string value, int maxLength, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Value is required.", name);
        var normalized = value.Trim();
        if (normalized.Length > maxLength) throw new ArgumentException($"Value cannot exceed {maxLength} characters.", name);
        return normalized;
    }

    private static DateTime EnsureUtc(DateTime value, string name)
    {
        if (value.Kind != DateTimeKind.Utc) throw new ArgumentException("Timestamp must be UTC.", name);
        return value;
    }
}
