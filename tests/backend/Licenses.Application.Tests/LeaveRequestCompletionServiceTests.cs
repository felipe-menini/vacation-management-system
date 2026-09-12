using Licenses.Application.Audit;
using Licenses.Application.Common;
using Licenses.Application.LeaveManagement;
using Licenses.Domain.Identity;
using Licenses.Domain.LeaveManagement;
using Licenses.Domain.Organization;

namespace Licenses.Application.Tests;

public sealed class LeaveRequestCompletionServiceTests
{
    private static readonly DateTime SubmittedAt = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime CompletedAt = new(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CompletesOnlyApprovedRequestsWhoseEndDateIsBeforeBusinessToday()
    {
        var eligible = ApprovedRequest(new DateOnly(2026, 9, 14));
        var sameDay = ApprovedRequest(new DateOnly(2026, 9, 15));
        var future = ApprovedRequest(new DateOnly(2026, 9, 16));
        var cancellationRequested = ApprovedRequest(new DateOnly(2026, 9, 14));
        cancellationRequested.RequestCancellation(CompletedAt);
        var rejected = SubmittedRequest(new DateOnly(2026, 9, 14));
        rejected.Reject(CompletedAt);
        var repository = new FakeLeaveRequestRepository([eligible, sameDay, future, cancellationRequested, rejected]);
        var service = CreateService(repository, new DateOnly(2026, 9, 15));

        var result = await service.CompleteEligibleAsync(cancellationToken: CancellationToken.None);

        Assert.Equal(1, result.CompletedCount);
        Assert.Equal(eligible.Id, Assert.Single(result.CompletedRequestIds));
        Assert.Equal(LeaveRequestStatus.Completed, eligible.Status);
        Assert.Equal(CompletedAt, eligible.CompletedAtUtc);
        Assert.Equal(LeaveRequestStatus.Approved, sameDay.Status);
        Assert.Equal(LeaveRequestStatus.Approved, future.Status);
        Assert.Equal(LeaveRequestStatus.CancellationRequested, cancellationRequested.Status);
        Assert.Equal(LeaveRequestStatus.Rejected, rejected.Status);
    }

    [Fact]
    public async Task UsesBusinessLocalDateInsteadOfRawUtcDate()
    {
        var request = ApprovedRequest(new DateOnly(2026, 9, 15));
        var repository = new FakeLeaveRequestRepository([request]);
        var clockBeforeBusinessMidnight = new FixedClock(new DateTimeOffset(2026, 9, 16, 1, 0, 0, TimeSpan.Zero));
        var businessDateProvider = new BusinessDateProvider(clockBeforeBusinessMidnight, TimeZoneInfo.FindSystemTimeZoneById("America/Montevideo"));

        var result = await new LeaveRequestCompletionService(repository, businessDateProvider, new FixedClock(CompletedAt), new RecordingAuditWriter()).CompleteEligibleAsync(cancellationToken: CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 9, 15), result.BusinessToday);
        Assert.Equal(0, result.CompletedCount);
        Assert.Equal(LeaveRequestStatus.Approved, request.Status);
    }

    [Fact]
    public async Task KeepsCompletionBalanceFreeAndFrozenFactsUntouched()
    {
        var request = ApprovedRequest(new DateOnly(2026, 9, 14));
        var policyVersionId = request.LeavePolicyVersionId;
        var calculatedDays = request.CalculatedDays;
        var startDate = request.StartDate;
        var endDate = request.EndDate;
        var balanceAccountId = request.BalanceAccountId;
        var reservationOperationId = request.BalanceReservationOperationId;

        var result = await CreateService(new FakeLeaveRequestRepository([request]), new DateOnly(2026, 9, 15)).CompleteEligibleAsync(cancellationToken: CancellationToken.None);

        Assert.Equal(1, result.CompletedCount);
        Assert.Equal(policyVersionId, request.LeavePolicyVersionId);
        Assert.Equal(calculatedDays, request.CalculatedDays);
        Assert.Equal(startDate, request.StartDate);
        Assert.Equal(endDate, request.EndDate);
        Assert.Equal(balanceAccountId, request.BalanceAccountId);
        Assert.Equal(reservationOperationId, request.BalanceReservationOperationId);
    }

    [Fact]
    public async Task CompletionIsIdempotentlySkippedWhenAlreadyCompleted()
    {
        var request = ApprovedRequest(new DateOnly(2026, 9, 14));
        request.Complete(new DateOnly(2026, 9, 15), CompletedAt);
        var originalCompletedAt = request.CompletedAtUtc;

        var result = await CreateService(new FakeLeaveRequestRepository([request]), new DateOnly(2026, 9, 16)).CompleteEligibleAsync(cancellationToken: CancellationToken.None);

        Assert.Equal(0, result.CompletedCount);
        Assert.Equal(0, result.ScannedCount);
        Assert.Equal(LeaveRequestStatus.Completed, request.Status);
        Assert.Equal(originalCompletedAt, request.CompletedAtUtc);
    }

    [Fact]
    public async Task RechecksLockedStatusAndSkipsCancellationRaceCleanly()
    {
        var request = ApprovedRequest(new DateOnly(2026, 9, 14));
        var repository = new FakeLeaveRequestRepository([request])
        {
            BeforeLockReturn = locked => locked.RequestCancellation(CompletedAt)
        };

        var result = await CreateService(repository, new DateOnly(2026, 9, 15)).CompleteEligibleAsync(cancellationToken: CancellationToken.None);

        Assert.Equal(1, result.ScannedCount);
        Assert.Equal(0, result.CompletedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Equal(LeaveRequestStatus.CancellationRequested, request.Status);
    }


    [Fact]
    public async Task SuccessfulCompletionWritesSystemAuditOnce()
    {
        var request = ApprovedRequest(new DateOnly(2026, 9, 14));
        var audit = new RecordingAuditWriter();
        var service = new LeaveRequestCompletionService(new FakeLeaveRequestRepository([request]), new FixedBusinessDateProvider(new DateOnly(2026, 9, 15)), new FixedClock(CompletedAt), audit);

        await service.CompleteEligibleAsync(cancellationToken: CancellationToken.None);
        await service.CompleteEligibleAsync(cancellationToken: CancellationToken.None);

        var auditEvent = Assert.Single(audit.Events);
        Assert.Null(auditEvent.ActorUserId);
        Assert.Equal("leave.request.complete", auditEvent.Action);
        Assert.Equal("LeaveRequest", auditEvent.ResourceType);
        Assert.Equal(request.Id, auditEvent.ResourceId);
        Assert.Equal(request.UserId, auditEvent.SubjectUserId);
        Assert.Equal(request.OrgUnitId, auditEvent.OrgUnitId);
        Assert.Equal(CompletedAt, auditEvent.OccurredAtUtc);
        Assert.Contains("previousStatus", auditEvent.MetadataJson);
        Assert.Contains("resultingStatus", auditEvent.MetadataJson);
        Assert.DoesNotContain("document", auditEvent.MetadataJson!, StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public async Task QueryRespectsBatchLimitAndDeterministicOrdering()
    {
        var later = ApprovedRequest(new DateOnly(2026, 9, 13), id: Guid.Parse("00000000-0000-0000-0000-000000000003"));
        var firstSameDay = ApprovedRequest(new DateOnly(2026, 9, 12), id: Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var secondSameDay = ApprovedRequest(new DateOnly(2026, 9, 12), id: Guid.Parse("00000000-0000-0000-0000-000000000002"));
        var repository = new FakeLeaveRequestRepository([later, secondSameDay, firstSameDay]);

        var listed = await repository.ListEligibleApprovedForCompletionAsync(new DateOnly(2026, 9, 14), 2, CancellationToken.None);

        Assert.Equal([firstSameDay.Id, secondSameDay.Id], listed.Select(x => x.Id));
    }

    private static LeaveRequestCompletionService CreateService(FakeLeaveRequestRepository repository, DateOnly businessToday) =>
        new(repository, new FixedBusinessDateProvider(businessToday), new FixedClock(CompletedAt), new RecordingAuditWriter());

    private static LeaveRequest ApprovedRequest(DateOnly endDate, Guid? id = null)
    {
        var request = SubmittedRequest(endDate, id);
        request.Approve(SubmittedAt);
        return request;
    }

    private static LeaveRequest SubmittedRequest(DateOnly endDate, Guid? id = null)
    {
        var startDate = endDate.AddDays(-1);
        var request = LeaveRequest.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), startDate, endDate, LeaveRequestDayPortion.FullDay, null, Guid.NewGuid(), SubmittedAt);
        if (id is { } value)
        {
            typeof(LeaveRequest).GetProperty(nameof(LeaveRequest.Id))!.SetValue(request, value);
        }
        request.Submit(Guid.NewGuid(), 2m, Guid.NewGuid(), Guid.NewGuid(), SubmittedAt);
        return request;
    }

    private sealed class FakeLeaveRequestRepository(List<LeaveRequest> requests) : ILeaveRequestRepository
    {
        public Action<LeaveRequest>? BeforeLockReturn { get; init; }
        public Task<IReadOnlyList<LeaveRequest>> ListEligibleApprovedForCompletionAsync(DateOnly businessToday, int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<LeaveRequest>>(requests
                .Where(x => x.Status == LeaveRequestStatus.Approved && x.EndDate < businessToday)
                .OrderBy(x => x.EndDate)
                .ThenBy(x => x.Id)
                .Take(limit)
                .ToList());
        public Task<LeaveRequest?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken)
        {
            var request = requests.SingleOrDefault(x => x.Id == id);
            if (request is not null) BeforeLockReturn?.Invoke(request);
            return Task.FromResult(request);
        }
        public Task<IDisposable> BeginTransactionAsync(CancellationToken cancellationToken) => Task.FromResult<IDisposable>(new NoopTransaction());
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task CommitTransactionAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<LeaveRequest>> ListByUserAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<LeaveRequest>> ListByUsersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<LeaveRequest>> ListPendingByOrgUnitsAsync(IReadOnlyCollection<Guid> orgUnitIds, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<LeaveRequest>> ListPendingCancellationByOrgUnitsAsync(IReadOnlyCollection<Guid> orgUnitIds, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LeaveRequest?> GetAsync(Guid id, bool tracking, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LeaveRequest?> GetBySubmissionOperationIdAsync(Guid operationId, bool tracking, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LeaveRequestDocument?> GetDocumentAsync(Guid id, bool tracking, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<LeaveRequestDocument>> ListDocumentsByRequestIdAsync(Guid requestId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<LeaveRequestDocument>> ListDocumentsByRequestIdsAsync(IReadOnlyCollection<Guid> requestIds, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task AddDocumentAsync(LeaveRequestDocument document, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LeaveRequestDecision?> GetDecisionByOperationIdAsync(Guid operationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LeaveRequestDecision?> GetDecisionByRequestIdAsync(Guid requestId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<LeaveRequestDecision>> ListDecisionsByRequestIdsAsync(IReadOnlyCollection<Guid> requestIds, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LeaveRequestCancellation?> GetCancellationByOperationIdAsync(Guid operationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LeaveRequestCancellation?> GetCancellationByDecisionOperationIdAsync(Guid operationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LeaveRequestCancellation?> GetCancellationByRequestIdAsync(Guid requestId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<LeaveRequestCancellation>> ListCancellationsByRequestIdsAsync(IReadOnlyCollection<Guid> requestIds, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LeaveRequestRevocation?> GetRevocationByOperationIdAsync(Guid operationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LeaveRequestRevocation?> GetRevocationByRequestIdAsync(Guid requestId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<LeaveRequestRevocation>> ListRevocationsByRequestIdsAsync(IReadOnlyCollection<Guid> requestIds, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task AddAsync(LeaveRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task AddDecisionAsync(LeaveRequestDecision decision, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task AddCancellationAsync(LeaveRequestCancellation cancellation, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task AddRevocationAsync(LeaveRequestRevocation revocation, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<User?> GetUserAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<OrgUnit?> GetOrgUnitAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LeaveType?> GetLeaveTypeAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LeavePolicy?> GetPolicyAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LeavePolicyVersion?> GetPolicyVersionAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<WorkingCalendar?> GetWorkingCalendarAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Guid?> GetBalanceAccountIdAsync(Guid userId, Guid balanceBucketId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> HasEffectiveAssignmentAsync(Guid userId, Guid orgUnitId, DateOnly date, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<LeaveRequest>> ListOverlappingAsync(Guid userId, DateOnly startDate, DateOnly endDate, Guid excludingRequestId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<User>> ListUsersInOrgUnitsAsync(IReadOnlyCollection<Guid> orgUnitIds, DateTime utcNow, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class RecordingAuditWriter : IAuditWriter
    {
        public List<AuditEventData> Events { get; } = [];
        public Task WriteAsync(AuditEventData auditEvent, CancellationToken cancellationToken)
        {
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedBusinessDateProvider(DateOnly today) : IBusinessDateProvider { public DateOnly Today { get; } = today; }
    private sealed class FixedClock(DateTimeOffset utcNow) : IClock { public DateTimeOffset UtcNow { get; } = utcNow; }
    private sealed class NoopTransaction : IDisposable { public void Dispose() { } }
}