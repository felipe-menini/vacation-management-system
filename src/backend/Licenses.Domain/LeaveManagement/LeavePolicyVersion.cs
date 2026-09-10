namespace Licenses.Domain.LeaveManagement;

public enum LeavePolicyVersionStatus { Draft = 1, Published = 2 }
public enum PolicyDayCountMode { BusinessDays = 1, CalendarDays = 2 }
public enum PolicyOverlapBehavior { Block = 1, Warn = 2, Allow = 3 }

public sealed class LeavePolicyVersion
{
    private LeavePolicyVersion() { }

    private LeavePolicyVersion(Guid id, Guid leavePolicyId, int versionNumber, DateOnly effectiveFrom, DateOnly? effectiveTo, PolicyDayCountMode dayCountMode, bool allowHalfDay, int? minimumNoticeDays, PolicyDayCountMode noticeDayCountMode, decimal? maximumRequestDays, PolicyOverlapBehavior overlapBehavior, bool consumesBalance, Guid? balanceBucketId, Guid? workingCalendarId, DateTime createdAtUtc)
    {
        Id = id == Guid.Empty ? throw new ArgumentException("Id is required.", nameof(id)) : id;
        LeavePolicyId = leavePolicyId == Guid.Empty ? throw new ArgumentException("Leave policy is required.", nameof(leavePolicyId)) : leavePolicyId;
        VersionNumber = versionNumber > 0 ? versionNumber : throw new ArgumentOutOfRangeException(nameof(versionNumber), "Version number must be positive.");
        Status = LeavePolicyVersionStatus.Draft;
        CreatedAtUtc = EnsureUtc(createdAtUtc, nameof(createdAtUtc));
        UpdatedAtUtc = CreatedAtUtc;
        ApplyRules(effectiveFrom, effectiveTo, dayCountMode, allowHalfDay, minimumNoticeDays, noticeDayCountMode, maximumRequestDays, overlapBehavior, consumesBalance, balanceBucketId, workingCalendarId);
    }

    public Guid Id { get; private set; }
    public Guid LeavePolicyId { get; private set; }
    public int VersionNumber { get; private set; }
    public LeavePolicyVersionStatus Status { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }
    public PolicyDayCountMode DayCountMode { get; private set; }
    public bool AllowHalfDay { get; private set; }
    public int? MinimumNoticeDays { get; private set; }
    public PolicyDayCountMode NoticeDayCountMode { get; private set; }
    public decimal? MaximumRequestDays { get; private set; }
    public PolicyOverlapBehavior OverlapBehavior { get; private set; }
    public bool ConsumesBalance { get; private set; }
    public Guid? BalanceBucketId { get; private set; }
    public Guid? WorkingCalendarId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public DateTime? PublishedAtUtc { get; private set; }

    public static LeavePolicyVersion CreateDraft(Guid leavePolicyId, int versionNumber, DateOnly effectiveFrom, DateOnly? effectiveTo, PolicyDayCountMode dayCountMode, bool allowHalfDay, int? minimumNoticeDays, PolicyDayCountMode noticeDayCountMode, decimal? maximumRequestDays, PolicyOverlapBehavior overlapBehavior, bool consumesBalance, Guid? balanceBucketId, Guid? workingCalendarId, DateTime createdAtUtc) =>
        new(Guid.NewGuid(), leavePolicyId, versionNumber, effectiveFrom, effectiveTo, dayCountMode, allowHalfDay, minimumNoticeDays, noticeDayCountMode, maximumRequestDays, overlapBehavior, consumesBalance, balanceBucketId, workingCalendarId, createdAtUtc);

    public void UpdateDraft(DateOnly effectiveFrom, DateOnly? effectiveTo, PolicyDayCountMode dayCountMode, bool allowHalfDay, int? minimumNoticeDays, PolicyDayCountMode noticeDayCountMode, decimal? maximumRequestDays, PolicyOverlapBehavior overlapBehavior, bool consumesBalance, Guid? balanceBucketId, Guid? workingCalendarId, DateTime updatedAtUtc)
    {
        EnsureDraft();
        ApplyRules(effectiveFrom, effectiveTo, dayCountMode, allowHalfDay, minimumNoticeDays, noticeDayCountMode, maximumRequestDays, overlapBehavior, consumesBalance, balanceBucketId, workingCalendarId);
        UpdatedAtUtc = EnsureUtc(updatedAtUtc, nameof(updatedAtUtc));
    }

    public void Publish(DateTime publishedAtUtc)
    {
        EnsureDraft();
        Status = LeavePolicyVersionStatus.Published;
        PublishedAtUtc = EnsureUtc(publishedAtUtc, nameof(publishedAtUtc));
        UpdatedAtUtc = PublishedAtUtc.Value;
    }

    private void ApplyRules(DateOnly effectiveFrom, DateOnly? effectiveTo, PolicyDayCountMode dayCountMode, bool allowHalfDay, int? minimumNoticeDays, PolicyDayCountMode noticeDayCountMode, decimal? maximumRequestDays, PolicyOverlapBehavior overlapBehavior, bool consumesBalance, Guid? balanceBucketId, Guid? workingCalendarId)
    {
        if (effectiveTo is not null && effectiveTo.Value < effectiveFrom) throw new ArgumentException("EffectiveTo must be greater than or equal to EffectiveFrom.");
        if (!Enum.IsDefined(dayCountMode)) throw new ArgumentOutOfRangeException(nameof(dayCountMode), "Day count mode is invalid.");
        if (!Enum.IsDefined(noticeDayCountMode)) throw new ArgumentOutOfRangeException(nameof(noticeDayCountMode), "Notice day count mode is invalid.");
        if (!Enum.IsDefined(overlapBehavior)) throw new ArgumentOutOfRangeException(nameof(overlapBehavior), "Overlap behavior is invalid.");
        if (minimumNoticeDays is < 0) throw new ArgumentOutOfRangeException(nameof(minimumNoticeDays), "Minimum notice days must be non-negative.");
        if (maximumRequestDays is <= 0) throw new ArgumentOutOfRangeException(nameof(maximumRequestDays), "Maximum request days must be greater than zero.");
        if (consumesBalance && balanceBucketId is null) throw new InvalidOperationException("A balance bucket is required when the policy consumes balance.");
        if (!consumesBalance && balanceBucketId is not null) throw new InvalidOperationException("Balance bucket must be empty when the policy does not consume balance.");
        if ((dayCountMode == PolicyDayCountMode.BusinessDays || noticeDayCountMode == PolicyDayCountMode.BusinessDays) && workingCalendarId is null)
            throw new InvalidOperationException("WorkingCalendarId is required when day count or notice mode uses BUSINESS_DAYS.");

        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        DayCountMode = dayCountMode;
        AllowHalfDay = allowHalfDay;
        MinimumNoticeDays = minimumNoticeDays;
        NoticeDayCountMode = noticeDayCountMode;
        MaximumRequestDays = maximumRequestDays;
        OverlapBehavior = overlapBehavior;
        ConsumesBalance = consumesBalance;
        BalanceBucketId = balanceBucketId;
        WorkingCalendarId = workingCalendarId;
    }

    private void EnsureDraft()
    {
        if (Status == LeavePolicyVersionStatus.Published) throw new InvalidOperationException("Published policy versions are immutable.");
    }

    private static DateTime EnsureUtc(DateTime value, string name)
    {
        if (value.Kind != DateTimeKind.Utc) throw new ArgumentException("Timestamp must be UTC.", name);
        return value;
    }
}
