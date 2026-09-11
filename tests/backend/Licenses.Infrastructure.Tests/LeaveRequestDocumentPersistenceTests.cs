using Licenses.Domain.Identity;
using Licenses.Domain.LeaveManagement;
using Licenses.Domain.Organization;
using Licenses.Infrastructure.LeaveManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Licenses.Infrastructure.Tests;

public sealed class LeaveRequestDocumentPersistenceTests
{
    [Fact]
    public async Task PersistsMetadataOnlyAndEnforcesConservativeConstraints()
    {
        await PostgreSqlTestDatabase.WithFreshDatabaseAsync(async db =>
        {
            var now = DateTime.UtcNow;
            var user = User.Create("Document User", $"document.{Guid.NewGuid():N}@example.test", null, now);
            var unit = OrgUnit.Create("Engineering", "DOC" + Guid.NewGuid().ToString("N")[..8], null, now);
            var type = LeaveType.Create("DOC" + Guid.NewGuid().ToString("N")[..8], "Medical", null, 1, true, now);
            await db.Users.AddAsync(user);
            await db.OrgUnits.AddAsync(unit);
            await db.LeaveTypes.AddAsync(type);
            await db.SaveChangesAsync();
            var request = LeaveRequest.CreateDraft(user.Id, unit.Id, type.Id, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1), LeaveRequestDayPortion.FullDay, null, user.Id, now);
            await db.LeaveRequests.AddAsync(request);
            var document = LeaveRequestDocument.Create(request.Id, LeaveRequestDocumentKind.MedicalCertificate, "cert.pdf", "application/pdf", 12, "leave-request-documents/test.pdf", new string('b', 64), user.Id, now);
            await db.LeaveRequestDocuments.AddAsync(document);
            await db.SaveChangesAsync();

            var columns = await db.Database.SqlQueryRaw<string>("SELECT column_name FROM information_schema.columns WHERE table_schema = 'licenses' AND table_name = 'leave_request_documents'").ToListAsync();
            Assert.DoesNotContain(columns, c => c.Contains("blob", StringComparison.OrdinalIgnoreCase) || c.Contains("bytes", StringComparison.OrdinalIgnoreCase) && c != "size_bytes");
            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO licenses.leave_request_documents (id, leave_request_id, kind, original_file_name, content_type, size_bytes, storage_key, sha256, uploaded_by_user_id, created_at_utc) VALUES ({Guid.NewGuid()}, {request.Id}, {"BAD"}, {"bad.pdf"}, {"application/pdf"}, {1}, {"leave-request-documents/bad.pdf"}, {new string('c', 64)}, {user.Id}, {now})"));
            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO licenses.leave_request_documents (id, leave_request_id, kind, original_file_name, content_type, size_bytes, storage_key, sha256, uploaded_by_user_id, created_at_utc) VALUES ({Guid.NewGuid()}, {request.Id}, {"MEDICAL_CERTIFICATE"}, {"dup.pdf"}, {"application/pdf"}, {1}, {"leave-request-documents/test.pdf"}, {new string('d', 64)}, {user.Id}, {now})"));
            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM licenses.leave_requests WHERE id = {request.Id}"));
        });
    }
}

public sealed class LocalPrivateDocumentStorageTests
{
    [Fact]
    public async Task StoresAndReadsPrivateContentWithoutUsingOriginalFilename()
    {
        var root = Path.Combine(Path.GetTempPath(), "licenses-doc-tests", Guid.NewGuid().ToString("N"));
        var storage = new LocalPrivateDocumentStorage(Options.Create(new PrivateDocumentStorageOptions { RootPath = root }));
        var key = LeaveRequestDocument.CreateOpaqueStorageKey("pdf");
        await using var input = new MemoryStream("%PDF-1.7 private"u8.ToArray());
        var stored = await storage.StoreAsync(key, input, CancellationToken.None);
        await using var opened = await storage.OpenReadAsync(stored.StorageKey, "application/pdf", CancellationToken.None);
        using var reader = new StreamReader(opened.Content);
        Assert.Equal("%PDF-1.7 private", await reader.ReadToEndAsync());
        Assert.DoesNotContain("certificate", stored.StorageKey, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RejectsPathTraversalStorageKeys()
    {
        var root = Path.Combine(Path.GetTempPath(), "licenses-doc-tests", Guid.NewGuid().ToString("N"));
        var storage = new LocalPrivateDocumentStorage(Options.Create(new PrivateDocumentStorageOptions { RootPath = root }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => storage.StoreAsync("../escape.pdf", new MemoryStream("x"u8.ToArray()), CancellationToken.None));
    }
}