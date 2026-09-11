using Licenses.Application.Authorization;
using Licenses.Application.LeaveManagement;
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
    }

    [Fact]
    public async Task SelfDecisionAndOperationIdConflictsAreRejected()
    {
        var setup = TestSetup.Create(consumesBalance: false);
        var operationId = Guid.NewGuid();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => setup.CreateService(setup.Employee.Id, PermissionCodes.LeaveRequestsDecide).ApproveAsync(setup.Request.Id, new(operationId, null), CancellationToken.None)!);

        var service = setup.CreateService(setup.Approver.Id, PermissionCodes.LeaveRequestsDecide);
        await service.ApproveAsync(setup.Request.Id, new(operationId, null), CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RejectAsync(setup.Request.Id, new(operationId, "Changed"), CancellationToken.None)!);
    }

    private sealed record TestSetup(User Employee, User Approver, OrgUnit Unit, LeaveType Type, LeavePolicy Policy, LeavePolicyVersion Version, LeaveRequest Request, FakeLeaveRequestRepository Requests, FakeBalanceRepository Balances)
    {
        public static TestSetup Create(bool consumesBalance)
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
            request.EnsureReservationOperationId();
            request.Submit(version.Id, 2m, consumesBalance ? Guid.NewGuid() : null, consumesBalance ? request.BalanceReservationOperationId : null, Now);
            var balances = new FakeBalanceRepository(bucketId);
            var requests = new FakeLeaveRequestRepository(employee, approver, unit, type, policy, version, request);
            return new(employee, approver, unit, type, policy, version, request, requests, balances);
        }

        public LeaveRequestService CreateService(Guid actorId, params string[] permissions)
        {
            var auth = new AuthorizationService(new FakeAuthorizationRepository(actorId, Unit, Employee, Approver, permissions), new FixedTimeProvider(Now));
            var balance = new BalanceService(Balances, auth, new FixedCurrentActor(actorId), new FixedTimeProvider(Now));
            return new LeaveRequestService(Requests, new FakePolicyRepository(), balance, auth, new FixedCurrentActor(actorId), new FixedTimeProvider(Now));
        }
    }

    private sealed class FakeLeaveRequestRepository(User employee, User approver, OrgUnit unit, LeaveType type, LeavePolicy policy, LeavePolicyVersion version, LeaveRequest request) : ILeaveRequestRepository
    {
        private readonly List<LeaveRequestDecision> _decisions = [];
        public Task<IReadOnlyList<LeaveRequest>> ListByUserAsync(Guid userId, CancellationToken ct) => Task.FromResult<IReadOnlyList<LeaveRequest>>([request]);
        public Task<IReadOnlyList<LeaveRequest>> ListByUsersAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => Task.FromResult<IReadOnlyList<LeaveRequest>>([request]);
        public Task<IReadOnlyList<LeaveRequest>> ListPendingByOrgUnitsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => Task.FromResult<IReadOnlyList<LeaveRequest>>(request.Status == LeaveRequestStatus.PendingApproval ? [request] : []);
        public Task<LeaveRequest?> GetAsync(Guid id, bool tracking, CancellationToken ct) => Task.FromResult(id == request.Id ? request : null);
        public Task<LeaveRequest?> GetForUpdateAsync(Guid id, CancellationToken ct) => GetAsync(id, true, ct);
        public Task<LeaveRequestDecision?> GetDecisionByOperationIdAsync(Guid operationId, CancellationToken ct) => Task.FromResult(_decisions.SingleOrDefault(x => x.OperationId == operationId));
        public Task<LeaveRequestDecision?> GetDecisionByRequestIdAsync(Guid requestId, CancellationToken ct) => Task.FromResult(_decisions.SingleOrDefault(x => x.LeaveRequestId == requestId));
        public Task<IReadOnlyList<LeaveRequestDecision>> ListDecisionsByRequestIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => Task.FromResult<IReadOnlyList<LeaveRequestDecision>>(_decisions.Where(x => ids.Contains(x.LeaveRequestId)).ToList());
        public Task AddAsync(LeaveRequest r, CancellationToken ct) => Task.CompletedTask;
        public Task AddDecisionAsync(LeaveRequestDecision decision, CancellationToken ct) { _decisions.Add(decision); return Task.CompletedTask; }
        public Task<User?> GetUserAsync(Guid id, CancellationToken ct) => Task.FromResult<User?>(id == employee.Id ? employee : id == approver.Id ? approver : null);
        public Task<OrgUnit?> GetOrgUnitAsync(Guid id, CancellationToken ct) => Task.FromResult<OrgUnit?>(id == unit.Id ? unit : null);
        public Task<LeaveType?> GetLeaveTypeAsync(Guid id, CancellationToken ct) => Task.FromResult<LeaveType?>(id == type.Id ? type : null);
        public Task<LeavePolicy?> GetPolicyAsync(Guid id, CancellationToken ct) => Task.FromResult<LeavePolicy?>(policy);
        public Task<LeavePolicyVersion?> GetPolicyVersionAsync(Guid id, CancellationToken ct) => Task.FromResult<LeavePolicyVersion?>(id == version.Id ? version : null);
        public Task<WorkingCalendar?> GetWorkingCalendarAsync(Guid id, CancellationToken ct) => Task.FromResult<WorkingCalendar?>(null);
        public Task<Guid?> GetBalanceAccountIdAsync(Guid userId, Guid balanceBucketId, CancellationToken ct) => Task.FromResult<Guid?>(request.BalanceAccountId);
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
        public Task<BalanceMutationRecord> MutateAsync(Guid userId, Guid b, Guid op, BalanceLedgerEntryType type, decimal amount, string reason, Guid? by, DateTime at, CancellationToken ct)
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

    private sealed class FakePolicyRepository : ILeavePolicyRepository
    {
        public Task<List<LeavePolicy>> ListPoliciesAsync(CancellationToken ct) => Task.FromResult(new List<LeavePolicy>());
        public Task<LeavePolicy?> GetPolicyAsync(Guid id, CancellationToken ct) => Task.FromResult<LeavePolicy?>(null);
        public Task<LeavePolicyVersion?> GetVersionAsync(Guid id, CancellationToken ct) => Task.FromResult<LeavePolicyVersion?>(null);
        public Task<List<LeavePolicyVersion>> ListVersionsAsync(Guid policyId, CancellationToken ct) => Task.FromResult(new List<LeavePolicyVersion>());
        public Task<bool> ExactPolicyScopeExistsAsync(Guid leaveTypeId, Guid? orgUnitId, Guid? excludingPolicyId, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> HasPublishedVersionsAsync(Guid policyId, CancellationToken ct) => Task.FromResult(false);
        public Task<int> GetNextVersionNumberAsync(Guid policyId, CancellationToken ct) => Task.FromResult(1);
        public Task<bool> HasOverlappingPublishedVersionAsync(Guid policyId, DateOnly effectiveFrom, DateOnly? effectiveTo, Guid? excludingVersionId, CancellationToken ct) => Task.FromResult(false);
        public Task<List<LeavePolicy>> ListPoliciesForLeaveTypeWithPublishedVersionsAsync(Guid leaveTypeId, DateOnly date, CancellationToken ct) => Task.FromResult(new List<LeavePolicy>());
        public Task AddPolicyAsync(LeavePolicy policy, CancellationToken ct) => Task.CompletedTask;
        public Task AddVersionAsync(LeavePolicyVersion version, CancellationToken ct) => Task.CompletedTask;
        public Task<LeaveType?> GetLeaveTypeAsync(Guid id, CancellationToken ct) => Task.FromResult<LeaveType?>(null);
        public Task<BalanceBucket?> GetBalanceBucketAsync(Guid id, CancellationToken ct) => Task.FromResult<BalanceBucket?>(null);
        public Task<WorkingCalendar?> GetWorkingCalendarAsync(Guid id, CancellationToken ct) => Task.FromResult<WorkingCalendar?>(null);
        public Task<OrgUnit?> GetOrgUnitAsync(Guid id, CancellationToken ct) => Task.FromResult<OrgUnit?>(null);
        public Task<List<OrgUnit>> ListOrgUnitsAsync(CancellationToken ct) => Task.FromResult(new List<OrgUnit>());
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FixedCurrentActor(Guid userId) : ICurrentActor { public Guid? UserId => userId; }
    private sealed class FixedTimeProvider(DateTime now) : TimeProvider { public override DateTimeOffset GetUtcNow() => new(now); }
    private sealed class NoopTransaction : IDisposable { public void Dispose() { } }
}
