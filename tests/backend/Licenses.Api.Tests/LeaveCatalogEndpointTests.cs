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

public sealed class LeaveCatalogEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task LeaveCatalogGet_Returns401_WhenActorIsMissing()
    {
        using var client = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing")).CreateClient();

        using var response = await client.GetAsync("/api/leave-types");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LeaveCatalogGet_Returns403_WhenKnownActorHasNoReadPermission()
    {
        using var client = CreateClientWithPermissions([]);

        using var response = await client.GetAsync("/api/leave-types");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task LeaveCatalogReadPermissionPermitsGet()
    {
        using var client = CreateClientWithPermissions([PermissionCodes.LeaveCatalogRead]);

        using var response = await client.GetAsync("/api/leave-types");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ReadOnlyActorCannotPostOrPut()
    {
        using var client = CreateClientWithPermissions([PermissionCodes.LeaveCatalogRead]);

        using var post = await client.PostAsJsonAsync("/api/leave-types", new CreateLeaveTypeCommand("VACATION", "Vacation", null, null, 0));
        using var put = await client.PutAsJsonAsync($"/api/balance-buckets/{Guid.NewGuid()}", new UpdateBalanceBucketCommand("Vacation Days", null, "DAY", true));

        Assert.Equal(HttpStatusCode.Forbidden, post.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
    }

    [Fact]
    public async Task ManagePermissionPermitsPostAndPut()
    {
        using var client = CreateClientWithPermissions([PermissionCodes.LeaveCatalogRead, PermissionCodes.LeaveCatalogManage]);

        using var createLeaveType = await client.PostAsJsonAsync("/api/leave-types", new CreateLeaveTypeCommand("medical", "Medical Leave", null, null, 1));
        var leaveType = await createLeaveType.Content.ReadFromJsonAsync<LeaveTypeDto>();
        using var updateLeaveType = await client.PutAsJsonAsync($"/api/leave-types/{leaveType!.Id}", new UpdateLeaveTypeCommand("Medical", "Updated", false, 2));

        using var createBucket = await client.PostAsJsonAsync("/api/balance-buckets", new CreateBalanceBucketCommand("medical_exam_days", "Medical Examination Days", null, "DAY", null));
        var bucket = await createBucket.Content.ReadFromJsonAsync<BalanceBucketDto>();
        using var updateBucket = await client.PutAsJsonAsync($"/api/balance-buckets/{bucket!.Id}", new UpdateBalanceBucketCommand("Medical Exam Days", null, "DAY", false));

        Assert.Equal(HttpStatusCode.Created, createLeaveType.StatusCode);
        Assert.Equal(HttpStatusCode.OK, updateLeaveType.StatusCode);
        Assert.Equal(HttpStatusCode.Created, createBucket.StatusCode);
        Assert.Equal(HttpStatusCode.OK, updateBucket.StatusCode);
    }

    private HttpClient CreateClientWithPermissions(string[] permissionCodes)
    {
        var actorId = Guid.NewGuid();
        var catalogRepository = new FakeLeaveCatalogRepository();
        return factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.AddScoped<ICurrentActor>(_ => new FixedCurrentActor(actorId));
                services.AddScoped<IAuthorizationRepository>(_ => new FakeAuthorizationRepository(actorId, permissionCodes));
                services.AddSingleton<ILeaveCatalogRepository>(catalogRepository);
            });
        }).CreateClient();
    }

    private sealed class FixedCurrentActor(Guid userId) : ICurrentActor
    {
        public Guid? UserId => userId;
    }

    private sealed class FakeAuthorizationRepository : IAuthorizationRepository
    {
        private readonly Guid _actorId;
        private readonly List<Permission> _permissions;
        private readonly Role _role;
        private readonly RoleScopeAssignment _assignment;
        private readonly User _actor;

        public FakeAuthorizationRepository(Guid actorId, IEnumerable<string> permissionCodes)
        {
            _actorId = actorId;
            _actor = User.Create("Actor", "actor@example.test", null, DateTime.UtcNow);
            typeof(User).GetProperty(nameof(User.Id))!.SetValue(_actor, actorId);
            _role = Role.Create("TEST", "Test", "Test role", true, DateTime.UtcNow);
            _permissions = permissionCodes.Select(x => Permission.Create(x, x)).ToList();
            _assignment = RoleScopeAssignment.Create(actorId, _role.Id, Guid.NewGuid(), false, DateTime.UtcNow.AddDays(-1), null);
        }

        public Task<User?> GetUserAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult<User?>(userId == _actorId ? _actor : null);
        public Task<OrgUnit?> GetOrgUnitAsync(Guid orgUnitId, CancellationToken cancellationToken) => Task.FromResult<OrgUnit?>(null);
        public Task<List<OrgUnit>> ListOrgUnitsAsync(CancellationToken cancellationToken) => Task.FromResult(new List<OrgUnit>());
        public Task<List<UserOrgAssignment>> ListActiveUserOrgAssignmentsAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken) => Task.FromResult(new List<UserOrgAssignment>());
        public Task<List<RoleScopeAssignment>> ListActiveRoleScopeAssignmentsAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken) => Task.FromResult(userId == _actorId ? [_assignment] : new List<RoleScopeAssignment>());
        public Task<bool> RoleHasPermissionAsync(Guid roleId, string permissionCode, CancellationToken cancellationToken) => Task.FromResult(roleId == _role.Id && _permissions.Any(x => x.Code == permissionCode));
        public Task<bool> IsRoleActiveAsync(Guid roleId, CancellationToken cancellationToken) => Task.FromResult(roleId == _role.Id && _role.IsActive);
        public Task<List<User>> ListUsersInOrgUnitsAsync(IReadOnlyCollection<Guid> orgUnitIds, DateTime utcNow, CancellationToken cancellationToken) => Task.FromResult(new List<User>());
        public Task<List<DevelopmentActorDto>> ListDevelopmentActorsAsync(DateTime utcNow, CancellationToken cancellationToken) => Task.FromResult(new List<DevelopmentActorDto>());
    }

    private sealed class FakeLeaveCatalogRepository : ILeaveCatalogRepository
    {
        private readonly List<LeaveType> _leaveTypes = [];
        private readonly List<BalanceBucket> _balanceBuckets = [];

        public Task<List<LeaveType>> ListLeaveTypesAsync(bool? isActive, CancellationToken cancellationToken) => Task.FromResult(_leaveTypes.Where(x => isActive is null || x.IsActive == isActive).OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToList());
        public Task<LeaveType?> GetLeaveTypeAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(_leaveTypes.SingleOrDefault(x => x.Id == id));
        public Task<bool> LeaveTypeCodeExistsAsync(string code, Guid? excludingId, CancellationToken cancellationToken) => Task.FromResult(_leaveTypes.Any(x => x.Code == code && (excludingId is null || x.Id != excludingId)));
        public Task AddLeaveTypeAsync(LeaveType leaveType, CancellationToken cancellationToken) { _leaveTypes.Add(leaveType); return Task.CompletedTask; }
        public Task<List<BalanceBucket>> ListBalanceBucketsAsync(bool? isActive, CancellationToken cancellationToken) => Task.FromResult(_balanceBuckets.Where(x => isActive is null || x.IsActive == isActive).OrderBy(x => x.Name).ToList());
        public Task<BalanceBucket?> GetBalanceBucketAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(_balanceBuckets.SingleOrDefault(x => x.Id == id));
        public Task<bool> BalanceBucketCodeExistsAsync(string code, Guid? excludingId, CancellationToken cancellationToken) => Task.FromResult(_balanceBuckets.Any(x => x.Code == code && (excludingId is null || x.Id != excludingId)));
        public Task AddBalanceBucketAsync(BalanceBucket balanceBucket, CancellationToken cancellationToken) { _balanceBuckets.Add(balanceBucket); return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
