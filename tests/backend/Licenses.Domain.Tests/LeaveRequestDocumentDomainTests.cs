using Licenses.Domain.LeaveManagement;

namespace Licenses.Domain.Tests;

public sealed class LeaveRequestDocumentDomainTests
{
    [Fact]
    public void MetadataIsCreatedAsImmutableValueSet()
    {
        var requestId = Guid.NewGuid();
        var uploaderId = Guid.NewGuid();
        var document = LeaveRequestDocument.Create(requestId, LeaveRequestDocumentKind.MedicalCertificate, " ../cert.pdf ", "application/pdf", 5, "leave-request-documents/key.pdf", new string('a', 64), uploaderId, DateTime.UtcNow);

        Assert.Equal(requestId, document.LeaveRequestId);
        Assert.Equal(LeaveRequestDocumentKind.MedicalCertificate, document.Kind);
        Assert.Equal("cert.pdf", document.OriginalFileName);
        Assert.Equal(uploaderId, document.UploadedByUserId);
    }

    [Fact]
    public void RejectsInvalidMetadataAndGeneratesOpaqueStorageKeys()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LeaveRequestDocument.Create(Guid.NewGuid(), LeaveRequestDocumentKind.MedicalCertificate, "cert.pdf", "application/pdf", 0, "k", new string('a', 64), Guid.NewGuid(), DateTime.UtcNow));
        Assert.Throws<ArgumentException>(() => LeaveRequestDocument.Create(Guid.NewGuid(), LeaveRequestDocumentKind.MedicalCertificate, "cert.pdf", "application/pdf", 1, "k", "bad", Guid.NewGuid(), DateTime.UtcNow));
        var key = LeaveRequestDocument.CreateOpaqueStorageKey("pdf");
        Assert.StartsWith("leave-request-documents/", key);
        Assert.EndsWith(".pdf", key);
        Assert.DoesNotContain("cert", key);
    }
}
