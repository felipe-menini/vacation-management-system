using Licenses.Domain.LeaveManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Licenses.Infrastructure.Persistence.Configurations;

public sealed class LeaveTypeConfiguration : IEntityTypeConfiguration<LeaveType>
{
    public void Configure(EntityTypeBuilder<LeaveType> builder)
    {
        builder.ToTable("leave_types");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(LeaveType.CodeMaxLength).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(LeaveType.NameMaxLength).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(LeaveType.DescriptionMaxLength);
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => new { x.IsActive, x.SortOrder });
        builder.ToTable(t => t.HasCheckConstraint("ck_leave_types_sort_order_non_negative", "sort_order >= 0"));
    }
}

public sealed class BalanceBucketConfiguration : IEntityTypeConfiguration<BalanceBucket>
{
    public void Configure(EntityTypeBuilder<BalanceBucket> builder)
    {
        builder.ToTable("balance_buckets");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(BalanceBucket.CodeMaxLength).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(BalanceBucket.NameMaxLength).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(BalanceBucket.DescriptionMaxLength);
        builder.Property(x => x.Unit)
            .HasColumnName("unit")
            .HasMaxLength(16)
            .HasConversion(unit => ToDatabaseUnit(unit), value => FromDatabaseUnit(value))
            .IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => x.IsActive);
        builder.ToTable(t => t.HasCheckConstraint("ck_balance_buckets_unit", "unit IN ('DAY')"));
    }
    private static string ToDatabaseUnit(BalanceBucketUnit unit) => unit switch
    {
        BalanceBucketUnit.Day => "DAY",
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Balance bucket unit is invalid.")
    };

    private static BalanceBucketUnit FromDatabaseUnit(string value) => value switch
    {
        "DAY" => BalanceBucketUnit.Day,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Balance bucket unit is invalid.")
    };
}
