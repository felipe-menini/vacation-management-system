using Licenses.Domain.LeaveManagement;

namespace Licenses.Domain.Tests;

public sealed class LeavePolicyDomainTests
{
    [Fact]
    public void CompanyWidePolicyCanonicalizesAppliesToDescendantsToFalse()
    {
        var policy = LeavePolicy.Create(Guid.NewGuid(), null, appliesToDescendants: true, true, DateTime.UtcNow);
        Assert.False(policy.AppliesToDescendants);
    }

    [Fact]
    public void DraftVersionCanBeEditedAndPublishedVersionCannot()
    {
        var version = Draft();
        version.UpdateDraft(new DateOnly(2026, 1, 2), null, PolicyDayCountMode.CalendarDays, false, 0, PolicyDayCountMode.CalendarDays, null, PolicyOverlapBehavior.Allow, false, null, DateTime.UtcNow);
        version.Publish(DateTime.UtcNow);
        Assert.Equal(LeavePolicyVersionStatus.Published, version.Status);
        Assert.Throws<InvalidOperationException>(() => version.UpdateDraft(new DateOnly(2026, 1, 3), null, PolicyDayCountMode.CalendarDays, false, null, PolicyDayCountMode.CalendarDays, null, PolicyOverlapBehavior.Allow, false, null, DateTime.UtcNow));
    }

    [Fact]
    public void VersionRulesAreValidated()
    {
        Assert.Throws<ArgumentException>(() => Draft(effectiveFrom: new DateOnly(2026, 2, 1), effectiveTo: new DateOnly(2026, 1, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Draft(minimumNoticeDays: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Draft(maximumRequestDays: 0));
        Assert.Throws<InvalidOperationException>(() => Draft(consumesBalance: true, balanceBucketId: null));
        Assert.Throws<InvalidOperationException>(() => Draft(consumesBalance: false, balanceBucketId: Guid.NewGuid()));
        Assert.Throws<ArgumentOutOfRangeException>(() => LeavePolicyVersion.CreateDraft(Guid.NewGuid(), 0, new DateOnly(2026, 1, 1), null, PolicyDayCountMode.BusinessDays, true, null, PolicyDayCountMode.CalendarDays, null, PolicyOverlapBehavior.Block, false, null, DateTime.UtcNow));
    }

    private static LeavePolicyVersion Draft(DateOnly? effectiveFrom = null, DateOnly? effectiveTo = null, int? minimumNoticeDays = null, decimal? maximumRequestDays = 1, bool consumesBalance = false, Guid? balanceBucketId = null) =>
        LeavePolicyVersion.CreateDraft(Guid.NewGuid(), 1, effectiveFrom ?? new DateOnly(2026, 1, 1), effectiveTo, PolicyDayCountMode.BusinessDays, true, minimumNoticeDays, PolicyDayCountMode.CalendarDays, maximumRequestDays, PolicyOverlapBehavior.Block, consumesBalance, balanceBucketId, DateTime.UtcNow);
}
