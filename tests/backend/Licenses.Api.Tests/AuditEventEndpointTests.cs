using System.Net;
using System.Net.Http.Json;
using Licenses.Application.Audit;
using Licenses.Application.Authorization;
using Licenses.Domain.Authorization;
using Licenses.Domain.Identity;
using Licenses.Domain.Organization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Licenses.Api.Tests;

public sealed class AuditEventEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Guid RootOrgUnitId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid ChildOrgUnitId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly Guid OtherOrgUnitId = Guid.Parse("10000000-0000-0000-0000-000000000003");

    [Fact]
    public async Task List_Returns401_WhenActorIsMissing()
    {
        using var client = CreateClient(null, [], new CapturingAuditEventReader());
        using var response = await client.GetAsync("/api/audit-events");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task List_Returns403_WhenActorLacksAuditReadPermission()
    {
        using var client = CreateClient(Guid.NewGuid(), [], new CapturingAuditEventReader());
        using var response = await client.GetAsync("/api/audit-events");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task List_PassesCompanyWideScopeAndFiltersToReader()
    {
        var actorId = Guid.NewGuid();
        var reader = new CapturingAuditEventReader();
        using var client = CreateClient(actorId, [PermissionCodes.AuditEventsRead], reader, scopeOrgUnitId: RootOrgUnitId, includeDescendants: true);

        var from = Uri.EscapeDataString("2026-09-01T00:00:00Z");
        var to = Uri.EscapeDataString("2026-09-11T00:00:00Z");
        using var response = await client.GetAsync($"/api/audit-events?fromUtc={from}&toUtc={to}&action=leave.document.read&resourceType=LeaveRequestDocument&actorUserId={actorId}&page=2&pageSize=250");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(reader.Query);
        Assert.Equal("leave.document.read", reader.Query!.Action);
        Assert.Equal("LeaveRequestDocument", reader.Query.ResourceType);
        Assert.Equal(actorId, reader.Query.ActorUserId);
        Assert.Equal(2, reader.Query.Page);
        Assert.Equal(250, reader.Query.PageSize);
        Assert.True(reader.CanReadGlobalEvents);
        Assert.Contains(RootOrgUnitId, reader.AuthorizedOrgUnitIds);
        Assert.Contains(ChildOrgUnitId, reader.AuthorizedOrgUnitIds);
    }

    [Fact]
    public async Task List_ScopedReaderCannotReadGlobalEventsUnlessRootScoped()
    {
        var reader = new CapturingAuditEventReader();
        using var client = CreateClient(Guid.NewGuid(), [PermissionCodes.AuditEventsRead], reader, scopeOrgUnitId: ChildOrgUnitId, includeDescendants: false);

        using var response = await client.GetAsync("/api/audit-events");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(reader.CanReadGlobalEvents);
        Assert.Equal([ChildOrgUnitId], reader.AuthorizedOrgUnitIds);
    }

    [Fact]
    public async Task List_TechAdminWithoutAuditReadIsForbidden()
    {
        using var client = CreateClient(Guid.NewGuid(), [PermissionCodes.OrgUnitsRead], new CapturingAuditEventReader());
        using var response = await client.GetAsync("/api/audit-events");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task MutationEndpoints_DoNotExist(string method)
    {
        using var client = CreateClient(Guid.NewGuid(), [PermissionCodes.AuditEventsRead], new CapturingAuditEventReader());
        using var request = new HttpRequestMessage(new HttpMethod(method), "/api/audit-events");
        if (method is "POST" or "PUT") request.Content = JsonContent.Create(new { });
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    private HttpClient CreateClient(Guid? actorId, string[] permissions, CapturingAuditEventReader reader, Guid? scopeOrgUnitId = null, bool includeDescendants = false) => factory.WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.AddScoped<ICurrentActor>(_ => new FixedCurrentActor(actorId));
            services.AddScoped<IAuthorizationRepository>(_ => new FakeAuthorizationRepository(actorId, permissions, scopeOrgUnitId ?? RootOrgUnitId, includeDescendants));
            services.AddScoped<IAuditEventReader>(_ => reader);
        });
    }).CreateClient();

    private sealed class FixedCurrentActor(Guid? userId) : ICurrentActor { public Guid? UserId => userId; }

    private sealed class CapturingAuditEventReader : IAuditEventReader
    {
        public AuditEventSearchQuery? Query { get; private set; }
        public IReadOnlySet<Guid> AuthorizedOrgUnitIds { get; private set; } = new HashSet<Guid>();
        public bool CanReadGlobalEvents { get; private set; }

        public Task<AuditEventListResult> SearchAsync(AuditEventSearchQuery query, IReadOnlySet<Guid> authorizedOrgUnitIds, bool canReadGlobalEvents, CancellationToken cancellationToken)
        {
            Query = query;
            AuthorizedOrgUnitIds = authorizedOrgUnitIds;
            CanReadGlobalEvents = canReadGlobalEvents;
            return Task.FromResult(new AuditEventListResult([], Math.Max(1, query.Page), Math.Clamp(query.PageSize, 1, 100), 0, false));
        }
    }

    private sealed class FakeAuthorizationRepository(Guid? actorId, string[] permissions, Guid scopeOrgUnitId, bool includeDescendants) : IAuthorizationRepository
    {
        private readonly Guid _roleId = Guid.NewGuid();
        public Task<User?> GetUserAsync(Guid userId, CancellationToken cancellationToken) { if (actorId != userId) return Task.FromResult<User?>(null); var user = User.Create("Actor", $"audit.{userId:N}@example.test", null, DateTime.UtcNow); typeof(User).GetProperty(nameof(User.Id))!.SetValue(user, userId); return Task.FromResult<User?>(user); }
        public Task<OrgUnit?> GetOrgUnitAsync(Guid orgUnitId, CancellationToken cancellationToken) => Task.FromResult<OrgUnit?>(ListOrgUnitsAsync(cancellationToken).Result.FirstOrDefault(x => x.Id == orgUnitId));
        public Task<List<OrgUnit>> ListOrgUnitsAsync(CancellationToken cancellationToken)
        {
            var root = OrgUnit.Create("Company", "COMPANY", null, DateTime.UtcNow); typeof(OrgUnit).GetProperty(nameof(OrgUnit.Id))!.SetValue(root, RootOrgUnitId);
            var child = OrgUnit.Create("Child", "CHILD", RootOrgUnitId, DateTime.UtcNow); typeof(OrgUnit).GetProperty(nameof(OrgUnit.Id))!.SetValue(child, ChildOrgUnitId);
            var other = OrgUnit.Create("Other", "OTHER", null, DateTime.UtcNow); typeof(OrgUnit).GetProperty(nameof(OrgUnit.Id))!.SetValue(other, OtherOrgUnitId);
            return Task.FromResult(new List<OrgUnit> { root, child, other });
        }
        public Task<List<UserOrgAssignment>> ListActiveUserOrgAssignmentsAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken) => Task.FromResult(new List<UserOrgAssignment>());
        public Task<List<RoleScopeAssignment>> ListActiveRoleScopeAssignmentsAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken) => Task.FromResult(actorId == userId ? new List<RoleScopeAssignment> { RoleScopeAssignment.Create(userId, _roleId, scopeOrgUnitId, includeDescendants, DateTime.UtcNow.AddDays(-1), null) } : []);
        public Task<bool> RoleHasPermissionAsync(Guid roleId, string permissionCode, CancellationToken cancellationToken) => Task.FromResult(roleId == _roleId && permissions.Contains(permissionCode));
        public Task<bool> IsRoleActiveAsync(Guid roleId, CancellationToken cancellationToken) => Task.FromResult(roleId == _roleId);
        public Task<List<User>> ListUsersInOrgUnitsAsync(IReadOnlyCollection<Guid> orgUnitIds, DateTime utcNow, CancellationToken cancellationToken) => Task.FromResult(new List<User>());
        public Task<List<DevelopmentActorDto>> ListDevelopmentActorsAsync(DateTime utcNow, CancellationToken cancellationToken) => Task.FromResult(new List<DevelopmentActorDto>());
    }
}
