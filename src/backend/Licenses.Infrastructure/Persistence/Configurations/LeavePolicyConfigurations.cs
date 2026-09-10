using Licenses.Domain.LeaveManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Licenses.Infrastructure.Persistence.Configurations;

public sealed class LeavePolicyConfiguration : IEntityTypeConfiguration<LeavePolicy>
{
    public void Configure(EntityTypeBuilder<LeavePolicy> builder)
    {
        builder.ToTable("leave_policies");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.LeaveTypeId).HasColumnName("leave_type_id").IsRequired();
        builder.Property(x => x.OrgUnitId).HasColumnName("org_unit_id");
        builder.Property(x => x.AppliesToDescendants).HasColumnName("applies_to_descendants").IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
        builder.HasMany(x => x.Versions).WithOne().HasForeignKey(x => x.LeavePolicyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.LeaveTypeId, x.OrgUnitId }).IsUnique();
        builder.HasIndex(x => x.LeaveTypeId);
        builder.HasIndex(x => x.OrgUnitId);
        builder.HasOne<LeaveType>().WithMany().HasForeignKey(x => x.LeaveTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Licenses.Domain.Organization.OrgUnit>().WithMany().HasForeignKey(x => x.OrgUnitId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t => t.HasCheckConstraint("ck_leave_policies_global_descendants_false", "org_unit_id IS NOT NULL OR applies_to_descendants = false"));
    }
}

public sealed class LeavePolicyVersionConfiguration : IEntityTypeConfiguration<LeavePolicyVersion>
{
    public void Configure(EntityTypeBuilder<LeavePolicyVersion> builder)
    {
        builder.ToTable("leave_policy_versions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.LeavePolicyId).HasColumnName("leave_policy_id").IsRequired();
        builder.Property(x => x.VersionNumber).HasColumnName("version_number").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(16).HasConversion(x => ToStatus(x), x => FromStatus(x)).IsRequired();
        builder.Property(x => x.EffectiveFrom).HasColumnName("effective_from").HasColumnType("date").IsRequired();
        builder.Property(x => x.EffectiveTo).HasColumnName("effective_to").HasColumnType("date");
        builder.Property(x => x.DayCountMode).HasColumnName("day_count_mode").HasMaxLength(32).HasConversion(x => ToDayCount(x), x => FromDayCount(x)).IsRequired();
        builder.Property(x => x.AllowHalfDay).HasColumnName("allow_half_day").IsRequired();
        builder.Property(x => x.MinimumNoticeDays).HasColumnName("minimum_notice_days");
        builder.Property(x => x.NoticeDayCountMode).HasColumnName("notice_day_count_mode").HasMaxLength(32).HasConversion(x => ToDayCount(x), x => FromDayCount(x)).IsRequired();
        builder.Property(x => x.MaximumRequestDays).HasColumnName("maximum_request_days").HasPrecision(8, 2);
        builder.Property(x => x.OverlapBehavior).HasColumnName("overlap_behavior").HasMaxLength(16).HasConversion(x => ToOverlap(x), x => FromOverlap(x)).IsRequired();
        builder.Property(x => x.ConsumesBalance).HasColumnName("consumes_balance").IsRequired();
        builder.Property(x => x.BalanceBucketId).HasColumnName("balance_bucket_id");
        builder.Property(x => x.WorkingCalendarId).HasColumnName("working_calendar_id");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
        builder.Property(x => x.PublishedAtUtc).HasColumnName("published_at_utc");
        builder.HasIndex(x => new { x.LeavePolicyId, x.VersionNumber }).IsUnique();
        builder.HasIndex(x => new { x.LeavePolicyId, x.Status, x.EffectiveFrom, x.EffectiveTo });
        builder.HasOne<BalanceBucket>().WithMany().HasForeignKey(x => x.BalanceBucketId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkingCalendar>().WithMany().HasForeignKey(x => x.WorkingCalendarId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_leave_policy_versions_status", "status IN ('DRAFT','PUBLISHED')");
            t.HasCheckConstraint("ck_leave_policy_versions_day_count_mode", "day_count_mode IN ('BUSINESS_DAYS','CALENDAR_DAYS')");
            t.HasCheckConstraint("ck_leave_policy_versions_notice_day_count_mode", "notice_day_count_mode IN ('BUSINESS_DAYS','CALENDAR_DAYS')");
            t.HasCheckConstraint("ck_leave_policy_versions_overlap_behavior", "overlap_behavior IN ('BLOCK','WARN','ALLOW')");
            t.HasCheckConstraint("ck_leave_policy_versions_version_positive", "version_number > 0");
            t.HasCheckConstraint("ck_leave_policy_versions_effective_range", "effective_to IS NULL OR effective_to >= effective_from");
            t.HasCheckConstraint("ck_leave_policy_versions_min_notice_non_negative", "minimum_notice_days IS NULL OR minimum_notice_days >= 0");
            t.HasCheckConstraint("ck_leave_policy_versions_max_request_positive", "maximum_request_days IS NULL OR maximum_request_days > 0");
            t.HasCheckConstraint("ck_leave_policy_versions_balance_consistency", "(consumes_balance = true AND balance_bucket_id IS NOT NULL) OR (consumes_balance = false AND balance_bucket_id IS NULL)");
            t.HasCheckConstraint("ck_leave_policy_versions_calendar_required_for_business_days", "((day_count_mode = 'BUSINESS_DAYS' OR notice_day_count_mode = 'BUSINESS_DAYS') AND working_calendar_id IS NOT NULL) OR (day_count_mode = 'CALENDAR_DAYS' AND notice_day_count_mode = 'CALENDAR_DAYS')");
            t.HasCheckConstraint("ck_leave_policy_versions_published_at", "(status = 'PUBLISHED' AND published_at_utc IS NOT NULL) OR (status = 'DRAFT' AND published_at_utc IS NULL)");
        });
    }

    private static string ToStatus(LeavePolicyVersionStatus status) => status switch { LeavePolicyVersionStatus.Draft => "DRAFT", LeavePolicyVersionStatus.Published => "PUBLISHED", _ => throw new ArgumentOutOfRangeException(nameof(status)) };
    private static LeavePolicyVersionStatus FromStatus(string value) => value switch { "DRAFT" => LeavePolicyVersionStatus.Draft, "PUBLISHED" => LeavePolicyVersionStatus.Published, _ => throw new ArgumentOutOfRangeException(nameof(value)) };
    private static string ToDayCount(PolicyDayCountMode mode) => mode switch { PolicyDayCountMode.BusinessDays => "BUSINESS_DAYS", PolicyDayCountMode.CalendarDays => "CALENDAR_DAYS", _ => throw new ArgumentOutOfRangeException(nameof(mode)) };
    private static PolicyDayCountMode FromDayCount(string value) => value switch { "BUSINESS_DAYS" => PolicyDayCountMode.BusinessDays, "CALENDAR_DAYS" => PolicyDayCountMode.CalendarDays, _ => throw new ArgumentOutOfRangeException(nameof(value)) };
    private static string ToOverlap(PolicyOverlapBehavior behavior) => behavior switch { PolicyOverlapBehavior.Block => "BLOCK", PolicyOverlapBehavior.Warn => "WARN", PolicyOverlapBehavior.Allow => "ALLOW", _ => throw new ArgumentOutOfRangeException(nameof(behavior)) };
    private static PolicyOverlapBehavior FromOverlap(string value) => value switch { "BLOCK" => PolicyOverlapBehavior.Block, "WARN" => PolicyOverlapBehavior.Warn, "ALLOW" => PolicyOverlapBehavior.Allow, _ => throw new ArgumentOutOfRangeException(nameof(value)) };
}

public sealed class WorkingCalendarConfiguration : IEntityTypeConfiguration<WorkingCalendar>
{
    public void Configure(EntityTypeBuilder<WorkingCalendar> builder)
    {
        builder.ToTable("working_calendars");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(1000);
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
        builder.HasMany(x => x.Weekdays).WithOne().HasForeignKey(x => x.WorkingCalendarId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Exceptions).WithOne().HasForeignKey(x => x.WorkingCalendarId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.Code).IsUnique();
    }
}

public sealed class WorkingCalendarWeekdayConfiguration : IEntityTypeConfiguration<WorkingCalendarWeekday>
{
    public void Configure(EntityTypeBuilder<WorkingCalendarWeekday> builder)
    {
        builder.ToTable("working_calendar_weekdays");
        builder.HasKey(x => new { x.WorkingCalendarId, x.DayOfWeek });
        builder.Property(x => x.WorkingCalendarId).HasColumnName("working_calendar_id").IsRequired();
        builder.Property(x => x.DayOfWeek).HasColumnName("day_of_week").HasConversion<int>().IsRequired();
        builder.Property(x => x.IsWorkingDay).HasColumnName("is_working_day").IsRequired();
        builder.ToTable(t => t.HasCheckConstraint("ck_working_calendar_weekdays_day_of_week", "day_of_week BETWEEN 0 AND 6"));
    }
}

public sealed class WorkingCalendarExceptionConfiguration : IEntityTypeConfiguration<WorkingCalendarException>
{
    public void Configure(EntityTypeBuilder<WorkingCalendarException> builder)
    {
        builder.ToTable("working_calendar_exceptions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.WorkingCalendarId).HasColumnName("working_calendar_id").IsRequired();
        builder.Property(x => x.Date).HasColumnName("date").HasColumnType("date").IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.IsWorkingDay).HasColumnName("is_working_day").IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
        builder.HasIndex(x => new { x.WorkingCalendarId, x.Date }).IsUnique();
    }
}
