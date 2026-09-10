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

public sealed class LeavePolicyEndpointAuthorizationTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Guid TestWorkingCalendarId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task LeavePoliciesGet_Returns401_WhenActorIsMissing()
    {
        using var client = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing")).CreateClient();
        using var response = await client.GetAsync("/api/leave-policies");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LeavePoliciesGet_Returns403_WhenActorHasNoReadPermission()
    {
        using var client = CreateClientWithPermissions([]);
        using var response = await client.GetAsync("/api/leave-policies");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task LeavePoliciesMutation_Returns403_WhenActorHasOnlyReadPermission()
    {
        using var client = CreateClientWithPermissions([PermissionCodes.LeavePoliciesRead]);
        using var response = await client.PostAsJsonAsync("/api/leave-policies", new { leaveTypeId = Guid.NewGuid(), orgUnitId = (Guid?)null, appliesToDescendants = false });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/leave-policies")]
    [InlineData("POST", "/api/leave-policies/11111111-1111-1111-1111-111111111111/versions")]
    [InlineData("PUT", "/api/leave-policy-versions/11111111-1111-1111-1111-111111111111")]
    [InlineData("POST", "/api/leave-policy-versions/11111111-1111-1111-1111-111111111111/publish")]
    public async Task LeavePoliciesMutationEndpoints_Return403_WhenActorHasOnlyReadPermission(string method, string path)
    {
        using var client = CreateClientWithPermissions([PermissionCodes.LeavePoliciesRead]);
        using var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = JsonContent.Create(new { leaveTypeId = Guid.NewGuid(), effectiveFrom = "2026-01-01", dayCountMode = "BUSINESS_DAYS", allowHalfDay = true, noticeDayCountMode = "CALENDAR_DAYS", overlapBehavior = "BLOCK", consumesBalance = false })
        };

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task LeavePoliciesReadEndpoints_Return200_WhenActorHasReadPermission()
    {
        var repository = new FakeLeavePolicyRepository();
        var leaveType = repository.AddLeaveType("VACATION", true);
        var policy = repository.AddPolicy(leaveType.Id, null, false, true);

        using var client = CreateClientWithPermissions([PermissionCodes.LeavePoliciesRead], repository: repository);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/leave-policies")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/leave-policies/{policy.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/leave-policies/{policy.Id}/versions")).StatusCode);
    }

    [Fact]
    public async Task LeavePoliciesMutation_Returns201_WhenActorHasManagePermission()
    {
        var repository = new FakeLeavePolicyRepository();
        var leaveType = repository.AddLeaveType("VACATION", true);
        using var client = CreateClientWithPermissions([PermissionCodes.LeavePoliciesManage], repository: repository);

        using var response = await client.PostAsJsonAsync("/api/leave-policies", new { leaveTypeId = leaveType.Id, orgUnitId = (Guid?)null, appliesToDescendants = false });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task ProductionIgnoresDevelopmentActorHeader()
    {
        using var client = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production")).CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/leave-policies");
        request.Headers.Add("X-Dev-User-Id", Guid.NewGuid().ToString());

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TechAdminWithoutPolicyManagePermission_CannotMutatePolicies()
    {
        using var client = CreateClientWithPermissions([], roleCode: "TECH_ADMIN");

        using var response = await client.PostAsJsonAsync("/api/leave-policies", new { leaveTypeId = Guid.NewGuid(), orgUnitId = (Guid?)null, appliesToDescendants = false });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PublishedVersionCannotBeMutatedThroughPutEndpoint()
    {
        var repository = new FakeLeavePolicyRepository();
        var leaveType = repository.AddLeaveType("VACATION", true);
        var policy = repository.AddPolicy(leaveType.Id, null, false, true);
        var version = repository.AddVersion(policy.Id, new DateOnly(2026, 1, 1), null);
        version.Publish(DateTime.UtcNow);
        using var client = CreateClientWithPermissions([PermissionCodes.LeavePoliciesManage], repository: repository);

        using var response = await client.PutAsJsonAsync($"/api/leave-policy-versions/{version.Id}", VersionPayload());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateVersionCreatesDraftAndPublishIsExplicit()
    {
        var repository = new FakeLeavePolicyRepository();
        var leaveType = repository.AddLeaveType("VACATION", true);
        var policy = repository.AddPolicy(leaveType.Id, null, false, true);
        using var client = CreateClientWithPermissions([PermissionCodes.LeavePoliciesManage], repository: repository);

        using var createResponse = await client.PostAsJsonAsync($"/api/leave-policies/{policy.Id}/versions", VersionPayload());
        var created = await createResponse.Content.ReadFromJsonAsync<LeavePolicyVersionDto>();

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.Equal("DRAFT", created!.Status);

        using var publishResponse = await client.PostAsync($"/api/leave-policy-versions/{created.Id}/publish", null);
        var published = await publishResponse.Content.ReadFromJsonAsync<LeavePolicyVersionDto>();

        Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);
        Assert.Equal("PUBLISHED", published!.Status);
    }

    [Fact]
    public async Task ResolveRequiresReadAndReturnsOnlyPublishedVersion()
    {
        var repository = new FakeLeavePolicyRepository();
        var leaveType = repository.AddLeaveType("VACATION", true);
        var policy = repository.AddPolicy(leaveType.Id, null, false, true);
        repository.AddVersion(policy.Id, new DateOnly(2026, 1, 1), null);
        repository.AddVersion(policy.Id, new DateOnly(2027, 1, 1), null).Publish(DateTime.UtcNow);
        using var client = CreateClientWithPermissions([PermissionCodes.LeavePoliciesRead], repository: repository);

        using var response = await client.GetAsync($"/api/leave-policies/resolve?leaveTypeId={leaveType.Id}&date=2027-01-01");
        var resolved = await response.Content.ReadFromJsonAsync<ResolveLeavePolicyResultDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(resolved!.Found);
        Assert.Equal("PUBLISHED", resolved.Version!.Status);
        Assert.Equal(new DateOnly(2027, 1, 1), resolved.Version.EffectiveFrom);
    }

    [Fact]
    public async Task ResolveReturnsClearNoPolicyResult()
    {
        using var client = CreateClientWithPermissions([PermissionCodes.LeavePoliciesRead], repository: new FakeLeavePolicyRepository());

        using var response = await client.GetAsync($"/api/leave-policies/resolve?leaveTypeId={Guid.NewGuid()}&date=2027-01-01");
        var resolved = await response.Content.ReadFromJsonAsync<ResolveLeavePolicyResultDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(resolved!.Found);
        Assert.False(string.IsNullOrWhiteSpace(resolved.Reason));
    }

    [Theory]
    [InlineData("invalid-effective-date")]
    [InlineData("invalid-balance-configuration")]
    [InlineData("inactive-leave-type-publication")]
    [InlineData("inactive-balance-bucket-publication")]
    [InlineData("overlapping-published-period")]
    public async Task LeavePolicyApiValidationFailures_Return400(string scenario)
    {
        var repository = new FakeLeavePolicyRepository();
        var leaveType = repository.AddLeaveType("VACATION", scenario != "inactive-leave-type-publication");
        var bucket = repository.AddBucket("VACATION_DAYS", scenario != "inactive-balance-bucket-publication");
        var policy = repository.AddPolicy(leaveType.Id, null, false, true);
        using var client = CreateClientWithPermissions([PermissionCodes.LeavePoliciesManage], repository: repository);

        HttpResponseMessage response = scenario switch
        {
            "invalid-effective-date" => await client.PostAsJsonAsync($"/api/leave-policies/{policy.Id}/versions", VersionPayload(effectiveFrom: new DateOnly(2026, 2, 1), effectiveTo: new DateOnly(2026, 1, 1))),
            "invalid-balance-configuration" => await client.PostAsJsonAsync($"/api/leave-policies/{policy.Id}/versions", VersionPayload(consumesBalance: true, balanceBucketId: null)),
            "inactive-leave-type-publication" => await PublishCreatedVersionAsync(client, policy.Id, VersionPayload()),
            "inactive-balance-bucket-publication" => await PublishCreatedVersionAsync(client, policy.Id, VersionPayload(consumesBalance: true, balanceBucketId: bucket.Id)),
            "overlapping-published-period" => await PublishOverlappingVersionAsync(client, policy.Id),
            _ => throw new InvalidOperationException()
        };

        using (response)
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    private HttpClient CreateClientWithPermissions(string[] permissionCodes, FakeLeavePolicyRepository? repository = null, string roleCode = "TEST")
    {
        var actorId = Guid.NewGuid();
        return factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.AddScoped<ICurrentActor>(_ => new FixedCurrentActor(actorId));
                services.AddScoped<IAuthorizationRepository>(_ => new FakeAuthorizationRepository(actorId, permissionCodes, roleCode));
                if (repository is not null) services.AddScoped<ILeavePolicyRepository>(_ => repository);
            });
        }).CreateClient();
    }

    private static object VersionPayload(DateOnly? effectiveFrom = null, DateOnly? effectiveTo = null, bool consumesBalance = false, Guid? balanceBucketId = null) =>
        new { effectiveFrom = effectiveFrom ?? new DateOnly(2026, 1, 1), effectiveTo, dayCountMode = "BUSINESS_DAYS", allowHalfDay = true, minimumNoticeDays = 7, noticeDayCountMode = "CALENDAR_DAYS", maximumRequestDays = 15, overlapBehavior = "BLOCK", consumesBalance, balanceBucketId, workingCalendarId = TestWorkingCalendarId };

    private static async Task<HttpResponseMessage> PublishCreatedVersionAsync(HttpClient client, Guid policyId, object payload)
    {
        using var create = await client.PostAsJsonAsync($"/api/leave-policies/{policyId}/versions", payload);
        var version = await create.Content.ReadFromJsonAsync<LeavePolicyVersionDto>();
        return await client.PostAsync($"/api/leave-policy-versions/{version!.Id}/publish", null);
    }

    private static async Task<HttpResponseMessage> PublishOverlappingVersionAsync(HttpClient client, Guid policyId)
    {
        using var first = await client.PostAsJsonAsync($"/api/leave-policies/{policyId}/versions", VersionPayload(effectiveFrom: new DateOnly(2026, 1, 1), effectiveTo: new DateOnly(2026, 12, 31)));
        var firstVersion = await first.Content.ReadFromJsonAsync<LeavePolicyVersionDto>();
        using var firstPublish = await client.PostAsync($"/api/leave-policy-versions/{firstVersion!.Id}/publish", null);
        return await PublishCreatedVersionAsync(client, policyId, VersionPayload(effectiveFrom: new DateOnly(2026, 6, 1)));
    }

    private sealed class FixedCurrentActor(Guid userId) : ICurrentActor { public Guid? UserId => userId; }

    private sealed class FakeAuthorizationRepository : IAuthorizationRepository
    {
        private readonly Guid _actorId;
        private readonly List<Permission> _permissions;
        private readonly Role _role;
        private readonly RoleScopeAssignment _assignment;
        private readonly User _actor;

        public FakeAuthorizationRepository(Guid actorId, IEnumerable<string> permissionCodes, string roleCode)
        {
            _actorId = actorId;
            _actor = User.Create("Actor", "actor.policy@example.test", null, DateTime.UtcNow);
            typeof(User).GetProperty(nameof(User.Id))!.SetValue(_actor, actorId);
            _role = Role.Create(roleCode, roleCode, "Test role", true, DateTime.UtcNow);
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

    private sealed class FakeLeavePolicyRepository : ILeavePolicyRepository
    {
        private readonly List<LeavePolicy> _policies = [];
        private readonly List<LeavePolicyVersion> _versions = [];
        private readonly List<LeaveType> _types = [];
        private readonly List<BalanceBucket> _buckets = [];
        private readonly List<OrgUnit> _orgs = [];
        private readonly List<WorkingCalendar> _calendars = [TestCalendar()];

        public LeaveType AddLeaveType(string code, bool active) { var x = LeaveType.Create(code, code, null, 0, active, DateTime.UtcNow); _types.Add(x); return x; }
        public BalanceBucket AddBucket(string code, bool active) { var x = BalanceBucket.Create(code, code, null, BalanceBucketUnit.Day, active, DateTime.UtcNow); _buckets.Add(x); return x; }
        public LeavePolicy AddPolicy(Guid leaveTypeId, Guid? orgUnitId, bool appliesToDescendants, bool isActive) { var x = LeavePolicy.Create(leaveTypeId, orgUnitId, appliesToDescendants, isActive, DateTime.UtcNow); _policies.Add(x); return x; }
        private static WorkingCalendar TestCalendar() { var x = WorkingCalendar.Create("TEST", "Test", null, true, StandardWeekdays(), DateTime.UtcNow); typeof(WorkingCalendar).GetProperty(nameof(WorkingCalendar.Id))!.SetValue(x, TestWorkingCalendarId); return x; }
        private static Dictionary<DayOfWeek, bool> StandardWeekdays() => new() { [DayOfWeek.Sunday] = false, [DayOfWeek.Monday] = true, [DayOfWeek.Tuesday] = true, [DayOfWeek.Wednesday] = true, [DayOfWeek.Thursday] = true, [DayOfWeek.Friday] = true, [DayOfWeek.Saturday] = false };
        public LeavePolicyVersion AddVersion(Guid policyId, DateOnly effectiveFrom, DateOnly? effectiveTo) { var x = LeavePolicyVersion.CreateDraft(policyId, GetNextVersionNumberAsync(policyId, CancellationToken.None).Result, effectiveFrom, effectiveTo, PolicyDayCountMode.BusinessDays, true, 7, PolicyDayCountMode.CalendarDays, 15, PolicyOverlapBehavior.Block, false, null, TestWorkingCalendarId, DateTime.UtcNow); _versions.Add(x); return x; }

        public Task<List<LeavePolicy>> ListPoliciesAsync(CancellationToken cancellationToken) => Task.FromResult(_policies.ToList());
        public Task<LeavePolicy?> GetPolicyAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(_policies.SingleOrDefault(x => x.Id == id));
        public Task<LeavePolicyVersion?> GetVersionAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(_versions.SingleOrDefault(x => x.Id == id));
        public Task<List<LeavePolicyVersion>> ListVersionsAsync(Guid policyId, CancellationToken cancellationToken) => Task.FromResult(_versions.Where(x => x.LeavePolicyId == policyId).OrderBy(x => x.VersionNumber).ToList());
        public Task<bool> ExactPolicyScopeExistsAsync(Guid leaveTypeId, Guid? orgUnitId, Guid? excludingPolicyId, CancellationToken cancellationToken) => Task.FromResult(_policies.Any(x => x.LeaveTypeId == leaveTypeId && x.OrgUnitId == orgUnitId && (excludingPolicyId is null || x.Id != excludingPolicyId)));
        public Task<bool> HasPublishedVersionsAsync(Guid policyId, CancellationToken cancellationToken) => Task.FromResult(_versions.Any(x => x.LeavePolicyId == policyId && x.Status == LeavePolicyVersionStatus.Published));
        public Task<int> GetNextVersionNumberAsync(Guid policyId, CancellationToken cancellationToken) => Task.FromResult(_versions.Where(x => x.LeavePolicyId == policyId).Select(x => (int?)x.VersionNumber).Max() is { } max ? max + 1 : 1);
        public Task<bool> HasOverlappingPublishedVersionAsync(Guid policyId, DateOnly effectiveFrom, DateOnly? effectiveTo, Guid? excludingVersionId, CancellationToken cancellationToken) => Task.FromResult(_versions.Any(x => x.LeavePolicyId == policyId && x.Status == LeavePolicyVersionStatus.Published && (excludingVersionId is null || x.Id != excludingVersionId) && x.EffectiveFrom <= (effectiveTo ?? DateOnly.MaxValue) && (x.EffectiveTo ?? DateOnly.MaxValue) >= effectiveFrom));
        public Task<List<LeavePolicy>> ListPoliciesForLeaveTypeWithPublishedVersionsAsync(Guid leaveTypeId, DateOnly date, CancellationToken cancellationToken) { foreach (var p in _policies) { p.Versions.Clear(); p.Versions.AddRange(_versions.Where(v => v.LeavePolicyId == p.Id && v.Status == LeavePolicyVersionStatus.Published && v.EffectiveFrom <= date && (v.EffectiveTo is null || v.EffectiveTo >= date))); } return Task.FromResult(_policies.Where(x => x.LeaveTypeId == leaveTypeId && x.IsActive && x.Versions.Count > 0).ToList()); }
        public Task AddPolicyAsync(LeavePolicy policy, CancellationToken cancellationToken) { _policies.Add(policy); return Task.CompletedTask; }
        public Task AddVersionAsync(LeavePolicyVersion version, CancellationToken cancellationToken) { _versions.Add(version); return Task.CompletedTask; }
        public Task<LeaveType?> GetLeaveTypeAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(_types.SingleOrDefault(x => x.Id == id));
        public Task<BalanceBucket?> GetBalanceBucketAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(_buckets.SingleOrDefault(x => x.Id == id));
        public Task<WorkingCalendar?> GetWorkingCalendarAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(_calendars.SingleOrDefault(x => x.Id == id));
        public Task<OrgUnit?> GetOrgUnitAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(_orgs.SingleOrDefault(x => x.Id == id));
        public Task<List<OrgUnit>> ListOrgUnitsAsync(CancellationToken cancellationToken) => Task.FromResult(_orgs.ToList());
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

