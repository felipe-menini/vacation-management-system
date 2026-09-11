using Licenses.Application.Audit;
using Licenses.Application.Authorization;
using Licenses.Application.LeaveManagement;
using Licenses.Application.Notifications;
using Licenses.Domain.Authorization;
using Licenses.Domain.Identity;
using Licenses.Domain.LeaveManagement;
using Licenses.Domain.Organization;

namespace Licenses.Application.Tests;

public sealed class LeaveRequestApprovalServiceTests
{
    private static readonly DateTime Now = new(2026, 9, 11, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ApproveConsumingRequestConsumesFrozenCalculatedDaysOnce()
    {
        var setup = TestSetup.Create(consumesBalance: true);
        var service = setup.CreateService(setup.Approver.Id, PermissionCodes.LeaveRequestsDecide);

        var result = await service.ApproveAsync(setup.Request.Id, new(Guid.NewGuid(), " ok "), CancellationToken.None);
        var retry = await service.ApproveAsync(setup.Request.Id, new(result!.Decision.OperationId, "ok"), CancellationToken.None);

        Assert.Equal("APPROVED", result.Request.Status);
        Assert.Equal("APPROVE", result.Decision.Decision);
        Assert.Single(setup.Balances.Mutations);
        Assert.Equal(BalanceLedgerEntryType.Consume, setup.Balances.Mutations[0].Type);
        Assert.Equal(setup.Request.CalculatedDays, setup.Balances.Mutations[0].Amount);
        Assert.True(retry!.WasAlreadyApplied);
        Assert.Single(setup.Outbox.Messages);
        var ev = Assert.IsType<LeaveRequestApproved>(setup.Outbox.Messages[0].Event);
        Assert.Equal(setup.Request.Id, ev.LeaveRequestId);
        Assert.Equal(setup.Request.UserId, ev.SubjectUserId);
        Assert.Equal(setup.Request.OrgUnitId, ev.OrgUnitId);
        Assert.Single(setup.Audit.Events);
        Assert.Equal("leave.request.approve", setup.Audit.Events[0].Action);
        Assert.Equal(setup.Approver.Id, setup.Audit.Events[0].ActorUserId);
        Assert.Equal(setup.Employee.Id, setup.Audit.Events[0].SubjectUserId);
    }

    [Fact]
    public async Task RejectConsumingRequestRequiresReasonAndReleasesFrozenCalculatedDays()
    {
        var setup = TestSetup.Create(consumesBalance: true);
        var service = setup.CreateService(setup.Approver.Id, PermissionCodes.LeaveRequestsDecide);

        await Assert.ThrowsAsync<ArgumentException>(() => service.RejectAsync(setup.Request.Id, new(Guid.NewGuid(), " "), CancellationToken.None)!);
        var result = await service.RejectAsync(setup.Request.Id, new(Guid.NewGuid(), "Not covered"), CancellationToken.None);

        Assert.Equal("REJECTED", result!.Request.Status);
        Assert.Equal("REJECT", result.Decision.Decision);
        Assert.Single(setup.Balances.Mutations);
        Assert.Equal(BalanceLedgerEntryType.Release, setup.Balances.Mutations[0].Type);
        Assert.Single(setup.Outbox.Messages);
        Assert.IsType<LeaveRequestRejected>(setup.Outbox.Messages[0].Event);
        Assert.Single(setup.Audit.Events);
        Assert.Equal("leave.request.reject", setup.Audit.Events[0].Action);
    }

    [Fact]
    public async Task SubmitCreatesExactlyOneOutboxMessageAndRetryDoesNotDuplicate()
    {
        var setup = TestSetup.CreateDraft(consumesBalance: false);
        var service = setup.CreateService(setup.Employee.Id, PermissionCodes.LeaveRequestsCreateSelf);

        var result = await service.SubmitAsync(setup.Request.Id, CancellationToken.None);
        var retry = await service.SubmitAsync(setup.Request.Id, CancellationToken.None);

        Assert.Equal("PENDING_APPROVAL", result!.Request.Status);
        Assert.True(retry!.WasAlreadySubmitted);
        Assert.Single(setup.Outbox.Messages);
        var ev = Assert.IsType<LeaveRequestSubmitted>(setup.Outbox.Messages[0].Event);
        Assert.Equal(setup.Request.Id, ev.LeaveRequestId);
        Assert.Equal(setup.Employee.Id, ev.SubjectUserId);
        Assert.Equal(setup.Unit.Id, ev.OrgUnitId);
        Assert.Single(setup.Audit.Events);
        Assert.Equal("leave.request.submit", setup.Audit.Events[0].Action);
    }
    [Fact]
    public async Task SelfDecisionAndOperationIdConflictsAreRejected()
    {
        var setup = TestSetup.Create(consumesBalance: false);
        var operationId = Guid.NewGuid();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => setup.CreateService(setup.Employee.Id, PermissionCodes.LeaveRequestsDecide).ApproveAsync(setup.Request.Id, new(operationId, null), CancellationToken.None)!);
        Assert.Empty(setup.Outbox.Messages);
        Assert.Empty(setup.Audit.Events);

        var service = setup.CreateService(setup.Approver.Id, PermissionCodes.LeaveRequestsDecide);
        await service.ApproveAsync(setup.Request.Id, new(operationId, null), CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RejectAsync(setup.Request.Id, new(operationId, "Changed"), CancellationToken.None)!);
    }



    [Fact]
    public async Task RequestCancellationIsOwnerOnlyAndDoesNotMutateBalance()
    {
        var setup = TestSetup.Create(consumesBalance: true);
        setup.Request.Approve(Now);
        var operationId = Guid.NewGuid();
        var owner = setup.CreateService(setup.Employee.Id, PermissionCodes.LeaveRequestsCancelSelf);

        var result = await owner.RequestCancellationAsync(setup.Request.Id, new(operationId, " Plans changed "), CancellationToken.None);
        var retry = await owner.RequestCancellationAsync(setup.Request.Id, new(operationId, "Plans changed"), CancellationToken.None);

        Assert.Equal("CANCELLATION_REQUESTED", result!.Request.Status);
        Assert.Equal("Plans changed", result.Cancellation.Reason);
        Assert.True(retry!.WasAlreadyApplied);
        Assert.Empty(setup.Balances.Mutations);
        Assert.Single(setup.Outbox.Messages);
        Assert.IsType<LeaveCancellationRequested>(setup.Outbox.Messages[0].Event);
        Assert.Single(setup.Audit.Events);
        Assert.Equal("leave.request.cancellation.request", setup.Audit.Events[0].Action);
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.RequestCancellationAsync(setup.Request.Id, new(operationId, "Different"), CancellationToken.None)!);
    }

    [Fact]
    public async Task CancellationApprovalRefundsOnceAndRejectionReturnsToApprovedWithoutLedgerMutation()
    {
        var approving = TestSetup.Create(consumesBalance: true);
        approving.Request.Approve(Now);
        await approving.CreateService(approving.Employee.Id, PermissionCodes.LeaveRequestsCancelSelf).RequestCancellationAsync(approving.Request.Id, new(Guid.NewGuid(), "Cancel"), CancellationToken.None);
        var approveOperationId = Guid.NewGuid();
        var approveResult = await approving.CreateService(approving.Approver.Id, PermissionCodes.LeaveRequestsCancelDecide).ApproveCancellationAsync(approving.Request.Id, new(approveOperationId, null), CancellationToken.None);
        var approveRetry = await approving.CreateService(approving.Approver.Id, PermissionCodes.LeaveRequestsCancelDecide).ApproveCancellationAsync(approving.Request.Id, new(approveOperationId, null), CancellationToken.None);

        Assert.Equal("CANCELLED", approveResult!.Request.Status);
        Assert.Equal("APPROVE", approveResult.Cancellation.Decision);
        Assert.True(approveRetry!.WasAlreadyApplied);
        Assert.Single(approving.Balances.Mutations);
        Assert.Equal(BalanceLedgerEntryType.Refund, approving.Balances.Mutations[0].Type);
        Assert.Equal(approving.Request.CalculatedDays, approving.Balances.Mutations[0].Amount);
        Assert.Equal(2, approving.Outbox.Messages.Count);
        Assert.IsType<LeaveCancellationApproved>(approving.Outbox.Messages[1].Event);
        Assert.Equal(2, approving.Audit.Events.Count);
        Assert.Contains(approving.Audit.Events, x => x.Action == "leave.request.cancellation.approve");

        var rejecting = TestSetup.Create(consumesBalance: true);
        rejecting.Request.Approve(Now);
        await rejecting.CreateService(rejecting.Employee.Id, PermissionCodes.LeaveRequestsCancelSelf).RequestCancellationAsync(rejecting.Request.Id, new(Guid.NewGuid(), "Cancel"), CancellationToken.None);
        await Assert.ThrowsAsync<ArgumentException>(() => rejecting.CreateService(rejecting.Approver.Id, PermissionCodes.LeaveRequestsCancelDecide).RejectCancellationAsync(rejecting.Request.Id, new(Guid.NewGuid(), " "), CancellationToken.None)!);
        var rejectResult = await rejecting.CreateService(rejecting.Approver.Id, PermissionCodes.LeaveRequestsCancelDecide).RejectCancellationAsync(rejecting.Request.Id, new(Guid.NewGuid(), "Business need"), CancellationToken.None);
        Assert.Equal("APPROVED", rejectResult!.Request.Status);
        Assert.Equal("REJECT", rejectResult.Cancellation.Decision);
        Assert.Empty(rejecting.Balances.Mutations);
        Assert.Equal(2, rejecting.Outbox.Messages.Count);
        Assert.IsType<LeaveCancellationRejected>(rejecting.Outbox.Messages[1].Event);
        Assert.Equal(2, rejecting.Audit.Events.Count);
        Assert.Contains(rejecting.Audit.Events, x => x.Action == "leave.request.cancellation.reject");
    }

    [Fact]
    public async Task RevocationRefundsOnceAndBlocksSelfRevocation()
    {
        var setup = TestSetup.Create(consumesBalance: true);
        setup.Request.Approve(Now);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => setup.CreateService(setup.Employee.Id, PermissionCodes.LeaveRequestsRevoke).RevokeAsync(setup.Request.Id, new(Guid.NewGuid(), "Admin"), CancellationToken.None)!);

        var operationId = Guid.NewGuid();
        var result = await setup.CreateService(setup.Approver.Id, PermissionCodes.LeaveRequestsRevoke).RevokeAsync(setup.Request.Id, new(operationId, " Operational need "), CancellationToken.None);
        var retry = await setup.CreateService(setup.Approver.Id, PermissionCodes.LeaveRequestsRevoke).RevokeAsync(setup.Request.Id, new(operationId, "Operational need"), CancellationToken.None);

        Assert.Equal("REVOKED", result!.Request.Status);
        Assert.Equal("Operational need", result.Revocation.Reason);
        Assert.True(retry!.WasAlreadyApplied);
        Assert.Single(setup.Balances.Mutations);
        Assert.Equal(BalanceLedgerEntryType.Refund, setup.Balances.Mutations[0].Type);
        Assert.Single(setup.Outbox.Messages);
        Assert.IsType<LeaveRequestRevoked>(setup.Outbox.Messages[0].Event);
        Assert.Single(setup.Audit.Events);
        Assert.Equal("leave.request.revoke", setup.Audit.Events[0].Action);
    }

    [Fact]
    public async Task ManualCreateForOthersReusesSubmissionAndFinishesPendingApproval()
    {
        var setup = TestSetup.Create(consumesBalance: true);
        var operationId = Guid.NewGuid();
        var service = setup.CreateService(setup.Approver.Id, PermissionCodes.LeaveRequestsCreateForOthers);
        var command = new CreateLeaveRequestForUserCommand(setup.Unit.Id, setup.Type.Id, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 2), "FULL_DAY", "Created by supervisor", operationId);

        var result = await service.CreateForUserAsync(setup.Employee.Id, command, CancellationToken.None);
        var retry = await service.CreateForUserAsync(setup.Employee.Id, command, CancellationToken.None);

        Assert.Equal("PENDING_APPROVAL", result.Request.Status);
        Assert.Equal(setup.Employee.Id, result.Request.UserId);
        Assert.Equal(setup.Approver.Id, result.Request.CreatedByUserId);
        Assert.Equal(operationId, result.Request.SubmissionOperationId);
        Assert.True(retry.WasAlreadySubmitted);
        Assert.Single(setup.Balances.Mutations);
        Assert.Equal(BalanceLedgerEntryType.Reserve, setup.Balances.Mutations[0].Type);
        Assert.Single(setup.Audit.Events);
        Assert.Equal("leave.request.create_for_other", setup.Audit.Events[0].Action);
        Assert.Equal(setup.Approver.Id, setup.Audit.Events[0].ActorUserId);
        Assert.Equal(setup.Employee.Id, setup.Audit.Events[0].SubjectUserId);
    }

    [Fact]
    public async Task CreateDraftAuditsCreateOnce()
    {
        var setup = TestSetup.CreateDraft(consumesBalance: false);
        var service = setup.CreateService(setup.Employee.Id, PermissionCodes.LeaveRequestsCreateSelf);
        var command = new CreateLeaveRequestCommand(setup.Unit.Id, setup.Type.Id, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 1), "FULL_DAY", "Draft");

        var result = await service.CreateDraftAsync(command, CancellationToken.None);

        Assert.Equal("DRAFT", result.Status);
        Assert.Single(setup.Audit.Events);
        Assert.Equal("leave.request.create", setup.Audit.Events[0].Action);
        Assert.Equal(setup.Employee.Id, setup.Audit.Events[0].ActorUserId);
        Assert.Equal(setup.Employee.Id, setup.Audit.Events[0].SubjectUserId);
    }

    private sealed record TestSetup(User Employee, User Approver, OrgUnit Unit, LeaveType Type, LeavePolicy Policy, LeavePolicyVersion Version, LeaveRequest Request, FakeLeaveRequestRepository Requests, FakeBalanceRepository Balances, FakeApplicationEventOutbox Outbox, FakeAuditWriter Audit)
    {
        public static TestSetup Create(bool consumesBalance) => CreateCore(consumesBalance, submitted: true);

        public static TestSetup CreateDraft(bool consumesBalance) => CreateCore(consumesBalance, submitted: false);

        private static TestSetup CreateCore(bool consumesBalance, bool submitted)
        {
            var employee = User.Create("Employee", $"employee.{Guid.NewGuid():N}@example.test", null, Now);
            var approver = User.Create("Approver", $"approver.{Guid.NewGuid():N}@example.test", null, Now);
            var unit = OrgUnit.Create("Engineering", "ENG" + Guid.NewGuid().ToString("N")[..8], null, Now);
            var type = LeaveType.Create("VAC" + Guid.NewGuid().ToString("N")[..8], "Vacation", null, 1, true, Now);
            var bucketId = consumesBalance ? Guid.NewGuid() : (Guid?)null;
            var policy = LeavePolicy.Create(type.Id, null, false, true, Now);
            var version = LeavePolicyVersion.CreateDraft(policy.Id, 1, new DateOnly(2026, 1, 1), null, PolicyDayCountMode.CalendarDays, true, null, PolicyDayCountMode.CalendarDays, null, PolicyOverlapBehavior.Block, consumesBalance, bucketId, null, Now);
            version.Publish(Now);
            var request = LeaveRequest.CreateDraft(employee.Id, unit.Id, type.Id, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 2), LeaveRequestDayPortion.FullDay, "Employee comment", employee.Id, Now);
            if (submitted)
            {
                request.EnsureReservationOperationId();
                request.Submit(version.Id, 2m, consumesBalance ? Guid.NewGuid() : null, consumesBalance ? request.BalanceReservationOperationId : null, Now);
            }
            var balances = new FakeBalanceRepository(bucketId);
            var requests = new FakeLeaveRequestRepository(employee, approver, unit, type, policy, version, request);
            var outbox = new FakeApplicationEventOutbox();
            var audit = new FakeAuditWriter();
            return new(employee, approver, unit, type, policy, version, request, requests, balances, outbox, audit);
        }

        public LeaveRequestService CreateService(Guid actorId, params string[] permissions)
        {
            var auth = new AuthorizationService(new FakeAuthorizationRepository(actorId, Unit, Employee, Approver, permissions), new FixedTimeProvider(Now));
            var balance = new BalanceService(Balances, auth, new FixedCurrentActor(actorId), new FixedTimeProvider(Now));
            return new LeaveRequestService(Requests, new FakePolicyRepository(Policy, Version, Type, Unit), balance, auth, new FixedCurrentActor(actorId), new FixedTimeProvider(Now), Outbox, Audit);
        }
    }

    private sealed class FakeLeaveRequestRepository(User employee, User approver, OrgUnit unit, LeaveType type, LeavePolicy policy, LeavePolicyVersion version, LeaveRequest request) : ILeaveRequestRepository
    {
        private readonly List<LeaveRequest> _requests = [request];
        private readonly List<LeaveRequestDecision> _decisions = [];
        private readonly List<LeaveRequestCancellation> _cancellations = [];
        private readonly List<LeaveRequestRevocation> _revocations = [];
        private readonly List<LeaveRequestDocument> _documents = [];
        private readonly Guid _balanceAccountId = Guid.NewGuid();
        public Task<IReadOnlyList<LeaveRequest>> ListByUserAsync(Guid userId, CancellationToken ct) => Task.FromResult<IReadOnlyList<LeaveRequest>>(_requests.Where(x => x.UserId == userId).ToList());
        public Task<IReadOnlyList<LeaveRequest>> ListByUsersAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => Task.FromResult<IReadOnlyList<LeaveRequest>>(_requests.Where(x => ids.Contains(x.UserId)).ToList());
        public Task<IReadOnlyList<LeaveRequest>> ListPendingByOrgUnitsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => Task.FromResult<IReadOnlyList<LeaveRequest>>(_requests.Where(x => ids.Contains(x.OrgUnitId) && x.Status == LeaveRequestStatus.PendingApproval).ToList());
        public Task<IReadOnlyList<LeaveRequest>> ListPendingCancellationByOrgUnitsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => Task.FromResult<IReadOnlyList<LeaveRequest>>(_requests.Where(x => ids.Contains(x.OrgUnitId) && x.Status == LeaveRequestStatus.CancellationRequested).ToList());
        public Task<LeaveRequest?> GetAsync(Guid id, bool tracking, CancellationToken ct) => Task.FromResult(_requests.SingleOrDefault(x => x.Id == id));
        public Task<LeaveRequest?> GetForUpdateAsync(Guid id, CancellationToken ct) => GetAsync(id, true, ct);
        public Task<LeaveRequest?> GetBySubmissionOperationIdAsync(Guid operationId, bool tracking, CancellationToken ct) => Task.FromResult(_requests.SingleOrDefault(x => x.SubmissionOperationId == operationId));
        public Task<LeaveRequestDocument?> GetDocumentAsync(Guid id, bool tracking, CancellationToken ct) => Task.FromResult(_documents.SingleOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<LeaveRequestDocument>> ListDocumentsByRequestIdAsync(Guid requestId, CancellationToken ct) => Task.FromResult<IReadOnlyList<LeaveRequestDocument>>(_documents.Where(x => x.LeaveRequestId == requestId).ToList());
        public Task<IReadOnlyList<LeaveRequestDocument>> ListDocumentsByRequestIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => Task.FromResult<IReadOnlyList<LeaveRequestDocument>>(_documents.Where(x => ids.Contains(x.LeaveRequestId)).ToList());
        public Task AddDocumentAsync(LeaveRequestDocument document, CancellationToken ct) { _documents.Add(document); return Task.CompletedTask; }
        public Task<LeaveRequestDecision?> GetDecisionByOperationIdAsync(Guid operationId, CancellationToken ct) => Task.FromResult(_decisions.SingleOrDefault(x => x.OperationId == operationId));
        public Task<LeaveRequestDecision?> GetDecisionByRequestIdAsync(Guid requestId, CancellationToken ct) => Task.FromResult(_decisions.SingleOrDefault(x => x.LeaveRequestId == requestId));
        public Task<IReadOnlyList<LeaveRequestDecision>> ListDecisionsByRequestIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => Task.FromResult<IReadOnlyList<LeaveRequestDecision>>(_decisions.Where(x => ids.Contains(x.LeaveRequestId)).ToList());
        public Task<LeaveRequestCancellation?> GetCancellationByOperationIdAsync(Guid operationId, CancellationToken ct) => Task.FromResult(_cancellations.SingleOrDefault(x => x.OperationId == operationId));
        public Task<LeaveRequestCancellation?> GetCancellationByDecisionOperationIdAsync(Guid operationId, CancellationToken ct) => Task.FromResult(_cancellations.SingleOrDefault(x => x.DecisionOperationId == operationId));
        public Task<LeaveRequestCancellation?> GetCancellationByRequestIdAsync(Guid requestId, CancellationToken ct) => Task.FromResult(_cancellations.SingleOrDefault(x => x.LeaveRequestId == requestId));
        public Task<IReadOnlyList<LeaveRequestCancellation>> ListCancellationsByRequestIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => Task.FromResult<IReadOnlyList<LeaveRequestCancellation>>(_cancellations.Where(x => ids.Contains(x.LeaveRequestId)).ToList());
        public Task<LeaveRequestRevocation?> GetRevocationByOperationIdAsync(Guid operationId, CancellationToken ct) => Task.FromResult(_revocations.SingleOrDefault(x => x.OperationId == operationId));
        public Task<LeaveRequestRevocation?> GetRevocationByRequestIdAsync(Guid requestId, CancellationToken ct) => Task.FromResult(_revocations.SingleOrDefault(x => x.LeaveRequestId == requestId));
        public Task<IReadOnlyList<LeaveRequestRevocation>> ListRevocationsByRequestIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => Task.FromResult<IReadOnlyList<LeaveRequestRevocation>>(_revocations.Where(x => ids.Contains(x.LeaveRequestId)).ToList());
        public Task AddAsync(LeaveRequest r, CancellationToken ct) { _requests.Add(r); return Task.CompletedTask; }
        public Task AddDecisionAsync(LeaveRequestDecision decision, CancellationToken ct) { _decisions.Add(decision); return Task.CompletedTask; }
        public Task AddCancellationAsync(LeaveRequestCancellation cancellation, CancellationToken ct) { _cancellations.Add(cancellation); return Task.CompletedTask; }
        public Task AddRevocationAsync(LeaveRequestRevocation revocation, CancellationToken ct) { _revocations.Add(revocation); return Task.CompletedTask; }
        public Task<User?> GetUserAsync(Guid id, CancellationToken ct) => Task.FromResult<User?>(id == employee.Id ? employee : id == approver.Id ? approver : null);
        public Task<OrgUnit?> GetOrgUnitAsync(Guid id, CancellationToken ct) => Task.FromResult<OrgUnit?>(id == unit.Id ? unit : null);
        public Task<LeaveType?> GetLeaveTypeAsync(Guid id, CancellationToken ct) => Task.FromResult<LeaveType?>(id == type.Id ? type : null);
        public Task<LeavePolicy?> GetPolicyAsync(Guid id, CancellationToken ct) => Task.FromResult<LeavePolicy?>(policy);
        public Task<LeavePolicyVersion?> GetPolicyVersionAsync(Guid id, CancellationToken ct) => Task.FromResult<LeavePolicyVersion?>(id == version.Id ? version : null);
        public Task<WorkingCalendar?> GetWorkingCalendarAsync(Guid id, CancellationToken ct) => Task.FromResult<WorkingCalendar?>(null);
        public Task<Guid?> GetBalanceAccountIdAsync(Guid userId, Guid balanceBucketId, CancellationToken ct) => Task.FromResult<Guid?>(_requests.LastOrDefault(x => x.UserId == userId)?.BalanceAccountId ?? _balanceAccountId);
        public Task<bool> HasEffectiveAssignmentAsync(Guid userId, Guid orgUnitId, DateOnly date, CancellationToken ct) => Task.FromResult(true);
        public Task<IReadOnlyList<LeaveRequest>> ListOverlappingAsync(Guid userId, DateOnly start, DateOnly end, Guid excluding, CancellationToken ct) => Task.FromResult<IReadOnlyList<LeaveRequest>>([]);
        public Task<IReadOnlyList<User>> ListUsersInOrgUnitsAsync(IReadOnlyCollection<Guid> ids, DateTime now, CancellationToken ct) => Task.FromResult<IReadOnlyList<User>>([employee]);
        public Task<IDisposable> BeginTransactionAsync(CancellationToken ct) => Task.FromResult<IDisposable>(new NoopTransaction());
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
        public Task CommitTransactionAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeBalanceRepository(Guid? bucketId) : IBalanceRepository
    {
        public List<(BalanceLedgerEntryType Type, decimal Amount)> Mutations { get; } = [];
        public Task<User?> GetUserAsync(Guid id, CancellationToken ct) => Task.FromResult<User?>(null);
        public Task<IReadOnlyList<BalanceSnapshotRecord>> ListSnapshotsAsync(Guid id, CancellationToken ct) => Task.FromResult<IReadOnlyList<BalanceSnapshotRecord>>([]);
        public Task<IReadOnlyList<BalanceLedgerEntryRecord>?> ListLedgerAsync(Guid id, Guid b, CancellationToken ct) => Task.FromResult<IReadOnlyList<BalanceLedgerEntryRecord>?>([]);
        public Task<BalanceMutationRecord> MutateAsync(Guid userId, Guid b, Guid op, BalanceLedgerEntryType type, decimal amount, string reason, Guid? by, DateTime at, BalanceMutationAuditContext? auditContext, CancellationToken ct)
        {
            Mutations.Add((type, amount));
            return Task.FromResult(new BalanceMutationRecord(Guid.NewGuid(), op, new BalanceSnapshotRecord(userId, bucketId ?? b, "VAC", "Vacation", BalanceBucketUnit.Day, 0m, 0m), false));
        }
    }

    private sealed class FakeAuthorizationRepository(Guid actorId, OrgUnit unit, User employee, User approver, string[] permissions) : IAuthorizationRepository
    {
        private readonly Guid _roleId = Guid.NewGuid();
        public Task<User?> GetUserAsync(Guid id, CancellationToken ct) => Task.FromResult<User?>(id == employee.Id ? employee : id == approver.Id ? approver : null);
        public Task<OrgUnit?> GetOrgUnitAsync(Guid id, CancellationToken ct) => Task.FromResult<OrgUnit?>(unit);
        public Task<List<OrgUnit>> ListOrgUnitsAsync(CancellationToken ct) => Task.FromResult(new List<OrgUnit> { unit });
        public Task<List<UserOrgAssignment>> ListActiveUserOrgAssignmentsAsync(Guid id, DateTime now, CancellationToken ct) => Task.FromResult(new List<UserOrgAssignment> { UserOrgAssignment.Create(id, unit.Id, true, Now.AddDays(-1), null) });
        public Task<List<RoleScopeAssignment>> ListActiveRoleScopeAssignmentsAsync(Guid id, DateTime now, CancellationToken ct) => Task.FromResult(id == actorId ? new List<RoleScopeAssignment> { RoleScopeAssignment.Create(id, _roleId, unit.Id, false, Now.AddDays(-1), null) } : []);
        public Task<bool> RoleHasPermissionAsync(Guid roleId, string code, CancellationToken ct) => Task.FromResult(roleId == _roleId && permissions.Contains(code));
        public Task<bool> IsRoleActiveAsync(Guid roleId, CancellationToken ct) => Task.FromResult(roleId == _roleId);
        public Task<List<User>> ListUsersInOrgUnitsAsync(IReadOnlyCollection<Guid> ids, DateTime now, CancellationToken ct) => Task.FromResult(new List<User> { employee, approver });
        public Task<List<DevelopmentActorDto>> ListDevelopmentActorsAsync(DateTime now, CancellationToken ct) => Task.FromResult(new List<DevelopmentActorDto>());
    }

    private sealed class FakePolicyRepository(LeavePolicy policy, LeavePolicyVersion version, LeaveType type, OrgUnit unit) : ILeavePolicyRepository
    {
        public Task<List<LeavePolicy>> ListPoliciesAsync(CancellationToken ct) => Task.FromResult(new List<LeavePolicy>());
        public Task<LeavePolicy?> GetPolicyAsync(Guid id, CancellationToken ct) => Task.FromResult<LeavePolicy?>(null);
        public Task<LeavePolicyVersion?> GetVersionAsync(Guid id, CancellationToken ct) => Task.FromResult<LeavePolicyVersion?>(null);
        public Task<List<LeavePolicyVersion>> ListVersionsAsync(Guid policyId, CancellationToken ct) => Task.FromResult(new List<LeavePolicyVersion>());
        public Task<bool> ExactPolicyScopeExistsAsync(Guid leaveTypeId, Guid? orgUnitId, Guid? excludingPolicyId, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> HasPublishedVersionsAsync(Guid policyId, CancellationToken ct) => Task.FromResult(false);
        public Task<int> GetNextVersionNumberAsync(Guid policyId, CancellationToken ct) => Task.FromResult(1);
        public Task<bool> HasOverlappingPublishedVersionAsync(Guid policyId, DateOnly effectiveFrom, DateOnly? effectiveTo, Guid? excludingVersionId, CancellationToken ct) => Task.FromResult(false);
        public Task<List<LeavePolicy>> ListPoliciesForLeaveTypeWithPublishedVersionsAsync(Guid leaveTypeId, DateOnly date, CancellationToken ct)
        {
            policy.Versions.Clear();
            if (policy.LeaveTypeId == leaveTypeId && policy.IsActive && version.Status == LeavePolicyVersionStatus.Published && version.EffectiveFrom <= date && (version.EffectiveTo is null || version.EffectiveTo >= date))
                policy.Versions.Add(version);
            return Task.FromResult(policy.Versions.Count == 0 ? [] : new List<LeavePolicy> { policy });
        }
        public Task AddPolicyAsync(LeavePolicy policy, CancellationToken ct) => Task.CompletedTask;
        public Task AddVersionAsync(LeavePolicyVersion version, CancellationToken ct) => Task.CompletedTask;
        public Task<LeaveType?> GetLeaveTypeAsync(Guid id, CancellationToken ct) => Task.FromResult<LeaveType?>(id == type.Id ? type : null);
        public Task<BalanceBucket?> GetBalanceBucketAsync(Guid id, CancellationToken ct)
        {
            if (version.BalanceBucketId != id) return Task.FromResult<BalanceBucket?>(null);
            var bucket = BalanceBucket.Create("VAC", "Vacation", null, BalanceBucketUnit.Day, true, Now);
            typeof(BalanceBucket).GetProperty(nameof(BalanceBucket.Id))!.SetValue(bucket, id);
            return Task.FromResult<BalanceBucket?>(bucket);
        }
        public Task<WorkingCalendar?> GetWorkingCalendarAsync(Guid id, CancellationToken ct) => Task.FromResult<WorkingCalendar?>(null);
        public Task<OrgUnit?> GetOrgUnitAsync(Guid id, CancellationToken ct) => Task.FromResult<OrgUnit?>(id == unit.Id ? unit : null);
        public Task<List<OrgUnit>> ListOrgUnitsAsync(CancellationToken ct) => Task.FromResult(new List<OrgUnit> { unit });
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeApplicationEventOutbox : IApplicationEventOutbox
    {
        public List<(IApplicationEvent Event, Guid CorrelationId)> Messages { get; } = [];
        public Task EnqueueAsync(IApplicationEvent applicationEvent, Guid correlationId, CancellationToken cancellationToken)
        {
            if (!Messages.Any(x => x.Event.GetType() == applicationEvent.GetType() && x.CorrelationId == correlationId))
                Messages.Add((applicationEvent, correlationId));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAuditWriter : IAuditWriter
    {
        public List<AuditEventData> Events { get; } = [];
        public Task WriteAsync(AuditEventData auditEvent, CancellationToken cancellationToken)
        {
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedCurrentActor(Guid userId) : ICurrentActor { public Guid? UserId => userId; }
    private sealed class FixedTimeProvider(DateTime now) : TimeProvider { public override DateTimeOffset GetUtcNow() => new(now); }
    private sealed class NoopTransaction : IDisposable { public void Dispose() { } }
}
