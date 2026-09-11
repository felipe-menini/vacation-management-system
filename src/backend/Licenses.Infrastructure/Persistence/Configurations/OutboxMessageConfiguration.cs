using Licenses.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Licenses.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(OutboxMessage.EventTypeMaxLength).IsRequired();
        builder.Property(x => x.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.CorrelationId).HasColumnName("correlation_id").IsRequired();
        builder.Property(x => x.OccurredAtUtc).HasColumnName("occurred_at_utc").IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(x => x.ProcessedAtUtc).HasColumnName("processed_at_utc");
        builder.Property(x => x.Attempts).HasColumnName("attempts").IsRequired();
        builder.Property(x => x.LastError).HasColumnName("last_error").HasMaxLength(OutboxMessage.LastErrorMaxLength);
        builder.Property(x => x.NextAttemptAtUtc).HasColumnName("next_attempt_at_utc");
        builder.Property(x => x.ProcessingLeaseExpiresAtUtc).HasColumnName("processing_lease_expires_at_utc");
        builder.Property(x => x.DeadLetteredAtUtc).HasColumnName("dead_lettered_at_utc");
        builder.HasIndex(x => new { x.ProcessedAtUtc, x.CreatedAtUtc });
        builder.HasIndex(x => new { x.ProcessedAtUtc, x.DeadLetteredAtUtc, x.NextAttemptAtUtc })
            .HasDatabaseName("ix_outbox_messages_processing_eligibility");
        builder.HasIndex(x => x.OccurredAtUtc);
        builder.HasIndex(x => new { x.EventType, x.CorrelationId }).IsUnique();
        builder.ToTable(t => t.HasCheckConstraint("ck_outbox_messages_attempts_non_negative", "attempts >= 0"));
    }
}
