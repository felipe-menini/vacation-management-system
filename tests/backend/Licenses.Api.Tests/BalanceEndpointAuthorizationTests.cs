using System.Net;
using System.Net.Http.Json;
using Licenses.Application.Authorization;
using Licenses.Application.LeaveManagement;
using Licenses.Domain.Authorization;
using Licenses.Domain.Identity;
using Licenses.Domain.LeaveManagement;
using Licenses.Domain.Organization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Licenses.Api.Tests;

public sealed class BalanceEndpointAuthorizationTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task SelfBalancesRequireActorAndPermission()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await CreateClient(null, []).GetAsync("/api/balances/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await CreateClient(Guid.NewGuid(), []).GetAsync("/api/balances/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await CreateClient(Guid.NewGuid(), [PermissionCodes.LeaveBalancesReadSelf]).GetAsync("/api/balances/me")).StatusCode);
    }

    [Fact]
    public async Task ScopedReadUsesNonDisclosureForOutOfScopeUser()
    {
        var actor = Guid.NewGuid();
        using var client = CreateClient(actor, [PermissionCodes.LeaveBalancesRead], inScope: false);
        using var response = await client.GetAsync($"/api/users/{Guid.NewGuid()}/balances");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ReadOnlyActorCannotMutateAndHrCanGrant()
    {
        var user = Guid.NewGuid();
        var bucket = Guid.NewGuid();
        using var readOnly = CreateClient(Guid.NewGuid(), [PermissionCodes.LeaveBalancesRead]);
        Assert.Equal(HttpStatusCode.Forbidden, (await readOnly.PostAsJsonAsync($"/api/users/{user}/balances/{bucket}/grant", Payload())).StatusCode);

        using var hr = CreateClient(Guid.NewGuid(), [PermissionCodes.LeaveBalancesManage]);
        Assert.Equal(HttpStatusCode.OK, (await hr.PostAsJsonAsync($"/api/users/{user}/balances/{bucket}/grant", Payload())).StatusCode);
    }

    [Theory]
    [InlineData("reserve")]
    [InlineData("release")]
    [InlineData("consume")]
    [InlineData("refund")]
    public async Task InternalBalanceOperationsAreNotPublicEndpoints(string operation)
    {
        using var client = CreateClient(Guid.NewGuid(), [PermissionCodes.LeaveBalancesManage]);
        using var response = await client.PostAsJsonAsync($"/api/users/{Guid.NewGuid()}/balances/{Guid.NewGuid()}/{operation}", Payload());
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private HttpClient CreateClient(Guid? actorId, string[] permissions, bool inScope = true) => factory.WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.AddScoped<ICurrentActor>(_ => new FixedCurrentActor(actorId));
            services.AddScoped<IAuthorizationRepository>(_ => new FakeAuthorizationRepository(actorId, permissions, inScope));
            services.AddScoped<IBalanceRepository>(_ => new FakeBalanceRepository());
        });
    }).CreateClient();

    private static object Payload() => new { operationId = Guid.NewGuid(), amount = 1m, reason = "Test reason" };
    private sealed class FixedCurrentActor(Guid? userId) : ICurrentActor { public Guid? UserId => userId; }

    private sealed class FakeAuthorizationRepository(Guid? actorId, string[] permissions, bool inScope) : IAuthorizationRepository
    {
        private static readonly Guid ScopeId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private readonly Guid _roleId = Guid.NewGuid();
        public Task<User?> GetUserAsync(Guid userId, CancellationToken cancellationToken) { if (actorId != userId) return Task.FromResult<User?>(null); var user = User.Create("Actor", "actor.balance.api@example.test", null, DateTime.UtcNow); typeof(User).GetProperty(nameof(User.Id))!.SetValue(user, userId); return Task.FromResult<User?>(user); }
        public Task<OrgUnit?> GetOrgUnitAsync(Guid orgUnitId, CancellationToken cancellationToken) => Task.FromResult<OrgUnit?>(null);
        public Task<List<OrgUnit>> ListOrgUnitsAsync(CancellationToken cancellationToken) { var org = OrgUnit.Create("Scope", "SCOPE", null, DateTime.UtcNow); typeof(OrgUnit).GetProperty(nameof(OrgUnit.Id))!.SetValue(org, ScopeId); return Task.FromResult(new List<OrgUnit> { org }); }
        public Task<List<UserOrgAssignment>> ListActiveUserOrgAssignmentsAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken) => Task.FromResult(inScope ? new List<UserOrgAssignment> { UserOrgAssignment.Create(userId, ScopeId, true, DateTime.UtcNow.AddDays(-1), null) } : []);
        public Task<List<RoleScopeAssignment>> ListActiveRoleScopeAssignmentsAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken) => Task.FromResult(actorId == userId ? new List<RoleScopeAssignment> { RoleScopeAssignment.Create(userId, _roleId, ScopeId, true, DateTime.UtcNow.AddDays(-1), null) } : []);
        public Task<bool> RoleHasPermissionAsync(Guid roleId, string permissionCode, CancellationToken cancellationToken) => Task.FromResult(roleId == _roleId && permissions.Contains(permissionCode));
        public Task<bool> IsRoleActiveAsync(Guid roleId, CancellationToken cancellationToken) => Task.FromResult(roleId == _roleId);
        public Task<List<User>> ListUsersInOrgUnitsAsync(IReadOnlyCollection<Guid> orgUnitIds, DateTime utcNow, CancellationToken cancellationToken) => Task.FromResult(new List<User>());
        public Task<List<DevelopmentActorDto>> ListDevelopmentActorsAsync(DateTime utcNow, CancellationToken cancellationToken) => Task.FromResult(new List<DevelopmentActorDto>());
    }

    private sealed class FakeBalanceRepository : IBalanceRepository
    {
        public Task<User?> GetUserAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult<User?>(null);
        public Task<IReadOnlyList<BalanceSnapshotRecord>> ListSnapshotsAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<BalanceSnapshotRecord>>([]);
        public Task<IReadOnlyList<BalanceLedgerEntryRecord>?> ListLedgerAsync(Guid userId, Guid balanceBucketId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<BalanceLedgerEntryRecord>?>([]);
        public Task<BalanceMutationRecord> MutateAsync(Guid userId, Guid balanceBucketId, Guid operationId, BalanceLedgerEntryType type, decimal amount, string reason, Guid? createdByUserId, DateTime createdAtUtc, BalanceMutationAuditContext? auditContext, CancellationToken cancellationToken)
        {
            var snapshot = new BalanceSnapshotRecord(userId, balanceBucketId, "VACATION_DAYS", "Vacation Days", BalanceBucketUnit.Day, amount, 0m);
            return Task.FromResult(new BalanceMutationRecord(Guid.NewGuid(), operationId, snapshot, false));
        }
    }
}
