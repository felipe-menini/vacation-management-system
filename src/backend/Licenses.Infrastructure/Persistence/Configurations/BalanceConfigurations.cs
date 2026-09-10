using Licenses.Domain.LeaveManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Licenses.Infrastructure.Persistence.Configurations;

public sealed class BalanceAccountConfiguration : IEntityTypeConfiguration<BalanceAccount>
{
    public void Configure(EntityTypeBuilder<BalanceAccount> builder)
    {
        builder.ToTable("balance_accounts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.BalanceBucketId).HasColumnName("balance_bucket_id").IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.HasIndex(x => new { x.UserId, x.BalanceBucketId }).IsUnique();
        builder.HasOne<Licenses.Domain.Identity.User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BalanceBucket>().WithMany().HasForeignKey(x => x.BalanceBucketId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class BalanceLedgerEntryConfiguration : IEntityTypeConfiguration<BalanceLedgerEntry>
{
    public void Configure(EntityTypeBuilder<BalanceLedgerEntry> builder)
    {
        builder.ToTable("balance_ledger_entries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.BalanceAccountId).HasColumnName("balance_account_id").IsRequired();
        builder.Property(x => x.OperationId).HasColumnName("operation_id").IsRequired();
        builder.Property(x => x.Type).HasColumnName("type").HasMaxLength(16).HasConversion(x => ToDatabaseType(x), x => FromDatabaseType(x)).IsRequired();
        builder.Property(x => x.AvailableDelta).HasColumnName("available_delta").HasPrecision(10, 2).IsRequired();
        builder.Property(x => x.ReservedDelta).HasColumnName("reserved_delta").HasPrecision(10, 2).IsRequired();
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(BalanceLedgerEntry.ReasonMaxLength).IsRequired();
        builder.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.HasIndex(x => x.OperationId).IsUnique();
        builder.HasIndex(x => new { x.BalanceAccountId, x.CreatedAtUtc });
        builder.HasOne<BalanceAccount>().WithMany().HasForeignKey(x => x.BalanceAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Licenses.Domain.Identity.User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_balance_ledger_entries_type", "type IN ('GRANT','RESERVE','RELEASE','CONSUME','REFUND','ADJUSTMENT','EXPIRE')");
            t.HasCheckConstraint("ck_balance_ledger_entries_non_zero", "available_delta <> 0 OR reserved_delta <> 0");
            t.HasCheckConstraint("ck_balance_ledger_entries_type_semantics", "(type = 'GRANT' AND available_delta > 0 AND reserved_delta = 0) OR (type = 'RESERVE' AND available_delta < 0 AND reserved_delta > 0 AND available_delta = -reserved_delta) OR (type = 'RELEASE' AND available_delta > 0 AND reserved_delta < 0 AND available_delta = -reserved_delta) OR (type = 'CONSUME' AND available_delta = 0 AND reserved_delta < 0) OR (type = 'REFUND' AND available_delta > 0 AND reserved_delta = 0) OR (type = 'ADJUSTMENT' AND available_delta <> 0 AND reserved_delta = 0) OR (type = 'EXPIRE' AND available_delta < 0 AND reserved_delta = 0)");
        });
    }

    public static string ToDatabaseType(BalanceLedgerEntryType type) => type switch
    {
        BalanceLedgerEntryType.Grant => "GRANT",
        BalanceLedgerEntryType.Reserve => "RESERVE",
        BalanceLedgerEntryType.Release => "RELEASE",
        BalanceLedgerEntryType.Consume => "CONSUME",
        BalanceLedgerEntryType.Refund => "REFUND",
        BalanceLedgerEntryType.Adjustment => "ADJUSTMENT",
        BalanceLedgerEntryType.Expire => "EXPIRE",
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    public static BalanceLedgerEntryType FromDatabaseType(string value) => value switch
    {
        "GRANT" => BalanceLedgerEntryType.Grant,
        "RESERVE" => BalanceLedgerEntryType.Reserve,
        "RELEASE" => BalanceLedgerEntryType.Release,
        "CONSUME" => BalanceLedgerEntryType.Consume,
        "REFUND" => BalanceLedgerEntryType.Refund,
        "ADJUSTMENT" => BalanceLedgerEntryType.Adjustment,
        "EXPIRE" => BalanceLedgerEntryType.Expire,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Ledger entry type is invalid.")
    };
}
