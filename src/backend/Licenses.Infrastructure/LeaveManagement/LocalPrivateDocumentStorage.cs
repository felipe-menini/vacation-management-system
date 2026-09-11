using System.Security.Cryptography;
using Licenses.Application.LeaveManagement;
using Microsoft.Extensions.Options;

namespace Licenses.Infrastructure.LeaveManagement;

public sealed class PrivateDocumentStorageOptions
{
    public string RootPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "private-documents");
    public long MaxUploadSizeBytes { get; set; } = LeaveRequestDocumentService.DefaultMaxUploadSizeBytes;
}

public sealed class LocalPrivateDocumentStorage(IOptions<PrivateDocumentStorageOptions> options) : IPrivateDocumentStorage
{
    private readonly string _rootPath = Path.GetFullPath(options.Value.RootPath);

    public async Task<UploadedPrivateDocument> StoreAsync(string storageKey, Stream content, CancellationToken cancellationToken)
    {
        var target = ResolveStoragePath(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        await using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var sha = SHA256.Create();
        var buffer = new byte[81920];
        int read;
        long total = 0;
        while ((read = await content.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            sha.TransformBlock(buffer, 0, read, null, 0);
            total += read;
        }
        sha.TransformFinalBlock([], 0, 0);
        return new UploadedPrivateDocument(storageKey, Convert.ToHexString(sha.Hash!).ToLowerInvariant(), total);
    }

    public Task<PrivateDocumentReadStream> OpenReadAsync(string storageKey, string contentType, CancellationToken cancellationToken)
    {
        var path = ResolveStoragePath(storageKey);
        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(new PrivateDocumentReadStream(stream, contentType, stream.Length));
    }

    private string ResolveStoragePath(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey)) throw new ArgumentException("Storage key is required.", nameof(storageKey));
        var normalized = storageKey.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, normalized));
        var rootWithSeparator = _rootPath.EndsWith(Path.DirectorySeparatorChar) ? _rootPath : _rootPath + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Storage key resolves outside the private document root.");
        return fullPath;
    }
}
