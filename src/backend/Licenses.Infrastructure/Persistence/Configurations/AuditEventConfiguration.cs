using Licenses.Domain.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Licenses.Infrastructure.Persistence.Configurations;

public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("audit_events");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ActorUserId).HasColumnName("actor_user_id");
        builder.Property(x => x.Action).HasColumnName("action").HasMaxLength(AuditEvent.ActionMaxLength).IsRequired();
        builder.Property(x => x.ResourceType).HasColumnName("resource_type").HasMaxLength(AuditEvent.ResourceTypeMaxLength).IsRequired();
        builder.Property(x => x.ResourceId).HasColumnName("resource_id");
        builder.Property(x => x.SubjectUserId).HasColumnName("subject_user_id");
        builder.Property(x => x.OrgUnitId).HasColumnName("org_unit_id");
        builder.Property(x => x.CorrelationId).HasColumnName("correlation_id");
        builder.Property(x => x.OccurredAtUtc).HasColumnName("occurred_at_utc").IsRequired();
        builder.Property(x => x.MetadataJson).HasColumnName("metadata_json").HasColumnType("jsonb");

        builder.HasIndex(x => x.OccurredAtUtc);
        builder.HasIndex(x => new { x.ActorUserId, x.OccurredAtUtc });
        builder.HasIndex(x => new { x.ResourceType, x.ResourceId, x.OccurredAtUtc });
        builder.HasIndex(x => new { x.SubjectUserId, x.OccurredAtUtc });
        builder.HasIndex(x => new { x.OrgUnitId, x.OccurredAtUtc });

        builder.HasOne<Licenses.Domain.Identity.User>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Licenses.Domain.Identity.User>().WithMany().HasForeignKey(x => x.SubjectUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Licenses.Domain.Organization.OrgUnit>().WithMany().HasForeignKey(x => x.OrgUnitId).OnDelete(DeleteBehavior.Restrict);
    }
}