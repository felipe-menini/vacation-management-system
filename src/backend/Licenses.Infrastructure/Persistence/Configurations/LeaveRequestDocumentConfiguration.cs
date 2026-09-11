using Licenses.Application.LeaveManagement;
using Licenses.Domain.LeaveManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Licenses.Infrastructure.Persistence.Configurations;

public sealed class LeaveRequestDocumentConfiguration : IEntityTypeConfiguration<LeaveRequestDocument>
{
    public void Configure(EntityTypeBuilder<LeaveRequestDocument> builder)
    {
        builder.ToTable("leave_request_documents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.LeaveRequestId).HasColumnName("leave_request_id").IsRequired();
        builder.Property(x => x.Kind).HasColumnName("kind").HasMaxLength(64).HasConversion(x => LeaveRequestDocumentService.ToKind(x), x => LeaveRequestDocumentService.FromKind(x)).IsRequired();
        builder.Property(x => x.OriginalFileName).HasColumnName("original_file_name").HasMaxLength(LeaveRequestDocument.OriginalFileNameMaxLength).IsRequired();
        builder.Property(x => x.ContentType).HasColumnName("content_type").HasMaxLength(LeaveRequestDocument.ContentTypeMaxLength).IsRequired();
        builder.Property(x => x.SizeBytes).HasColumnName("size_bytes").IsRequired();
        builder.Property(x => x.StorageKey).HasColumnName("storage_key").HasMaxLength(LeaveRequestDocument.StorageKeyMaxLength).IsRequired();
        builder.Property(x => x.Sha256).HasColumnName("sha256").HasMaxLength(LeaveRequestDocument.Sha256HexLength).IsRequired();
        builder.Property(x => x.UploadedByUserId).HasColumnName("uploaded_by_user_id").IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.HasOne<LeaveRequest>().WithMany().HasForeignKey(x => x.LeaveRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Licenses.Domain.Identity.User>().WithMany().HasForeignKey(x => x.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.LeaveRequestId, x.CreatedAtUtc });
        builder.HasIndex(x => x.StorageKey).IsUnique();
        builder.HasIndex(x => x.Sha256);
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_leave_request_documents_kind", "kind IN ('MEDICAL_CERTIFICATE')");
            t.HasCheckConstraint("ck_leave_request_documents_size_positive", "size_bytes > 0");
            t.HasCheckConstraint("ck_leave_request_documents_sha256_hex", "sha256 ~ '^[0-9a-f]{64}$'");
        });
    }
}
