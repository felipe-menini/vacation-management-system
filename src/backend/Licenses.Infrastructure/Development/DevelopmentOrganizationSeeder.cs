using Licenses.Application.Authorization;
using Licenses.Domain.Authorization;
using Licenses.Domain.Identity;
using Licenses.Domain.LeaveManagement;
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
        if ((await db.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
        {
            await db.Database.MigrateAsync(cancellationToken);
        }

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
        var hrUser = await EnsureUserWithPrimaryAssignmentAsync(db, "HR User", "hr.user@example.test", hr.Id, now, cancellationToken);

        var permissions = await EnsurePermissionCatalogAsync(db, cancellationToken);
        var employee = await EnsureRoleAsync(db, "EMPLOYEE", "Employee", "Base employee capabilities.", now, cancellationToken);
        var supervisor = await EnsureRoleAsync(db, "SUPERVISOR", "Supervisor", "Scoped supervisor capabilities.", now, cancellationToken);
        var manager = await EnsureRoleAsync(db, "MANAGER", "Manager", "Scoped manager capabilities with descendants when assigned.", now, cancellationToken);
        var hrRole = await EnsureRoleAsync(db, "HR", "HR", "Organization-wide HR administration.", now, cancellationToken);
        var techAdmin = await EnsureRoleAsync(db, "TECH_ADMIN", "Technical Administrator", "Technical platform administration without default HR authority.", now, cancellationToken);

        await EnsureRolePermissionsAsync(db, employee, [PermissionCodes.LeaveCatalogRead, PermissionCodes.LeavePoliciesRead], permissions, cancellationToken);
        await EnsureRolePermissionsAsync(db, supervisor, [PermissionCodes.OrgUnitsRead, PermissionCodes.OrgUsersRead, PermissionCodes.OrgAssignmentsRead, PermissionCodes.LeaveCatalogRead, PermissionCodes.LeavePoliciesRead], permissions, cancellationToken);
        await EnsureRolePermissionsAsync(db, manager, [PermissionCodes.OrgUnitsRead, PermissionCodes.OrgUnitsManage, PermissionCodes.OrgUsersRead, PermissionCodes.OrgUsersManage, PermissionCodes.OrgAssignmentsRead, PermissionCodes.OrgAssignmentsManage, PermissionCodes.LeaveCatalogRead, PermissionCodes.LeavePoliciesRead], permissions, cancellationToken);
        await EnsureRolePermissionsAsync(db, hrRole, [PermissionCodes.OrgUnitsRead, PermissionCodes.OrgUnitsManage, PermissionCodes.OrgUsersRead, PermissionCodes.OrgUsersManage, PermissionCodes.OrgAssignmentsRead, PermissionCodes.OrgAssignmentsManage, PermissionCodes.LeaveCatalogRead, PermissionCodes.LeaveCatalogManage, PermissionCodes.LeavePoliciesRead, PermissionCodes.LeavePoliciesManage], permissions, cancellationToken);
        await EnsureRolePermissionsAsync(db, techAdmin, [PermissionCodes.OrgUnitsRead], permissions, cancellationToken);

        await EnsureRoleScopeAssignmentAsync(db, felipe.Id, manager.Id, it.Id, includeDescendants: true, now, cancellationToken);
        await EnsureRoleScopeAssignmentAsync(db, supportSupervisor.Id, supervisor.Id, support.Id, includeDescendants: false, now, cancellationToken);
        await EnsureRoleScopeAssignmentAsync(db, supportUser.Id, employee.Id, support.Id, includeDescendants: false, now, cancellationToken);
        await EnsureRoleScopeAssignmentAsync(db, developmentUser.Id, employee.Id, development.Id, includeDescendants: false, now, cancellationToken);
        await EnsureRoleScopeAssignmentAsync(db, cybersecurityUser.Id, employee.Id, cybersecurity.Id, includeDescendants: false, now, cancellationToken);
        await EnsureRoleScopeAssignmentAsync(db, hrUser.Id, hrRole.Id, company.Id, includeDescendants: true, now, cancellationToken);

        await EnsureLeaveCatalogSeedAsync(db, now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await EnsureLeavePolicySeedAsync(db, now, cancellationToken);

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
            [PermissionCodes.OrgAssignmentsManage] = "Manage user organizational assignments inside authorized scope.",
            [PermissionCodes.LeaveCatalogRead] = "Read leave catalog configuration.",
            [PermissionCodes.LeaveCatalogManage] = "Create and update leave catalog configuration.",
            [PermissionCodes.LeavePoliciesRead] = "Read leave policy configuration.",
            [PermissionCodes.LeavePoliciesManage] = "Create, update, and publish leave policy configuration."
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

    private static async Task EnsureLeaveCatalogSeedAsync(ApplicationDbContext db, DateTime now, CancellationToken cancellationToken)
    {
        await EnsureLeaveTypeAsync(db, "VACATION", "Vacation", null, 10, now, cancellationToken);
        await EnsureLeaveTypeAsync(db, "MEDICAL", "Medical Leave", null, 20, now, cancellationToken);
        await EnsureLeaveTypeAsync(db, "MEDICAL_EXAM", "Medical Examination", null, 30, now, cancellationToken);
        await EnsureLeaveTypeAsync(db, "STUDY", "Study Leave", null, 40, now, cancellationToken);
        await EnsureLeaveTypeAsync(db, "BEREAVEMENT", "Bereavement Leave", null, 50, now, cancellationToken);

        await EnsureBalanceBucketAsync(db, "VACATION_DAYS", "Vacation Days", null, BalanceBucketUnit.Day, now, cancellationToken);
        await EnsureBalanceBucketAsync(db, "MEDICAL_EXAM_DAYS", "Medical Examination Days", null, BalanceBucketUnit.Day, now, cancellationToken);
    }

    private static async Task EnsureLeaveTypeAsync(ApplicationDbContext db, string code, string name, string? description, int sortOrder, DateTime now, CancellationToken cancellationToken)
    {
        if (await db.LeaveTypes.AnyAsync(x => x.Code == code, cancellationToken)) return;
        await db.LeaveTypes.AddAsync(LeaveType.Create(code, name, description, sortOrder, isActive: true, now), cancellationToken);
    }

    private static async Task EnsureBalanceBucketAsync(ApplicationDbContext db, string code, string name, string? description, BalanceBucketUnit unit, DateTime now, CancellationToken cancellationToken)
    {
        if (await db.BalanceBuckets.AnyAsync(x => x.Code == code, cancellationToken)) return;
        await db.BalanceBuckets.AddAsync(BalanceBucket.Create(code, name, description, unit, isActive: true, now), cancellationToken);
    }

    private static async Task EnsureLeavePolicySeedAsync(ApplicationDbContext db, DateTime now, CancellationToken cancellationToken)
    {
        var vacation = await db.LeaveTypes.SingleAsync(x => x.Code == "VACATION", cancellationToken);
        var medical = await db.LeaveTypes.SingleAsync(x => x.Code == "MEDICAL", cancellationToken);
        var vacationBucket = await db.BalanceBuckets.SingleAsync(x => x.Code == "VACATION_DAYS", cancellationToken);
        var it = await db.OrgUnits.SingleAsync(x => x.Code == "IT", cancellationToken);

        var vacationPolicy = await EnsureLeavePolicyAsync(db, vacation.Id, orgUnitId: null, appliesToDescendants: false, now, cancellationToken);
        await EnsurePublishedPolicyVersionAsync(
            db,
            vacationPolicy.Id,
            versionNumber: 1,
            effectiveFrom: new DateOnly(2026, 1, 1),
            effectiveTo: null,
            PolicyDayCountMode.BusinessDays,
            allowHalfDay: true,
            minimumNoticeDays: 7,
            PolicyDayCountMode.CalendarDays,
            maximumRequestDays: 15m,
            PolicyOverlapBehavior.Block,
            consumesBalance: true,
            vacationBucket.Id,
            now,
            cancellationToken);

        var medicalPolicy = await EnsureLeavePolicyAsync(db, medical.Id, orgUnitId: null, appliesToDescendants: false, now, cancellationToken);
        await EnsurePublishedPolicyVersionAsync(
            db,
            medicalPolicy.Id,
            versionNumber: 1,
            effectiveFrom: new DateOnly(2026, 1, 1),
            effectiveTo: null,
            PolicyDayCountMode.CalendarDays,
            allowHalfDay: false,
            minimumNoticeDays: 0,
            PolicyDayCountMode.CalendarDays,
            maximumRequestDays: null,
            PolicyOverlapBehavior.Allow,
            consumesBalance: false,
            balanceBucketId: null,
            now,
            cancellationToken);

        var itVacationOverride = await EnsureLeavePolicyAsync(db, vacation.Id, it.Id, appliesToDescendants: true, now, cancellationToken);
        await EnsurePublishedPolicyVersionAsync(
            db,
            itVacationOverride.Id,
            versionNumber: 1,
            effectiveFrom: new DateOnly(2026, 1, 1),
            effectiveTo: null,
            PolicyDayCountMode.BusinessDays,
            allowHalfDay: true,
            minimumNoticeDays: 5,
            PolicyDayCountMode.CalendarDays,
            maximumRequestDays: 10m,
            PolicyOverlapBehavior.Warn,
            consumesBalance: true,
            vacationBucket.Id,
            now,
            cancellationToken);
    }

    private static async Task<LeavePolicy> EnsureLeavePolicyAsync(ApplicationDbContext db, Guid leaveTypeId, Guid? orgUnitId, bool appliesToDescendants, DateTime now, CancellationToken cancellationToken)
    {
        var existing = await db.LeavePolicies.FirstOrDefaultAsync(x => x.LeaveTypeId == leaveTypeId && x.OrgUnitId == orgUnitId, cancellationToken);
        if (existing is not null) return existing;
        var policy = LeavePolicy.Create(leaveTypeId, orgUnitId, appliesToDescendants, isActive: true, now);
        await db.LeavePolicies.AddAsync(policy, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return policy;
    }

    private static async Task EnsurePublishedPolicyVersionAsync(ApplicationDbContext db, Guid policyId, int versionNumber, DateOnly effectiveFrom, DateOnly? effectiveTo, PolicyDayCountMode dayCountMode, bool allowHalfDay, int? minimumNoticeDays, PolicyDayCountMode noticeDayCountMode, decimal? maximumRequestDays, PolicyOverlapBehavior overlapBehavior, bool consumesBalance, Guid? balanceBucketId, DateTime now, CancellationToken cancellationToken)
    {
        if (await db.LeavePolicyVersions.AnyAsync(x => x.LeavePolicyId == policyId && x.VersionNumber == versionNumber, cancellationToken)) return;
        var version = LeavePolicyVersion.CreateDraft(policyId, versionNumber, effectiveFrom, effectiveTo, dayCountMode, allowHalfDay, minimumNoticeDays, noticeDayCountMode, maximumRequestDays, overlapBehavior, consumesBalance, balanceBucketId, now);
        version.Publish(now);
        await db.LeavePolicyVersions.AddAsync(version, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }
}
