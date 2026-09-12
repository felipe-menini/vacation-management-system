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

    [Fact]
    public void PendingApprovalCanApproveOrRejectOnlyOnce()
    {
        var approved = Submitted();
        approved.Approve(DateTime.UtcNow);
        Assert.Equal(LeaveRequestStatus.Approved, approved.Status);
        Assert.Throws<InvalidOperationException>(() => approved.Reject(DateTime.UtcNow));

        var rejected = Submitted();
        rejected.Reject(DateTime.UtcNow);
        Assert.Equal(LeaveRequestStatus.Rejected, rejected.Status);
        Assert.Throws<InvalidOperationException>(() => rejected.Approve(DateTime.UtcNow));
    }

    [Fact]
    public void DraftCannotBeApprovedOrRejected()
    {
        var request = Draft();
        Assert.Throws<InvalidOperationException>(() => request.Approve(DateTime.UtcNow));
        Assert.Throws<InvalidOperationException>(() => request.Reject(DateTime.UtcNow));
    }

    [Fact]
    public void DecisionRequiresRejectionReasonAndKeepsRequestCommentUnchanged()
    {
        var request = Submitted(comment: "Employee comment");
        var decision = LeaveRequestDecision.Create(request.Id, LeaveRequestDecisionKind.Reject, Guid.NewGuid(), "  Not eligible  ", Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        request.Reject(decision.CreatedAtUtc);

        Assert.Equal("Employee comment", request.Comment);
        Assert.Equal("Not eligible", decision.Comment);
        Assert.Throws<ArgumentException>(() => LeaveRequestDecision.Create(request.Id, LeaveRequestDecisionKind.Reject, Guid.NewGuid(), " ", Guid.NewGuid(), null, DateTime.UtcNow));
    }



    [Fact]
    public void ApprovedRequestCanRequestCancellationThenBeCancelledOrReturnedToApproved()
    {
        var cancelled = Submitted();
        cancelled.Approve(DateTime.UtcNow);
        var originalPolicyVersion = cancelled.LeavePolicyVersionId;
        var originalDays = cancelled.CalculatedDays;
        cancelled.RequestCancellation(DateTime.UtcNow);
        cancelled.ApproveCancellation(DateTime.UtcNow);

        Assert.Equal(LeaveRequestStatus.Cancelled, cancelled.Status);
        Assert.Equal(originalPolicyVersion, cancelled.LeavePolicyVersionId);
        Assert.Equal(originalDays, cancelled.CalculatedDays);

        var rejectedCancellation = Submitted();
        rejectedCancellation.Approve(DateTime.UtcNow);
        rejectedCancellation.RequestCancellation(DateTime.UtcNow);
        rejectedCancellation.RejectCancellation(DateTime.UtcNow);
        Assert.Equal(LeaveRequestStatus.Approved, rejectedCancellation.Status);
    }

    [Fact]
    public void ApprovedRequestCanBeRevokedAndInvalidStatesCannotUseEp09Transitions()
    {
        var request = Submitted();
        Assert.Throws<InvalidOperationException>(() => request.RequestCancellation(DateTime.UtcNow));
        Assert.Throws<InvalidOperationException>(() => request.Revoke(DateTime.UtcNow));
        request.Approve(DateTime.UtcNow);
        request.Revoke(DateTime.UtcNow);
        Assert.Equal(LeaveRequestStatus.Revoked, request.Status);
        Assert.NotNull(request.RevokedAtUtc);
    }

    [Fact]
    public void ApprovedRequestCompletesOnlyAfterBusinessEndDateAndFreezesFacts()
    {
        var request = Submitted();
        request.Approve(DateTime.UtcNow);
        var policyVersionId = request.LeavePolicyVersionId;
        var calculatedDays = request.CalculatedDays;
        var startDate = request.StartDate;
        var endDate = request.EndDate;
        var completedAt = new DateTime(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);

        Assert.False(request.IsEligibleForCompletion(endDate));
        Assert.False(request.Complete(endDate, completedAt));
        Assert.Equal(LeaveRequestStatus.Approved, request.Status);

        Assert.True(request.Complete(endDate.AddDays(1), completedAt));
        Assert.Equal(LeaveRequestStatus.Completed, request.Status);
        Assert.Equal(completedAt, request.CompletedAtUtc);
        Assert.Equal(policyVersionId, request.LeavePolicyVersionId);
        Assert.Equal(calculatedDays, request.CalculatedDays);
        Assert.Equal(startDate, request.StartDate);
        Assert.Equal(endDate, request.EndDate);
    }

    [Fact]
    public void NonApprovedRequestsAreNotCompleted()
    {
        var businessToday = new DateOnly(2026, 9, 16);
        var completedAt = new DateTime(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);

        var statuses = new[]
        {
            LeaveRequestStatus.Draft,
            LeaveRequestStatus.PendingApproval,
            LeaveRequestStatus.Rejected,
            LeaveRequestStatus.CancellationRequested,
            LeaveRequestStatus.Cancelled,
            LeaveRequestStatus.Revoked,
            LeaveRequestStatus.Completed
        };

        foreach (var status in statuses)
        {
            var request = RequestWithStatus(status);
            Assert.False(request.Complete(businessToday, completedAt));
            Assert.Equal(status, request.Status);
        }
    }

    [Fact]
    public void CancellationAndRevocationHistoryRequireReasons()
    {
        Assert.Throws<ArgumentException>(() => LeaveRequestCancellation.Create(Guid.NewGuid(), Guid.NewGuid(), " ", Guid.NewGuid(), DateTime.UtcNow));
        Assert.Throws<ArgumentException>(() => LeaveRequestRevocation.Create(Guid.NewGuid(), Guid.NewGuid(), " ", Guid.NewGuid(), null, DateTime.UtcNow));
        var cancellation = LeaveRequestCancellation.Create(Guid.NewGuid(), Guid.NewGuid(), "  Need to change dates  ", Guid.NewGuid(), DateTime.UtcNow);
        Assert.Equal("Need to change dates", cancellation.Reason);
        Assert.Throws<ArgumentException>(() => cancellation.Decide(LeaveRequestCancellationDecision.Reject, Guid.NewGuid(), " ", Guid.NewGuid(), null, DateTime.UtcNow));
    }

    private static LeaveRequest Draft(DateOnly? start = null, DateOnly? end = null, LeaveRequestDayPortion portion = LeaveRequestDayPortion.FullDay) =>
        LeaveRequest.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), start ?? new DateOnly(2026, 9, 1), end ?? new DateOnly(2026, 9, 1), portion, null, Guid.NewGuid(), DateTime.UtcNow);

    private static LeaveRequest Submitted(string? comment = null)
    {
        var request = LeaveRequest.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1), LeaveRequestDayPortion.FullDay, comment, Guid.NewGuid(), DateTime.UtcNow);
        request.Submit(Guid.NewGuid(), 1m, null, null, DateTime.UtcNow);
        return request;
    }

    private static LeaveRequest RequestWithStatus(LeaveRequestStatus status)
    {
        var request = Submitted();
        switch (status)
        {
            case LeaveRequestStatus.Draft:
                return Draft();
            case LeaveRequestStatus.PendingApproval:
                return request;
            case LeaveRequestStatus.Approved:
                request.Approve(DateTime.UtcNow);
                return request;
            case LeaveRequestStatus.Rejected:
                request.Reject(DateTime.UtcNow);
                return request;
            case LeaveRequestStatus.CancellationRequested:
                request.Approve(DateTime.UtcNow);
                request.RequestCancellation(DateTime.UtcNow);
                return request;
            case LeaveRequestStatus.Cancelled:
                request.Approve(DateTime.UtcNow);
                request.RequestCancellation(DateTime.UtcNow);
                request.ApproveCancellation(DateTime.UtcNow);
                return request;
            case LeaveRequestStatus.Revoked:
                request.Approve(DateTime.UtcNow);
                request.Revoke(DateTime.UtcNow);
                return request;
            case LeaveRequestStatus.Completed:
                request.Approve(DateTime.UtcNow);
                request.Complete(new DateOnly(2026, 9, 2), DateTime.UtcNow);
                return request;
            default:
                throw new ArgumentOutOfRangeException(nameof(status));
        }
    }
}
