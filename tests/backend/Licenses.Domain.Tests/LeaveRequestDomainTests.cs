using Licenses.Domain.LeaveManagement;

namespace Licenses.Domain.Tests;

public sealed class LeaveRequestDomainTests
{
    [Fact]
    public void DraftCanBeEdited()
    {
        var request = Draft();
        var newOrgUnitId = Guid.NewGuid();
        var newLeaveTypeId = Guid.NewGuid();
        request.UpdateDraft(newOrgUnitId, newLeaveTypeId, new DateOnly(2026, 9, 2), new DateOnly(2026, 9, 3), LeaveRequestDayPortion.FullDay, "Updated", DateTime.UtcNow);
        Assert.Equal(newOrgUnitId, request.OrgUnitId);
        Assert.Equal(newLeaveTypeId, request.LeaveTypeId);
        Assert.Equal("Updated", request.Comment);
    }

    [Fact]
    public void SubmitTransitionsDraftAndFreezesEvaluatedValues()
    {
        var request = Draft();
        var policyVersionId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var operationId = request.EnsureReservationOperationId();
        request.Submit(policyVersionId, 2.5m, accountId, operationId, DateTime.UtcNow);
        Assert.Equal(LeaveRequestStatus.PendingApproval, request.Status);
        Assert.Equal(policyVersionId, request.LeavePolicyVersionId);
        Assert.Equal(2.5m, request.CalculatedDays);
        Assert.Equal(accountId, request.BalanceAccountId);
        Assert.Equal(operationId, request.BalanceReservationOperationId);
        Assert.NotNull(request.SubmittedAtUtc);
    }

    [Fact]
    public void SubmittedRequestCannotBeEditedOrSubmittedAgainThroughDomainCommand()
    {
        var request = Draft();
        request.Submit(Guid.NewGuid(), 1m, null, null, DateTime.UtcNow);
        Assert.Throws<InvalidOperationException>(() => request.UpdateDraft(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1), LeaveRequestDayPortion.FullDay, null, DateTime.UtcNow));
        Assert.Throws<InvalidOperationException>(() => request.Submit(Guid.NewGuid(), 1m, null, null, DateTime.UtcNow));
    }

    [Fact]
    public void RejectsInvalidDatesAndHalfDayRanges()
    {
        Assert.Throws<ArgumentException>(() => Draft(start: new DateOnly(2026, 9, 2), end: new DateOnly(2026, 9, 1)));
        Assert.Throws<InvalidOperationException>(() => Draft(start: new DateOnly(2026, 9, 1), end: new DateOnly(2026, 9, 2), portion: LeaveRequestDayPortion.HalfDay));
    }

    [Fact]
    public void ReservationOperationIdIsStable()
    {
        var request = Draft();
        var first = request.EnsureReservationOperationId();
        var second = request.EnsureReservationOperationId();
        Assert.Equal(first, second);
    }

    private static LeaveRequest Draft(DateOnly? start = null, DateOnly? end = null, LeaveRequestDayPortion portion = LeaveRequestDayPortion.FullDay) =>
        LeaveRequest.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), start ?? new DateOnly(2026, 9, 1), end ?? new DateOnly(2026, 9, 1), portion, null, Guid.NewGuid(), DateTime.UtcNow);
}
