using Licenses.Application.Authorization;
using Licenses.Domain.Authorization;
using Licenses.Domain.Identity;
using Licenses.Domain.Organization;
using Licenses.Infrastructure.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Licenses.Infrastructure.Tests;

public sealed class OrganizationPersistenceTests
{
    [Fact]
    public async Task PersistsOrganizationModelAgainstPostgreSql()
    {
        await PostgreSqlTestDatabase.WithFreshDatabaseAsync(async db =>
        {
            var now = DateTime.UtcNow;
            var company = OrgUnit.Create("Company", "COMPANY", null, now);
            var it = OrgUnit.Create("IT", "IT", company.Id, now);
            var user = User.Create("Felipe", "felipe@example.test", "entra-object-id", now);
            db.OrgUnits.AddRange(company, it);
            db.Users.Add(user);
            await db.SaveChangesAsync();

            db.UserOrgAssignments.Add(UserOrgAssignment.Create(user.Id, it.Id, true, now, null));
            await db.SaveChangesAsync();

            Assert.Equal(2, await db.OrgUnits.CountAsync());
            Assert.Single(await db.UserOrgAssignments.Where(x => x.UserId == user.Id).ToListAsync());
            Assert.True(await db.Users.AnyAsync(x => x.ExternalIdentityId == "entra-object-id"));

            var permission = Permission.Create(PermissionCodes.OrgUsersRead, "Read users in scope.");
            var role = Role.Create("MANAGER", "Manager", "Scoped manager", true, now);
            db.Permissions.Add(permission);
            db.Roles.Add(role);
            await db.SaveChangesAsync();

            db.RolePermissions.Add(RolePermission.Create(role.Id, permission.Id));
            db.RoleScopeAssignments.Add(RoleScopeAssignment.Create(user.Id, role.Id, it.Id, includeDescendants: true, now, null));
            await db.SaveChangesAsync();

            Assert.True(await db.RolePermissions.AnyAsync(x => x.RoleId == role.Id && x.PermissionId == permission.Id));
            Assert.True(await db.RoleScopeAssignments.AnyAsync(x => x.UserId == user.Id && x.IncludeDescendants));
        });
    }
}

public sealed class AuthorizationPersistenceTests
{
    [Fact]
    public async Task FiltersUsersByAuthorizedOrganizationalScopeAgainstPostgreSql()
    {
        await PostgreSqlTestDatabase.WithFreshDatabaseAsync(async db =>
        {
            var now = DateTime.UtcNow;
            var company = OrgUnit.Create("Company", "COMPANY", null, now);
            var it = OrgUnit.Create("IT", "IT", company.Id, now);
            var support = OrgUnit.Create("Support", "SUPPORT", it.Id, now);
            var hr = OrgUnit.Create("HR", "HR", company.Id, now);
            var manager = User.Create("Manager", "manager@example.test", null, now);
            var supportUser = User.Create("Support User", "support@example.test", null, now);
            var hrUser = User.Create("HR User", "hr@example.test", null, now);
            var permission = Permission.Create(PermissionCodes.OrgUsersRead, "Read scoped users.");
            var role = Role.Create("MANAGER", "Manager", "Scoped manager", true, now);

            db.OrgUnits.AddRange(company, it, support, hr);
            db.Users.AddRange(manager, supportUser, hrUser);
            db.Permissions.Add(permission);
            db.Roles.Add(role);
            await db.SaveChangesAsync();

            db.UserOrgAssignments.AddRange(
                UserOrgAssignment.Create(manager.Id, it.Id, true, now, null),
                UserOrgAssignment.Create(supportUser.Id, support.Id, true, now, null),
                UserOrgAssignment.Create(hrUser.Id, hr.Id, true, now, null));
            db.RolePermissions.Add(RolePermission.Create(role.Id, permission.Id));
            db.RoleScopeAssignments.Add(RoleScopeAssignment.Create(manager.Id, role.Id, it.Id, includeDescendants: true, now, null));
            await db.SaveChangesAsync();

            var authorization = new AuthorizationService(new EfAuthorizationRepository(db), TimeProvider.System);
            var allowedUnitIds = await authorization.GetAuthorizedOrgUnitIdsAsync(manager.Id, PermissionCodes.OrgUsersRead, CancellationToken.None);
            var visibleUsers = await new EfAuthorizationRepository(db).ListUsersInOrgUnitsAsync(allowedUnitIds.ToList(), DateTime.UtcNow, CancellationToken.None);

            Assert.Contains(visibleUsers, x => x.Id == manager.Id);
            Assert.Contains(visibleUsers, x => x.Id == supportUser.Id);
            Assert.DoesNotContain(visibleUsers, x => x.Id == hrUser.Id);
        });
    }
}
