using Licenses.Application.Authorization;
using Licenses.Domain.Authorization;
using Licenses.Domain.Identity;
using Licenses.Domain.Organization;
using Licenses.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Licenses.Infrastructure.Authorization;

public sealed class EfAuthorizationRepository(ApplicationDbContext dbContext) : IAuthorizationRepository
{
    public Task<User?> GetUserAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

    public Task<OrgUnit?> GetOrgUnitAsync(Guid orgUnitId, CancellationToken cancellationToken) =>
        dbContext.OrgUnits.AsNoTracking().FirstOrDefaultAsync(x => x.Id == orgUnitId, cancellationToken);

    public Task<List<OrgUnit>> ListOrgUnitsAsync(CancellationToken cancellationToken) =>
        dbContext.OrgUnits.AsNoTracking().ToListAsync(cancellationToken);

    public Task<List<UserOrgAssignment>> ListActiveUserOrgAssignmentsAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken) =>
        dbContext.UserOrgAssignments.AsNoTracking()
            .Where(x => x.UserId == userId && x.EffectiveFromUtc <= utcNow && (x.EffectiveToUtc == null || x.EffectiveToUtc > utcNow))
            .ToListAsync(cancellationToken);

    public Task<List<RoleScopeAssignment>> ListActiveRoleScopeAssignmentsAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken) =>
        dbContext.RoleScopeAssignments.AsNoTracking()
            .Where(x => x.UserId == userId && x.EffectiveFromUtc <= utcNow && (x.EffectiveToUtc == null || x.EffectiveToUtc > utcNow))
            .ToListAsync(cancellationToken);

    public Task<bool> RoleHasPermissionAsync(Guid roleId, string permissionCode, CancellationToken cancellationToken) =>
        dbContext.RolePermissions.AsNoTracking()
            .Join(dbContext.Permissions.AsNoTracking(), rolePermission => rolePermission.PermissionId, permission => permission.Id, (rolePermission, permission) => new { rolePermission.RoleId, permission.Code })
            .AnyAsync(x => x.RoleId == roleId && x.Code == permissionCode, cancellationToken);

    public Task<bool> IsRoleActiveAsync(Guid roleId, CancellationToken cancellationToken) =>
        dbContext.Roles.AsNoTracking().AnyAsync(x => x.Id == roleId && x.IsActive, cancellationToken);

    public Task<List<User>> ListUsersInOrgUnitsAsync(IReadOnlyCollection<Guid> orgUnitIds, DateTime utcNow, CancellationToken cancellationToken) =>
        dbContext.Users.AsNoTracking()
            .Where(user => user.IsActive && dbContext.UserOrgAssignments.Any(assignment =>
                assignment.UserId == user.Id
                && orgUnitIds.Contains(assignment.OrgUnitId)
                && assignment.EffectiveFromUtc <= utcNow
                && (assignment.EffectiveToUtc == null || assignment.EffectiveToUtc > utcNow)))
            .OrderBy(x => x.DisplayName)
            .ToListAsync(cancellationToken);

    public async Task<List<DevelopmentActorDto>> ListDevelopmentActorsAsync(DateTime utcNow, CancellationToken cancellationToken)
    {
        var primaryAssignments = await dbContext.UserOrgAssignments.AsNoTracking()
            .Where(x => x.IsPrimary && x.EffectiveFromUtc <= utcNow && (x.EffectiveToUtc == null || x.EffectiveToUtc > utcNow))
            .ToListAsync(cancellationToken);
        var units = await dbContext.OrgUnits.AsNoTracking().ToDictionaryAsync(x => x.Id, cancellationToken);
        var users = await dbContext.Users.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.DisplayName).ToListAsync(cancellationToken);

        return users.Select(user =>
        {
            var primary = primaryAssignments.FirstOrDefault(x => x.UserId == user.Id);
            return new DevelopmentActorDto(user.Id, user.DisplayName, user.Email, primary is not null && units.TryGetValue(primary.OrgUnitId, out var unit) ? unit.Name : null);
        }).ToList();
    }
}
