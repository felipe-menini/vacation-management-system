using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Licenses.Application.Authorization;
using Licenses.Application.LeaveManagement;
using Licenses.Domain.Authorization;
using Licenses.Domain.Identity;
using Licenses.Domain.Organization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Licenses.Api.Tests;

public sealed class LeaveCalendarEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Guid SupportId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid DevelopmentId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task RequiresActorAndCalendarPermission()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await CreateClient(null, []).GetAsync("/api/leave-calendar?from=2026-09-01&to=2026-09-30")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await CreateClient(Guid.NewGuid(), []).GetAsync("/api/leave-calendar?from=2026-09-01&to=2026-09-30")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await CreateClient(Guid.NewGuid(), [PermissionCodes.OrgUnitsRead]).GetAsync("/api/leave-calendar?from=2026-09-01&to=2026-09-30")).StatusCode);
    }

    [Fact]
    public async Task ValidatesDateRangeAndPagingBounds()
    {
        using var client = CreateClient(Guid.NewGuid(), [PermissionCodes.LeaveCalendarRead]);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/leave-calendar?from=2026-09-30&to=2026-09-01")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/leave-calendar?from=2026-01-01&to=2027-01-02")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/leave-calendar?from=2026-09-01&to=2026-09-30&pageSize=101")).StatusCode);
    }

    [Fact]
    public async Task ReturnsPrivacyConsciousPagedDto()
    {
        using var client = CreateClient(Guid.NewGuid(), [PermissionCodes.LeaveCalendarRead]);

        using var response = await client.GetAsync("/api/leave-calendar?from=2026-09-01&to=2026-09-30&page=1&pageSize=1");
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        var first = json.RootElement.GetProperty("items")[0];

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, json.RootElement.GetProperty("pageSize").GetInt32());
        Assert.Equal("HALF_DAY", first.GetProperty("dayPortion").GetString());
        Assert.False(first.TryGetProperty("comment", out _));
        Assert.False(first.TryGetProperty("leaveTypeName", out _));
        Assert.False(first.TryGetProperty("documents", out _));
        Assert.False(first.TryGetProperty("storageKey", out _));
        Assert.DoesNotContain("reason", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("medical", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExplicitOutOfScopeOrgFilterIsForbidden()
    {
        using var client = CreateClient(Guid.NewGuid(), [PermissionCodes.LeaveCalendarRead], allowedOrgUnitIds: [SupportId]);

        using var response = await client.GetAsync($"/api/leave-calendar?from=2026-09-01&to=2026-09-30&orgUnitId={DevelopmentId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private HttpClient CreateClient(Guid? actorId, string[] permissions, IReadOnlyCollection<Guid>? allowedOrgUnitIds = null) => factory.WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.AddScoped<ICurrentActor>(_ => new FixedCurrentActor(actorId));
            services.AddScoped<IAuthorizationRepository>(_ => new FakeAuthorizationRepository(actorId, permissions, allowedOrgUnitIds ?? [SupportId, DevelopmentId]));
            services.AddScoped<ILeaveCalendarReader>(_ => new FakeLeaveCalendarReader());
        });
    }).CreateClient();

    private sealed class FixedCurrentActor(Guid? userId) : ICurrentActor { public Guid? UserId => userId; }

    private sealed class FakeLeaveCalendarReader : ILeaveCalendarReader
    {
        public Task<IReadOnlySet<Guid>?> ExpandOrgUnitScopeAsync(Guid rootOrgUnitId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlySet<Guid>?>(rootOrgUnitId == SupportId ? new HashSet<Guid> { SupportId } : rootOrgUnitId == DevelopmentId ? new HashSet<Guid> { DevelopmentId } : null);

        public Task<LeaveCalendarPageDto> QueryAsync(LeaveCalendarQueryCriteria criteria, CancellationToken cancellationToken)
        {
            var entries = new List<LeaveCalendarEntryDto>
            {
                new(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), Guid.NewGuid(), "A User", SupportId, "Support", new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 10), "HALF_DAY", "APPROVED"),
                new(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), Guid.NewGuid(), "B User", DevelopmentId, "Development", new DateOnly(2026, 9, 11), new DateOnly(2026, 9, 12), "FULL_DAY", "CANCELLATION_REQUESTED")
            }.Where(x => criteria.OrgUnitIds.Contains(x.OrgUnitId)).ToList();
            return Task.FromResult(new LeaveCalendarPageDto(criteria.Page, criteria.PageSize, entries.Count, entries.Skip((criteria.Page - 1) * criteria.PageSize).Take(criteria.PageSize).ToList()));
        }
    }

    private sealed class FakeAuthorizationRepository(Guid? actorId, string[] permissions, IReadOnlyCollection<Guid> allowedOrgUnitIds) : IAuthorizationRepository
    {
        private readonly Guid _roleId = Guid.NewGuid();
        public Task<User?> GetUserAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult<User?>(actorId == userId ? User.Create("Actor", $"actor.{userId:N}@example.test", null, DateTime.UtcNow) : null);
        public Task<OrgUnit?> GetOrgUnitAsync(Guid orgUnitId, CancellationToken cancellationToken) => Task.FromResult<OrgUnit?>(OrgUnit.Create("Unit", $"U{Guid.NewGuid():N}"[..8], null, DateTime.UtcNow));
        public Task<List<OrgUnit>> ListOrgUnitsAsync(CancellationToken cancellationToken) => Task.FromResult(allowedOrgUnitIds.Select(id => { var unit = OrgUnit.Create("Unit", $"U{Guid.NewGuid():N}"[..8], null, DateTime.UtcNow); typeof(OrgUnit).GetProperty(nameof(OrgUnit.Id))!.SetValue(unit, id); return unit; }).ToList());
        public Task<List<UserOrgAssignment>> ListActiveUserOrgAssignmentsAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken) => Task.FromResult(new List<UserOrgAssignment>());
        public Task<List<RoleScopeAssignment>> ListActiveRoleScopeAssignmentsAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken) => Task.FromResult(actorId == userId ? allowedOrgUnitIds.Select(id => RoleScopeAssignment.Create(userId, _roleId, id, false, DateTime.UtcNow.AddDays(-1), null)).ToList() : []);
        public Task<bool> RoleHasPermissionAsync(Guid roleId, string permissionCode, CancellationToken cancellationToken) => Task.FromResult(roleId == _roleId && permissions.Contains(permissionCode));
        public Task<bool> IsRoleActiveAsync(Guid roleId, CancellationToken cancellationToken) => Task.FromResult(roleId == _roleId);
        public Task<List<User>> ListUsersInOrgUnitsAsync(IReadOnlyCollection<Guid> orgUnitIds, DateTime utcNow, CancellationToken cancellationToken) => Task.FromResult(new List<User>());
        public Task<List<DevelopmentActorDto>> ListDevelopmentActorsAsync(DateTime utcNow, CancellationToken cancellationToken) => Task.FromResult(new List<DevelopmentActorDto>());
    }
}
