using Licenses.Application.Authorization;
using Licenses.Domain.Authorization;
using Licenses.Domain.Identity;
using Licenses.Domain.Organization;

namespace Licenses.Application.Tests;

public sealed class AuthorizationServiceTests
{
    private readonly DateTime _now = new(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc);
    private readonly FakeAuthorizationRepository _repository = new();
    private readonly AuthorizationService _service;

    public AuthorizationServiceTests()
    {
        _service = new AuthorizationService(_repository, new FixedTimeProvider(_now));
    }

    [Fact]
    public async Task RoleGrantsPermissionAndExactScope()
    {
        var setup = SeedTree();
        var actor = AddActorWithRole(setup.It, includeDescendants: false, PermissionCodes.OrgUnitsRead);

        Assert.True(await _service.CanUserPerformAsync(actor.Id, PermissionCodes.OrgUnitsRead, setup.It.Id, CancellationToken.None));
    }

    [Fact]
    public async Task RoleWithoutPermissionDeniesAccess()
    {
        var setup = SeedTree();
        var actor = AddActorWithRole(setup.It, includeDescendants: true);

        Assert.False(await _service.CanUserPerformAsync(actor.Id, PermissionCodes.OrgUnitsRead, setup.Support.Id, CancellationToken.None));
    }

    [Fact]
    public async Task IncludeDescendantsIncludesChildAndDeepDescendants()
    {
        var setup = SeedTree();
        var actor = AddActorWithRole(setup.It, includeDescendants: true, PermissionCodes.OrgUnitsRead);

        Assert.True(await _service.CanUserPerformAsync(actor.Id, PermissionCodes.OrgUnitsRead, setup.Support.Id, CancellationToken.None));
        Assert.True(await _service.CanUserPerformAsync(actor.Id, PermissionCodes.OrgUnitsRead, setup.Cybersecurity.Id, CancellationToken.None));
    }

    [Fact]
    public async Task IncludeDescendantsFalseExcludesChildren()
    {
        var setup = SeedTree();
        var actor = AddActorWithRole(setup.Support, includeDescendants: false, PermissionCodes.OrgUnitsRead);

        Assert.True(await _service.CanUserPerformAsync(actor.Id, PermissionCodes.OrgUnitsRead, setup.Support.Id, CancellationToken.None));
        Assert.False(await _service.CanUserPerformAsync(actor.Id, PermissionCodes.OrgUnitsRead, setup.DeepSupport.Id, CancellationToken.None));
    }

    [Fact]
    public async Task SiblingAndUnrelatedBranchesAreExcluded()
    {
        var setup = SeedTree();
        var actor = AddActorWithRole(setup.Support, includeDescendants: true, PermissionCodes.OrgUnitsRead);

        Assert.False(await _service.CanUserPerformAsync(actor.Id, PermissionCodes.OrgUnitsRead, setup.Development.Id, CancellationToken.None));
        Assert.False(await _service.CanUserPerformAsync(actor.Id, PermissionCodes.OrgUnitsRead, setup.Hr.Id, CancellationToken.None));
    }

    [Fact]
    public async Task ExpiredAndFutureScopeAssignmentsDoNotGrantAccess()
    {
        var setup = SeedTree();
        var actor = AddUser("Actor", setup.It);
        var role = AddRole(true);
        var permission = AddPermission(PermissionCodes.OrgUnitsRead);
        _repository.RolePermissions.Add(RolePermission.Create(role.Id, permission.Id));
        _repository.RoleScopeAssignments.Add(RoleScopeAssignment.Create(actor.Id, role.Id, setup.It.Id, true, _now.AddDays(-10), _now.AddDays(-1)));
        _repository.RoleScopeAssignments.Add(RoleScopeAssignment.Create(actor.Id, role.Id, setup.Hr.Id, false, _now.AddDays(1), null));

        Assert.False(await _service.CanUserPerformAsync(actor.Id, PermissionCodes.OrgUnitsRead, setup.Support.Id, CancellationToken.None));
        Assert.False(await _service.CanUserPerformAsync(actor.Id, PermissionCodes.OrgUnitsRead, setup.Hr.Id, CancellationToken.None));
    }

    [Fact]
    public async Task InactiveRoleDoesNotGrantAccess()
    {
        var setup = SeedTree();
        var actor = AddUser("Actor", setup.It);
        var role = AddRole(false);
        var permission = AddPermission(PermissionCodes.OrgUnitsRead);
        _repository.RolePermissions.Add(RolePermission.Create(role.Id, permission.Id));
        _repository.RoleScopeAssignments.Add(RoleScopeAssignment.Create(actor.Id, role.Id, setup.It.Id, true, _now.AddDays(-1), null));

        Assert.False(await _service.CanUserPerformAsync(actor.Id, PermissionCodes.OrgUnitsRead, setup.It.Id, CancellationToken.None));
    }

    [Fact]
    public async Task InactiveUserCannotAct()
    {
        var setup = SeedTree();
        var actor = AddActorWithRole(setup.It, includeDescendants: true, PermissionCodes.OrgUnitsRead);
        actor.Update(actor.DisplayName, actor.Email, actor.ExternalIdentityId, isActive: false, _now);

        Assert.False(await _service.CanUserPerformAsync(actor.Id, PermissionCodes.OrgUnitsRead, setup.It.Id, CancellationToken.None));
    }

    [Fact]
    public async Task MultipleAssignmentsGrantUnionOfPermissionsAndScopes()
    {
        var setup = SeedTree();
        var actor = AddUser("Actor", setup.It);
        var readRole = AddRole(true);
        var manageRole = AddRole(true);
        var read = AddPermission(PermissionCodes.OrgUnitsRead);
        var manage = AddPermission(PermissionCodes.OrgUnitsManage);
        _repository.RolePermissions.Add(RolePermission.Create(readRole.Id, read.Id));
        _repository.RolePermissions.Add(RolePermission.Create(manageRole.Id, manage.Id));
        _repository.RoleScopeAssignments.Add(RoleScopeAssignment.Create(actor.Id, readRole.Id, setup.Support.Id, false, _now.AddDays(-1), null));
        _repository.RoleScopeAssignments.Add(RoleScopeAssignment.Create(actor.Id, manageRole.Id, setup.Development.Id, false, _now.AddDays(-1), null));

        Assert.True(await _service.CanUserPerformAsync(actor.Id, PermissionCodes.OrgUnitsRead, setup.Support.Id, CancellationToken.None));
        Assert.True(await _service.CanUserPerformAsync(actor.Id, PermissionCodes.OrgUnitsManage, setup.Development.Id, CancellationToken.None));
        Assert.False(await _service.CanUserPerformAsync(actor.Id, PermissionCodes.OrgUnitsRead, setup.Development.Id, CancellationToken.None));
    }

    [Fact]
    public async Task UserAccessUsesTargetMembershipScope()
    {
        var setup = SeedTree();
        var supervisor = AddActorWithRole(setup.Support, includeDescendants: false, PermissionCodes.OrgUsersRead);
        var supportUser = AddUser("Support User", setup.Support);
        var developmentUser = AddUser("Development User", setup.Development);

        Assert.True(await _service.CanAccessUserAsync(supervisor.Id, PermissionCodes.OrgUsersRead, supportUser.Id, CancellationToken.None));
        Assert.False(await _service.CanAccessUserAsync(supervisor.Id, PermissionCodes.OrgUsersRead, developmentUser.Id, CancellationToken.None));
    }

    [Fact]
    public async Task ManagerItCanAccessItDescendantsButNotHr()
    {
        var setup = SeedTree();
        var manager = AddActorWithRole(setup.It, includeDescendants: true, PermissionCodes.OrgUsersRead);
        var supportUser = AddUser("Support User", setup.Support);
        var developmentUser = AddUser("Development User", setup.Development);
        var cybersecurityUser = AddUser("Cybersecurity User", setup.Cybersecurity);
        var hrUser = AddUser("HR User", setup.Hr);

        Assert.True(await _service.CanAccessUserAsync(manager.Id, PermissionCodes.OrgUsersRead, supportUser.Id, CancellationToken.None));
        Assert.True(await _service.CanAccessUserAsync(manager.Id, PermissionCodes.OrgUsersRead, developmentUser.Id, CancellationToken.None));
        Assert.True(await _service.CanAccessUserAsync(manager.Id, PermissionCodes.OrgUsersRead, cybersecurityUser.Id, CancellationToken.None));
        Assert.False(await _service.CanAccessUserAsync(manager.Id, PermissionCodes.OrgUsersRead, hrUser.Id, CancellationToken.None));
    }

    private OrgSetup SeedTree()
    {
        var company = OrgUnit.Create("Company", "COMPANY", null, _now);
        var it = OrgUnit.Create("IT", "IT", company.Id, _now);
        var support = OrgUnit.Create("Support", "SUPPORT", it.Id, _now);
        var deepSupport = OrgUnit.Create("Tier 2", "TIER2", support.Id, _now);
        var development = OrgUnit.Create("Development", "DEVELOPMENT", it.Id, _now);
        var cybersecurity = OrgUnit.Create("Cybersecurity", "CYBERSECURITY", development.Id, _now);
        var hr = OrgUnit.Create("HR", "HR", company.Id, _now);
        _repository.OrgUnits.AddRange([company, it, support, deepSupport, development, cybersecurity, hr]);
        return new(company, it, support, deepSupport, development, cybersecurity, hr);
    }

    private User AddActorWithRole(OrgUnit scopedUnit, bool includeDescendants, params string[] permissionCodes)
    {
        var actor = AddUser("Actor", scopedUnit);
        var role = AddRole(true);
        foreach (var permissionCode in permissionCodes)
        {
            var permission = AddPermission(permissionCode);
            _repository.RolePermissions.Add(RolePermission.Create(role.Id, permission.Id));
        }
        _repository.RoleScopeAssignments.Add(RoleScopeAssignment.Create(actor.Id, role.Id, scopedUnit.Id, includeDescendants, _now.AddDays(-1), null));
        return actor;
    }

    private User AddUser(string name, OrgUnit orgUnit)
    {
        var user = User.Create(name, $"{Guid.NewGuid():N}@example.test", null, _now);
        _repository.Users.Add(user);
        _repository.UserOrgAssignments.Add(UserOrgAssignment.Create(user.Id, orgUnit.Id, true, _now.AddDays(-1), null));
        return user;
    }

    private Role AddRole(bool active)
    {
        var role = Role.Create(Guid.NewGuid().ToString("N"), "Role", "Test role", true, _now);
        if (!active) role.Update(role.Name, role.Description, false, _now);
        _repository.Roles.Add(role);
        return role;
    }

    private Permission AddPermission(string code)
    {
        var permission = Permission.Create(code, code);
        _repository.Permissions.Add(permission);
        return permission;
    }

    private sealed record OrgSetup(OrgUnit Company, OrgUnit It, OrgUnit Support, OrgUnit DeepSupport, OrgUnit Development, OrgUnit Cybersecurity, OrgUnit Hr);

    private sealed class FixedTimeProvider(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }

    private sealed class FakeAuthorizationRepository : IAuthorizationRepository
    {
        public List<User> Users { get; } = [];
        public List<OrgUnit> OrgUnits { get; } = [];
        public List<UserOrgAssignment> UserOrgAssignments { get; } = [];
        public List<Role> Roles { get; } = [];
        public List<Permission> Permissions { get; } = [];
        public List<RolePermission> RolePermissions { get; } = [];
        public List<RoleScopeAssignment> RoleScopeAssignments { get; } = [];

        public Task<User?> GetUserAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(Users.SingleOrDefault(x => x.Id == userId));
        public Task<OrgUnit?> GetOrgUnitAsync(Guid orgUnitId, CancellationToken cancellationToken) => Task.FromResult(OrgUnits.SingleOrDefault(x => x.Id == orgUnitId));
        public Task<List<OrgUnit>> ListOrgUnitsAsync(CancellationToken cancellationToken) => Task.FromResult(OrgUnits.ToList());
        public Task<List<UserOrgAssignment>> ListActiveUserOrgAssignmentsAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken) => Task.FromResult(UserOrgAssignments.Where(x => x.UserId == userId && x.IsActiveAt(utcNow)).ToList());
        public Task<List<RoleScopeAssignment>> ListActiveRoleScopeAssignmentsAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken) => Task.FromResult(RoleScopeAssignments.Where(x => x.UserId == userId && x.IsActiveAt(utcNow)).ToList());
        public Task<bool> RoleHasPermissionAsync(Guid roleId, string permissionCode, CancellationToken cancellationToken) => Task.FromResult(RolePermissions.Any(x => x.RoleId == roleId && Permissions.Any(p => p.Id == x.PermissionId && p.Code == permissionCode)));
        public Task<bool> IsRoleActiveAsync(Guid roleId, CancellationToken cancellationToken) => Task.FromResult(Roles.Any(x => x.Id == roleId && x.IsActive));
        public Task<List<User>> ListUsersInOrgUnitsAsync(IReadOnlyCollection<Guid> orgUnitIds, DateTime utcNow, CancellationToken cancellationToken) => Task.FromResult(Users.Where(user => user.IsActive && UserOrgAssignments.Any(x => x.UserId == user.Id && orgUnitIds.Contains(x.OrgUnitId) && x.IsActiveAt(utcNow))).ToList());
        public Task<List<DevelopmentActorDto>> ListDevelopmentActorsAsync(DateTime utcNow, CancellationToken cancellationToken) => Task.FromResult(new List<DevelopmentActorDto>());
    }
}
