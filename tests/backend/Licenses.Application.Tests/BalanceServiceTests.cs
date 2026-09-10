using Licenses.Application.Authorization;
using Licenses.Application.LeaveManagement;
using Licenses.Domain.Authorization;
using Licenses.Domain.Identity;
using Licenses.Domain.LeaveManagement;
using Licenses.Domain.Organization;

namespace Licenses.Application.Tests;

public sealed class BalanceServiceTests
{
    [Fact]
    public async Task LedgerOperationsDeriveAvailableAndReservedBalances()
    {
        var actor = Guid.NewGuid();
        var user = Guid.NewGuid();
        var bucket = Guid.NewGuid();
        var repo = new FakeBalanceRepository(user, bucket);
        var service = CreateService(repo, actor, [PermissionCodes.LeaveBalancesManage]);

        Assert.Equal(10m, (await service.GrantAsync(Command(user, bucket, 10m), CancellationToken.None))!.Snapshot.Available);
        var reserve = await service.ReserveAsync(Command(user, bucket, 4.5m), CancellationToken.None);
        Assert.Equal(5.5m, reserve.Snapshot.Available);
        Assert.Equal(4.5m, reserve.Snapshot.Reserved);
        var release = await service.ReleaseAsync(Command(user, bucket, 1.5m), CancellationToken.None);
        Assert.Equal(7m, release.Snapshot.Available);
        Assert.Equal(3m, release.Snapshot.Reserved);
        var consume = await service.ConsumeAsync(Command(user, bucket, 2m), CancellationToken.None);
        Assert.Equal(7m, consume.Snapshot.Available);
        Assert.Equal(1m, consume.Snapshot.Reserved);
        Assert.Equal(8m, (await service.RefundAsync(Command(user, bucket, 1m), CancellationToken.None)).Snapshot.Available);
        Assert.Equal(10m, (await service.AdjustAsync(Command(user, bucket, 2m), CancellationToken.None))!.Snapshot.Available);
        Assert.Equal(9m, (await service.AdjustAsync(Command(user, bucket, -1m), CancellationToken.None))!.Snapshot.Available);
        Assert.Equal(6m, (await service.ExpireAsync(Command(user, bucket, 3m), CancellationToken.None))!.Snapshot.Available);
    }

    [Fact]
    public async Task RejectsInvalidAmountsAndOverdraws()
    {
        var actor = Guid.NewGuid();
        var user = Guid.NewGuid();
        var bucket = Guid.NewGuid();
        var service = CreateService(new FakeBalanceRepository(user, bucket), actor, [PermissionCodes.LeaveBalancesManage]);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GrantAsync(Command(user, bucket, 0m), CancellationToken.None)!);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.ReserveAsync(Command(user, bucket, -1m), CancellationToken.None));
        await service.GrantAsync(Command(user, bucket, 2m), CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReserveAsync(Command(user, bucket, 3m), CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReleaseAsync(Command(user, bucket, 1m), CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ConsumeAsync(Command(user, bucket, 1m), CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AdjustAsync(Command(user, bucket, -3m), CancellationToken.None)!);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExpireAsync(Command(user, bucket, 3m), CancellationToken.None)!);
    }

    [Fact]
    public async Task IdempotentRetryReturnsExistingEntryAndConflictIsRejected()
    {
        var actor = Guid.NewGuid();
        var user = Guid.NewGuid();
        var bucket = Guid.NewGuid();
        var service = CreateService(new FakeBalanceRepository(user, bucket), actor, [PermissionCodes.LeaveBalancesManage]);
        var operationId = Guid.NewGuid();
        var first = await service.GrantAsync(new(user, bucket, operationId, 5m, "same"), CancellationToken.None);
        var second = await service.GrantAsync(new(user, bucket, operationId, 5m, "same"), CancellationToken.None);

        Assert.False(first!.WasAlreadyApplied);
        Assert.True(second!.WasAlreadyApplied);
        Assert.Equal(5m, second.Snapshot.Available);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GrantAsync(new(user, bucket, operationId, 6m, "conflict"), CancellationToken.None)!);
    }

    [Fact]
    public async Task InactiveBucketRulesAllowHistoricalSettlementButBlockNewGrantAndReserve()
    {
        var actor = Guid.NewGuid();
        var user = Guid.NewGuid();
        var bucket = Guid.NewGuid();
        var repo = new FakeBalanceRepository(user, bucket) { IsBucketActive = false, HasAccount = true };
        repo.SetBalances(5m, 3m);
        var service = CreateService(repo, actor, [PermissionCodes.LeaveBalancesManage]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GrantAsync(Command(user, bucket, 1m), CancellationToken.None)!);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReserveAsync(Command(user, bucket, 1m), CancellationToken.None));
        Assert.NotNull(await service.ReleaseAsync(Command(user, bucket, 1m), CancellationToken.None));
        Assert.NotNull(await service.ConsumeAsync(Command(user, bucket, 1m), CancellationToken.None));
        Assert.NotNull(await service.RefundAsync(Command(user, bucket, 1m), CancellationToken.None));
        Assert.NotNull(await service.AdjustAsync(Command(user, bucket, 1m), CancellationToken.None));
        Assert.NotNull(await service.ExpireAsync(Command(user, bucket, 1m), CancellationToken.None));
    }

    private static BalanceMutationCommand Command(Guid user, Guid bucket, decimal amount) => new(user, bucket, Guid.NewGuid(), amount, "Test reason");

    private static BalanceService CreateService(IBalanceRepository repo, Guid actorId, string[] permissions)
    {
        var auth = new AuthorizationService(new FakeAuthorizationRepository(actorId, permissions), TimeProvider.System);
        return new BalanceService(repo, auth, new FixedCurrentActor(actorId), TimeProvider.System);
    }

    private sealed class FixedCurrentActor(Guid userId) : ICurrentActor { public Guid? UserId => userId; }

    private sealed class FakeAuthorizationRepository(Guid actorId, string[] permissionCodes) : IAuthorizationRepository
    {
        private readonly Guid _roleId = Guid.NewGuid();
        public Task<User?> GetUserAsync(Guid userId, CancellationToken cancellationToken) { var user = User.Create("Actor", "actor.balance@example.test", null, DateTime.UtcNow); typeof(User).GetProperty(nameof(User.Id))!.SetValue(user, userId); return Task.FromResult<User?>(user); }
        public Task<OrgUnit?> GetOrgUnitAsync(Guid orgUnitId, CancellationToken cancellationToken) => Task.FromResult<OrgUnit?>(null);
        public Task<List<OrgUnit>> ListOrgUnitsAsync(CancellationToken cancellationToken) { var org = OrgUnit.Create("Scope", "SCOPE", null, DateTime.UtcNow); typeof(OrgUnit).GetProperty(nameof(OrgUnit.Id))!.SetValue(org, Guid.Parse("11111111-1111-1111-1111-111111111111")); return Task.FromResult(new List<OrgUnit> { org }); }
        public Task<List<UserOrgAssignment>> ListActiveUserOrgAssignmentsAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken) => Task.FromResult(new List<UserOrgAssignment> { UserOrgAssignment.Create(userId, Guid.Parse("11111111-1111-1111-1111-111111111111"), true, DateTime.UtcNow.AddDays(-1), null) });
        public Task<List<RoleScopeAssignment>> ListActiveRoleScopeAssignmentsAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken) => Task.FromResult(userId == actorId ? new List<RoleScopeAssignment> { RoleScopeAssignment.Create(actorId, _roleId, Guid.Parse("11111111-1111-1111-1111-111111111111"), true, DateTime.UtcNow.AddDays(-1), null) } : []);
        public Task<bool> RoleHasPermissionAsync(Guid roleId, string permissionCode, CancellationToken cancellationToken) => Task.FromResult(roleId == _roleId && permissionCodes.Contains(permissionCode));
        public Task<bool> IsRoleActiveAsync(Guid roleId, CancellationToken cancellationToken) => Task.FromResult(roleId == _roleId);
        public Task<List<User>> ListUsersInOrgUnitsAsync(IReadOnlyCollection<Guid> orgUnitIds, DateTime utcNow, CancellationToken cancellationToken) => Task.FromResult(new List<User>());
        public Task<List<DevelopmentActorDto>> ListDevelopmentActorsAsync(DateTime utcNow, CancellationToken cancellationToken) => Task.FromResult(new List<DevelopmentActorDto>());
    }

    private sealed class FakeBalanceRepository(Guid userId, Guid bucketId) : IBalanceRepository
    {
        private readonly Dictionary<Guid, (Guid UserId, Guid BucketId, BalanceLedgerEntryType Type, decimal Available, decimal Reserved, string Reason, Guid EntryId)> _operations = [];
        private decimal _available;
        private decimal _reserved;
        public bool IsBucketActive { get; set; } = true;
        public bool HasAccount { get; set; }
        public void SetBalances(decimal available, decimal reserved) { _available = available; _reserved = reserved; }
        public Task<User?> GetUserAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<User?>(null);
        public Task<IReadOnlyList<BalanceSnapshotRecord>> ListSnapshotsAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<BalanceSnapshotRecord>>([Snapshot()]);
        public Task<IReadOnlyList<BalanceLedgerEntryRecord>?> ListLedgerAsync(Guid id, Guid b, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<BalanceLedgerEntryRecord>?>([]);
        public Task<BalanceMutationRecord> MutateAsync(Guid id, Guid b, Guid op, BalanceLedgerEntryType type, decimal amount, string reason, Guid? by, DateTime at, CancellationToken ct)
        {
            var (ad, rd) = BalanceLedgerEntry.GetDeltas(type, amount);
            if (_operations.TryGetValue(op, out var existing))
            {
                if (existing.UserId != id || existing.BucketId != b || existing.Type != type || existing.Available != ad || existing.Reserved != rd || existing.Reason != reason) throw new InvalidOperationException("OperationId was already used for a different balance mutation.");
                return Task.FromResult(new BalanceMutationRecord(existing.EntryId, op, Snapshot(), true));
            }
            if ((type is BalanceLedgerEntryType.Grant or BalanceLedgerEntryType.Reserve) && !IsBucketActive) throw new InvalidOperationException("Inactive balance buckets cannot receive new grant or reserve operations.");
            if (!HasAccount && !IsBucketActive) throw new InvalidOperationException("Inactive balance buckets cannot receive new balance accounts.");
            HasAccount = true;
            if (_available + ad < 0 || _reserved + rd < 0) throw new InvalidOperationException("Balance cannot become negative.");
            _available += ad; _reserved += rd;
            var entryId = Guid.NewGuid();
            _operations[op] = (id, b, type, ad, rd, reason, entryId);
            return Task.FromResult(new BalanceMutationRecord(entryId, op, Snapshot(), false));
        }
        private BalanceSnapshotRecord Snapshot() => new(userId, bucketId, "VACATION_DAYS", "Vacation Days", BalanceBucketUnit.Day, _available, _reserved);
    }
}

