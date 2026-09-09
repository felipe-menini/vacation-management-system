using System.Net;
using Licenses.Api.Development;
using Licenses.Application.Authorization;
using Licenses.Domain.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Licenses.Api.Tests;

public sealed class AuthorizationEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task OrganizationEndpoint_Returns401_WhenActorIsMissing()
    {
        using var client = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing")).CreateClient();

        using var response = await client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OrganizationEndpoint_Returns403_WhenKnownActorHasNoPermission()
    {
        var actorId = Guid.NewGuid();
        using var client = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.AddScoped<ICurrentActor>(_ => new FixedCurrentActor(actorId));
                services.AddScoped<IAuthorizationRepository>(_ => new DenyingAuthorizationRepository(actorId));
            });
        }).CreateClient();

        using var response = await client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public void DevelopmentCurrentActor_IgnoresHeaderOutsideDevelopment()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Dev-User-Id"] = Guid.NewGuid().ToString();
        var accessor = new HttpContextAccessor { HttpContext = context };
        var actor = new DevelopmentCurrentActor(accessor, new FixedEnvironment("Production"));

        Assert.Null(actor.UserId);
    }

    [Fact]
    public void DevelopmentCurrentActor_ReadsHeaderOnlyInDevelopment()
    {
        var actorId = Guid.NewGuid();
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Dev-User-Id"] = actorId.ToString();
        var accessor = new HttpContextAccessor { HttpContext = context };
        var actor = new DevelopmentCurrentActor(accessor, new FixedEnvironment("Development"));

        Assert.Equal(actorId, actor.UserId);
    }

    private sealed class FixedCurrentActor(Guid userId) : ICurrentActor
    {
        public Guid? UserId => userId;
    }

    private sealed class FixedEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Licenses.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    private sealed class DenyingAuthorizationRepository(Guid actorId) : IAuthorizationRepository
    {
        public Task<User?> GetUserAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<User?>(userId == actorId ? User.Create("Actor", "actor@example.test", null, DateTime.UtcNow) : null);

        public Task<Licenses.Domain.Organization.OrgUnit?> GetOrgUnitAsync(Guid orgUnitId, CancellationToken cancellationToken) => Task.FromResult<Licenses.Domain.Organization.OrgUnit?>(null);
        public Task<List<Licenses.Domain.Organization.OrgUnit>> ListOrgUnitsAsync(CancellationToken cancellationToken) => Task.FromResult(new List<Licenses.Domain.Organization.OrgUnit>());
        public Task<List<Licenses.Domain.Organization.UserOrgAssignment>> ListActiveUserOrgAssignmentsAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken) => Task.FromResult(new List<Licenses.Domain.Organization.UserOrgAssignment>());
        public Task<List<Licenses.Domain.Authorization.RoleScopeAssignment>> ListActiveRoleScopeAssignmentsAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken) => Task.FromResult(new List<Licenses.Domain.Authorization.RoleScopeAssignment>());
        public Task<bool> RoleHasPermissionAsync(Guid roleId, string permissionCode, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<bool> IsRoleActiveAsync(Guid roleId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<List<User>> ListUsersInOrgUnitsAsync(IReadOnlyCollection<Guid> orgUnitIds, DateTime utcNow, CancellationToken cancellationToken) => Task.FromResult(new List<User>());
        public Task<List<DevelopmentActorDto>> ListDevelopmentActorsAsync(DateTime utcNow, CancellationToken cancellationToken) => Task.FromResult(new List<DevelopmentActorDto>());
    }
}
