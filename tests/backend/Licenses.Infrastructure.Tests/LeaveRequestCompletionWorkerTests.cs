using Licenses.Application.Audit;
using Licenses.Application.Common;
using Licenses.Application.LeaveManagement;
using Licenses.Domain.Identity;
using Licenses.Domain.LeaveManagement;
using Licenses.Domain.Organization;
using Licenses.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Licenses.Infrastructure.Tests;

public sealed class LeaveRequestCompletionWorkerTests
{
    private static readonly DateTime SubmittedAt = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime CompletedAt = new(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ProcessorUsesBusinessTodayAndProcessesStartupBacklog()
    {
        var request = ApprovedRequest(new DateOnly(2026, 9, 14));
        var repository = new FakeLeaveRequestRepository([request]);
        var processor = CreateProcessor(repository, new DateOnly(2026, 9, 15), batchSize: 10);

        var result = await processor.ProcessBacklogAsync(CancellationToken.None);

        Assert.Equal(1, result.CompletedCount);
        Assert.Equal(new DateOnly(2026, 9, 15), repository.LastBusinessToday);
        Assert.Equal(LeaveRequestStatus.Completed, request.Status);
    }

    [Fact]
    public async Task ProcessorRespectsBatchSizeAndProcessesMultipleBatches()
    {
        var requests = Enumerable.Range(0, 3).Select(_ => ApprovedRequest(new DateOnly(2026, 9, 14))).ToList();
        var repository = new FakeLeaveRequestRepository(requests);
        var processor = CreateProcessor(repository, new DateOnly(2026, 9, 15), batchSize: 2);

        var result = await processor.ProcessBacklogAsync(CancellationToken.None);

        Assert.Equal(2, result.Passes);
        Assert.Equal(3, result.CompletedCount);
        Assert.All(requests, request => Assert.Equal(LeaveRequestStatus.Completed, request.Status));
        Assert.All(repository.RequestedLimits, limit => Assert.Equal(2, limit));
    }

    [Fact]
    public async Task ProcessorStopsWhenNoEligibleRequestsRemain()
    {
        var request = ApprovedRequest(new DateOnly(2026, 9, 16));
        var repository = new FakeLeaveRequestRepository([request]);
        var processor = CreateProcessor(repository, new DateOnly(2026, 9, 15), batchSize: 10);

        var result = await processor.ProcessBacklogAsync(CancellationToken.None);

        Assert.Equal(1, result.Passes);
        Assert.Equal(0, result.CompletedCount);
        Assert.Equal(LeaveRequestStatus.Approved, request.Status);
    }

    [Fact]
    public async Task ProcessorStopsPathologicalFullSkippedBatch()
    {
        var request = ApprovedRequest(new DateOnly(2026, 9, 14));
        var repository = new FakeLeaveRequestRepository([request])
        {
            BeforeLockReturn = locked => locked.RequestCancellation(CompletedAt),
            AlwaysReturnOriginalCandidates = true
        };
        var processor = CreateProcessor(repository, new DateOnly(2026, 9, 15), batchSize: 1);

        var result = await processor.ProcessBacklogAsync(CancellationToken.None);

        Assert.Equal(1, result.Passes);
        Assert.Equal(1, result.SkippedCount);
        Assert.Equal(LeaveRequestStatus.CancellationRequested, request.Status);
    }

    [Fact]
    public async Task WorkerSurvivesTransientPassFailureAndRetriesNextPoll()
    {
        var processor = new FlakyCompletionProcessor();
        using var worker = CreateWorker(processor, TimeSpan.FromMilliseconds(10));
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        await worker.StartAsync(cts.Token);
        while (processor.Calls < 2 && !cts.IsCancellationRequested)
        {
            await Task.Delay(10, cts.Token);
        }
        await worker.StopAsync(CancellationToken.None);

        Assert.True(processor.Calls >= 2);
    }

    [Fact]
    public async Task WorkerHonorsCancellationToken()
    {
        var processor = new FlakyCompletionProcessor();
        using var worker = CreateWorker(processor, TimeSpan.FromMilliseconds(100));
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        await worker.StartAsync(CancellationToken.None);
        while (processor.Calls < 1 && !cts.IsCancellationRequested)
        {
            await Task.Delay(10, cts.Token);
        }
        await worker.StopAsync(CancellationToken.None);

        Assert.True(processor.Calls >= 1);
    }

    [Fact]
    public void LeaveCompletionOptionsRejectsUnboundedBatchSize()
    {
        var validator = new LeaveCompletionOptionsValidator();

        var result = validator.Validate(null, new LeaveCompletionOptions
        {
            Enabled = true,
            PollInterval = TimeSpan.FromMinutes(10),
            BatchSize = LeaveRequestCompletionService.MaxBatchSize + 1
        });

        Assert.True(result.Failed);
    }

    private static LeaveRequestCompletionProcessor CreateProcessor(FakeLeaveRequestRepository repository, DateOnly businessToday, int batchSize)
    {
        var service = new LeaveRequestCompletionService(repository, new FixedBusinessDateProvider(businessToday), new FixedClock(CompletedAt), new RecordingAuditWriter());
        return new LeaveRequestCompletionProcessor(
            service,
            Options.Create(new LeaveCompletionOptions { BatchSize = batchSize, PollInterval = TimeSpan.FromMinutes(1), Enabled = true }),
            NullLogger<LeaveRequestCompletionProcessor>.Instance);
    }

    private static LeaveRequestCompletionWorker CreateWorker(ILeaveRequestCompletionProcessor processor, TimeSpan pollInterval)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => processor);
        var provider = services.BuildServiceProvider();
        return new LeaveRequestCompletionWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new LeaveCompletionOptions { Enabled = true, PollInterval = pollInterval, BatchSize = 1 }),
            NullLogger<LeaveRequestCompletionWorker>.Instance);
    }

    private static LeaveRequest ApprovedRequest(DateOnly endDate)
    {
        var startDate = endDate.AddDays(-1);
        var request = LeaveRequest.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), startDate, endDate, LeaveRequestDayPortion.FullDay, null, Guid.NewGuid(), SubmittedAt);
        request.Submit(Guid.NewGuid(), 2m, Guid.NewGuid(), Guid.NewGuid(), SubmittedAt);
        request.Approve(SubmittedAt);
        return request;
    }

    private sealed class FakeLeaveRequestRepository(List<LeaveRequest> requests) : ILeaveRequestRepository
    {
        private readonly List<Guid> originalCandidateIds = [];

        public Action<LeaveRequest>? BeforeLockReturn { get; init; }
        public bool AlwaysReturnOriginalCandidates { get; init; }
        public DateOnly? LastBusinessToday { get; private set; }
        public List<int> RequestedLimits { get; } = [];

        public Task<IReadOnlyList<LeaveRequest>> ListEligibleApprovedForCompletionAsync(DateOnly businessToday, int limit, CancellationToken cancellationToken)
        {
            LastBusinessToday = businessToday;
            RequestedLimits.Add(limit);
            if (AlwaysReturnOriginalCandidates && originalCandidateIds.Count > 0)
            {
                return Task.FromResult<IReadOnlyList<LeaveRequest>>(originalCandidateIds.Select(id => requests.Single(x => x.Id == id)).Take(limit).ToList());
            }

            var candidates = requests
                .Where(x => x.Status == LeaveRequestStatus.Approved && x.EndDate < businessToday)
                .OrderBy(x => x.EndDate)
                .ThenBy(x => x.Id)
                .Take(limit)
                .ToList();
            if (AlwaysReturnOriginalCandidates && originalCandidateIds.Count == 0)
            {
                originalCandidateIds.AddRange(candidates.Select(x => x.Id));
            }

            return Task.FromResult<IReadOnlyList<LeaveRequest>>(candidates);
        }

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

    private sealed class FlakyCompletionProcessor : ILeaveRequestCompletionProcessor
    {
        public int Calls { get; private set; }
        public Task<LeaveRequestCompletionProcessingResult> ProcessBacklogAsync(CancellationToken cancellationToken)
        {
            Calls++;
            if (Calls == 1) throw new InvalidOperationException("Transient failure.");
            return Task.FromResult(new LeaveRequestCompletionProcessingResult(1, 0, 0, 0));
        }
    }

    private sealed class RecordingAuditWriter : IAuditWriter
    {
        public Task WriteAsync(AuditEventData auditEvent, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FixedBusinessDateProvider(DateOnly today) : IBusinessDateProvider { public DateOnly Today { get; } = today; }
    private sealed class FixedClock(DateTimeOffset utcNow) : IClock { public DateTimeOffset UtcNow { get; } = utcNow; }
    private sealed class NoopTransaction : IDisposable { public void Dispose() { } }
}
