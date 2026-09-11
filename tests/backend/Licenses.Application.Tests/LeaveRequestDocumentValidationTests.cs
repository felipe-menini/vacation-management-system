using System.Text;
using Licenses.Application.LeaveManagement;

namespace Licenses.Application.Tests;

public sealed class LeaveRequestDocumentValidationTests
{
    [Fact]
    public async Task AcceptsSupportedSignaturesAndRecordsContentForHashing()
    {
        await using var stream = new MemoryStream("%PDF-1.7 test"u8.ToArray());
        var result = await LeaveRequestDocumentService.ValidateFileAsync("certificate.pdf", "application/pdf", stream, 1024, CancellationToken.None);
        Assert.Equal("application/pdf", result.ContentType);
        Assert.Equal(".pdf", result.Extension);
        Assert.NotEmpty(result.ContentBytes);
    }

    [Theory]
    [InlineData("certificate.pdf", "image/png", "%PDF-1.7")]
    [InlineData("certificate.txt", "text/plain", "hello")]
    [InlineData("certificate.png", "image/png", "not-a-png")]
    public async Task RejectsUnsupportedOrMismatchedFiles(string fileName, string contentType, string payload)
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        await Assert.ThrowsAsync<ArgumentException>(() => LeaveRequestDocumentService.ValidateFileAsync(fileName, contentType, stream, 1024, CancellationToken.None));
    }

    [Fact]
    public async Task RejectsEmptyAndOversizedFiles()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => LeaveRequestDocumentService.ValidateFileAsync("empty.pdf", "application/pdf", new MemoryStream(), 1024, CancellationToken.None));
        await using var oversized = new MemoryStream("%PDF-1.7 oversized"u8.ToArray());
        await Assert.ThrowsAsync<ArgumentException>(() => LeaveRequestDocumentService.ValidateFileAsync("big.pdf", "application/pdf", oversized, 4, CancellationToken.None));
    }
}
