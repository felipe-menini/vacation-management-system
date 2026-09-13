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

public sealed class LeaveReportingEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Guid SupportId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid DevelopmentId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task RequiresActorAndReportsPermission()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await CreateClient(null, []).GetAsync("/api/reports/leave-summary?from=2026-09-01&to=2026-09-30")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await CreateClient(Guid.NewGuid(), []).GetAsync("/api/reports/leave-summary?from=2026-09-01&to=2026-09-30")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await CreateClient(Guid.NewGuid(), [PermissionCodes.OrgUnitsRead]).GetAsync("/api/reports/leave-summary?from=2026-09-01&to=2026-09-30")).StatusCode);
    }

    [Fact]
    public async Task TechnicalAdminHasNoAccessSolelyFromTechnicalRole()
    {
        using var client = CreateClient(Guid.NewGuid(), [PermissionCodes.OrgUnitsRead]);

        using var response = await client.GetAsync("/api/reports/leave-summary?from=2026-09-01&to=2026-09-30");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ValidatesDateRange()
    {
        using var client = CreateClient(Guid.NewGuid(), [PermissionCodes.LeaveReportsRead]);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/reports/leave-summary?from=2026-09-30&to=2026-09-01")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/reports/leave-summary?from=2026-01-01&to=2027-01-02")).StatusCode);
    }

    [Fact]
    public async Task ReturnsAggregatePrivacyConsciousDto()
    {
        using var client = CreateClient(Guid.NewGuid(), [PermissionCodes.LeaveReportsRead]);

        using var response = await client.GetAsync("/api/reports/leave-summary?from=2026-09-01&to=2026-09-30");
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, json.RootElement.GetProperty("currentWorkload").GetProperty("pendingApprovalCount").GetInt32());
        Assert.Equal(2, json.RootElement.GetProperty("period").GetProperty("approvedOrEffectiveAbsenceCount").GetInt32());
        Assert.True(json.RootElement.TryGetProperty("orgUnitBreakdown", out _));
        Assert.DoesNotContain("leaveType", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("comment", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("reason", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("document", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("medical", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("requestId", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("employeeId", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExplicitOutOfScopeOrgFilterIsForbidden()
    {
        using var client = CreateClient(Guid.NewGuid(), [PermissionCodes.LeaveReportsRead], allowedOrgUnitIds: [SupportId]);

        using var response = await client.GetAsync($"/api/reports/leave-summary?from=2026-09-01&to=2026-09-30&orgUnitId={DevelopmentId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ScopedManagerOnlySeesAuthorizedOrganization()
    {
        using var client = CreateClient(Guid.NewGuid(), [PermissionCodes.LeaveReportsRead], allowedOrgUnitIds: [SupportId]);

        var result = await client.GetFromJsonAsync<LeaveSummaryReportDto>("/api/reports/leave-summary?from=2026-09-01&to=2026-09-30");

        Assert.NotNull(result);
        Assert.Equal([SupportId], result.OrgUnitBreakdown.Select(x => x.OrgUnitId));
    }

    [Fact]
    public async Task HrRootScopeSeesCompanyWideData()
    {
        using var client = CreateClient(Guid.NewGuid(), [PermissionCodes.LeaveReportsRead], allowedOrgUnitIds: [SupportId, DevelopmentId]);

        var result = await client.GetFromJsonAsync<LeaveSummaryReportDto>("/api/reports/leave-summary?from=2026-09-01&to=2026-09-30");

        Assert.NotNull(result);
        Assert.Equal(new HashSet<Guid> { SupportId, DevelopmentId }, result.OrgUnitBreakdown.Select(x => x.OrgUnitId).ToHashSet());
    }

    private HttpClient CreateClient(Guid? actorId, string[] permissions, IReadOnlyCollection<Guid>? allowedOrgUnitIds = null) => factory.WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.AddScoped<ICurrentActor>(_ => new FixedCurrentActor(actorId));
            services.AddScoped<IAuthorizationRepository>(_ => new FakeAuthorizationRepository(actorId, permissions, allowedOrgUnitIds ?? [SupportId, DevelopmentId]));
            services.AddScoped<ILeaveReportingReader>(_ => new FakeLeaveReportingReader());
        });
    }).CreateClient();

    private sealed class FixedCurrentActor(Guid? userId) : ICurrentActor { public Guid? UserId => userId; }

    private sealed class FakeLeaveReportingReader : ILeaveReportingReader
    {
        public Task<IReadOnlySet<Guid>?> ExpandOrgUnitScopeAsync(Guid rootOrgUnitId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlySet<Guid>?>(rootOrgUnitId == SupportId ? new HashSet<Guid> { SupportId } : rootOrgUnitId == DevelopmentId ? new HashSet<Guid> { DevelopmentId } : null);

        public Task<LeaveSummaryReportDto> QuerySummaryAsync(LeaveSummaryReportCriteria criteria, CancellationToken cancellationToken)
        {
            var breakdown = criteria.OrgUnitIds.Select(id => new LeaveReportOrgUnitBreakdownDto(id, id == SupportId ? "Support" : "Development", 1, 1)).ToList();
            return Task.FromResult(new LeaveSummaryReportDto(
                criteria.From,
                criteria.To,
                criteria.OrgUnitId,
                new(PendingApprovalCount: criteria.OrgUnitIds.Count, CancellationRequestedCount: 1),
                new(ApprovedAbsenceCount: 1, CompletedAbsenceCount: 1, CancellationRequestedAbsenceCount: 1, ApprovedOrEffectiveAbsenceCount: breakdown.Count, UniqueEmployeesWithApprovedOrCompletedAbsence: 1),
                breakdown));
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
