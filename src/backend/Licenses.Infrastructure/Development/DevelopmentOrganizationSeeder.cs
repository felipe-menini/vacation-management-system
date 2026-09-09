using Licenses.Application.Authorization;
using Licenses.Domain.Authorization;
using Licenses.Domain.Identity;
using Licenses.Domain.Organization;
using Licenses.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Licenses.Infrastructure.Development;

public static class DevelopmentOrganizationSeeder
{
    public static async Task SeedAsync(IHost host, CancellationToken cancellationToken = default)
    {
        if (!host.Services.GetRequiredService<IHostEnvironment>().IsDevelopment()) return;

        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var company = await EnsureOrgUnitAsync(db, "Company", "COMPANY", null, now, cancellationToken);
        var it = await EnsureOrgUnitAsync(db, "IT", "IT", company.Id, now, cancellationToken);
        var support = await EnsureOrgUnitAsync(db, "Support", "SUPPORT", it.Id, now, cancellationToken);
        var development = await EnsureOrgUnitAsync(db, "Development", "DEVELOPMENT", it.Id, now, cancellationToken);
        var cybersecurity = await EnsureOrgUnitAsync(db, "Cybersecurity", "CYBERSECURITY", it.Id, now, cancellationToken);
        var hr = await EnsureOrgUnitAsync(db, "HR", "HR", company.Id, now, cancellationToken);

        var felipe = await EnsureUserWithPrimaryAssignmentAsync(db, "Felipe", "felipe@example.test", it.Id, now, cancellationToken);
        var supportSupervisor = await EnsureUserWithPrimaryAssignmentAsync(db, "Support Supervisor", "support.supervisor@example.test", support.Id, now, cancellationToken);
        var supportUser = await EnsureUserWithPrimaryAssignmentAsync(db, "Support User", "support.user@example.test", support.Id, now, cancellationToken);
        var developmentUser = await EnsureUserWithPrimaryAssignmentAsync(db, "Development User", "development.user@example.test", development.Id, now, cancellationToken);
        var cybersecurityUser = await EnsureUserWithPrimaryAssignmentAsync(db, "Cybersecurity User", "cybersecurity.user@example.test", cybersecurity.Id, now, cancellationToken);
        await EnsureUserWithPrimaryAssignmentAsync(db, "HR User", "hr.user@example.test", hr.Id, now, cancellationToken);

        var permissions = await EnsurePermissionCatalogAsync(db, cancellationToken);
        var employee = await EnsureRoleAsync(db, "EMPLOYEE", "Employee", "Base employee capabilities.", now, cancellationToken);
        var supervisor = await EnsureRoleAsync(db, "SUPERVISOR", "Supervisor", "Scoped supervisor capabilities.", now, cancellationToken);
        var manager = await EnsureRoleAsync(db, "MANAGER", "Manager", "Scoped manager capabilities with descendants when assigned.", now, cancellationToken);
        var hrRole = await EnsureRoleAsync(db, "HR", "HR", "Organization-wide HR administration.", now, cancellationToken);
        var techAdmin = await EnsureRoleAsync(db, "TECH_ADMIN", "Technical Administrator", "Technical platform administration without default HR authority.", now, cancellationToken);

        await EnsureRolePermissionsAsync(db, employee, [], permissions, cancellationToken);
        await EnsureRolePermissionsAsync(db, supervisor, [PermissionCodes.OrgUnitsRead, PermissionCodes.OrgUsersRead, PermissionCodes.OrgAssignmentsRead], permissions, cancellationToken);
        await EnsureRolePermissionsAsync(db, manager, [PermissionCodes.OrgUnitsRead, PermissionCodes.OrgUnitsManage, PermissionCodes.OrgUsersRead, PermissionCodes.OrgUsersManage, PermissionCodes.OrgAssignmentsRead, PermissionCodes.OrgAssignmentsManage], permissions, cancellationToken);
        await EnsureRolePermissionsAsync(db, hrRole, [PermissionCodes.OrgUnitsRead, PermissionCodes.OrgUnitsManage, PermissionCodes.OrgUsersRead, PermissionCodes.OrgUsersManage, PermissionCodes.OrgAssignmentsRead, PermissionCodes.OrgAssignmentsManage], permissions, cancellationToken);
        await EnsureRolePermissionsAsync(db, techAdmin, [PermissionCodes.OrgUnitsRead], permissions, cancellationToken);

        await EnsureRoleScopeAssignmentAsync(db, felipe.Id, manager.Id, it.Id, includeDescendants: true, now, cancellationToken);
        await EnsureRoleScopeAssignmentAsync(db, supportSupervisor.Id, supervisor.Id, support.Id, includeDescendants: false, now, cancellationToken);
        await EnsureRoleScopeAssignmentAsync(db, supportUser.Id, employee.Id, support.Id, includeDescendants: false, now, cancellationToken);
        await EnsureRoleScopeAssignmentAsync(db, developmentUser.Id, employee.Id, development.Id, includeDescendants: false, now, cancellationToken);
        await EnsureRoleScopeAssignmentAsync(db, cybersecurityUser.Id, employee.Id, cybersecurity.Id, includeDescendants: false, now, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task<OrgUnit> EnsureOrgUnitAsync(ApplicationDbContext db, string name, string code, Guid? parentId, DateTime now, CancellationToken cancellationToken)
    {
        var existing = await db.OrgUnits.FirstOrDefaultAsync(x => x.Code == code, cancellationToken);
        if (existing is not null) return existing;
        var unit = OrgUnit.Create(name, code, parentId, now);
        await db.OrgUnits.AddAsync(unit, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return unit;
    }

    private static async Task<User> EnsureUserWithPrimaryAssignmentAsync(ApplicationDbContext db, string displayName, string email, Guid orgUnitId, DateTime now, CancellationToken cancellationToken)
    {
        var normalizedEmail = email.ToUpperInvariant();
        var user = await db.Users.FirstOrDefaultAsync(x => x.Email == normalizedEmail, cancellationToken);
        if (user is null)
        {
            user = User.Create(displayName, email, externalIdentityId: null, now);
            await db.Users.AddAsync(user, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        var hasAssignment = await db.UserOrgAssignments.AnyAsync(x => x.UserId == user.Id && x.OrgUnitId == orgUnitId && x.IsPrimary && x.EffectiveToUtc == null, cancellationToken);
        if (!hasAssignment)
        {
            await db.UserOrgAssignments.AddAsync(UserOrgAssignment.Create(user.Id, orgUnitId, isPrimary: true, now, effectiveToUtc: null), cancellationToken);
        }

        return user;
    }

    private static async Task<Dictionary<string, Permission>> EnsurePermissionCatalogAsync(ApplicationDbContext db, CancellationToken cancellationToken)
    {
        var catalog = new Dictionary<string, string>
        {
            [PermissionCodes.OrgUnitsRead] = "Read organizational units.",
            [PermissionCodes.OrgUnitsManage] = "Create and update organizational units.",
            [PermissionCodes.OrgUsersRead] = "Read users inside authorized organizational scope.",
            [PermissionCodes.OrgUsersManage] = "Create and update users inside authorized organizational scope.",
            [PermissionCodes.OrgAssignmentsRead] = "Read user organizational assignments inside authorized scope.",
            [PermissionCodes.OrgAssignmentsManage] = "Manage user organizational assignments inside authorized scope."
        };

        foreach (var item in catalog)
        {
            if (!await db.Permissions.AnyAsync(x => x.Code == item.Key, cancellationToken))
            {
                await db.Permissions.AddAsync(Permission.Create(item.Key, item.Value), cancellationToken);
            }
        }
        await db.SaveChangesAsync(cancellationToken);
        return await db.Permissions.Where(x => catalog.Keys.Contains(x.Code)).ToDictionaryAsync(x => x.Code, cancellationToken);
    }

    private static async Task<Role> EnsureRoleAsync(ApplicationDbContext db, string code, string name, string description, DateTime now, CancellationToken cancellationToken)
    {
        var role = await db.Roles.FirstOrDefaultAsync(x => x.Code == code, cancellationToken);
        if (role is not null) return role;
        role = Role.Create(code, name, description, isSystem: true, now);
        await db.Roles.AddAsync(role, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return role;
    }

    private static async Task EnsureRolePermissionsAsync(ApplicationDbContext db, Role role, IReadOnlyCollection<string> permissionCodes, IReadOnlyDictionary<string, Permission> permissions, CancellationToken cancellationToken)
    {
        foreach (var permissionCode in permissionCodes)
        {
            var permissionId = permissions[permissionCode].Id;
            if (!await db.RolePermissions.AnyAsync(x => x.RoleId == role.Id && x.PermissionId == permissionId, cancellationToken))
            {
                await db.RolePermissions.AddAsync(RolePermission.Create(role.Id, permissionId), cancellationToken);
            }
        }
    }

    private static async Task EnsureRoleScopeAssignmentAsync(ApplicationDbContext db, Guid userId, Guid roleId, Guid orgUnitId, bool includeDescendants, DateTime now, CancellationToken cancellationToken)
    {
        var exists = await db.RoleScopeAssignments.AnyAsync(x =>
            x.UserId == userId && x.RoleId == roleId && x.OrgUnitId == orgUnitId && x.EffectiveToUtc == null, cancellationToken);
        if (!exists)
        {
            await db.RoleScopeAssignments.AddAsync(RoleScopeAssignment.Create(userId, roleId, orgUnitId, includeDescendants, now, effectiveToUtc: null), cancellationToken);
        }
    }
}
