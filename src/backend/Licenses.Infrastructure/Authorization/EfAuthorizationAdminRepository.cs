using Licenses.Application.Authorization;
using Licenses.Domain.Authorization;
using Licenses.Domain.Identity;
using Licenses.Domain.Organization;
using Licenses.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Licenses.Infrastructure.Authorization;

public sealed class EfAuthorizationAdminRepository(ApplicationDbContext dbContext) : IAuthorizationAdminRepository
{
    public Task<User?> GetUserAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

    public Task<Role?> GetRoleAsync(Guid roleId, CancellationToken cancellationToken) =>
        dbContext.Roles.FirstOrDefaultAsync(x => x.Id == roleId, cancellationToken);

    public Task<List<Role>> ListRolesAsync(CancellationToken cancellationToken) =>
        dbContext.Roles.AsNoTracking().OrderBy(x => x.Code).ToListAsync(cancellationToken);

    public Task<OrgUnit?> GetOrgUnitAsync(Guid orgUnitId, CancellationToken cancellationToken) =>
        dbContext.OrgUnits.FirstOrDefaultAsync(x => x.Id == orgUnitId, cancellationToken);

    public Task<RoleScopeAssignment?> GetRoleScopeAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken) =>
        dbContext.RoleScopeAssignments.FirstOrDefaultAsync(x => x.Id == assignmentId, cancellationToken);

    public Task<List<RoleScopeAssignment>> ListRoleScopeAssignmentsAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.RoleScopeAssignments.AsNoTracking().Where(x => x.UserId == userId).OrderByDescending(x => x.EffectiveFromUtc).ToListAsync(cancellationToken);

    public Task<bool> HasOverlappingActiveRoleScopeAssignmentAsync(Guid userId, Guid roleId, Guid orgUnitId, DateTime effectiveFromUtc, DateTime? effectiveToUtc, Guid? excludingAssignmentId, CancellationToken cancellationToken) =>
        dbContext.RoleScopeAssignments.AnyAsync(x =>
            x.UserId == userId
            && x.RoleId == roleId
            && x.OrgUnitId == orgUnitId
            && (excludingAssignmentId == null || x.Id != excludingAssignmentId)
            && x.EffectiveFromUtc < (effectiveToUtc ?? DateTime.MaxValue)
            && (x.EffectiveToUtc ?? DateTime.MaxValue) > effectiveFromUtc,
            cancellationToken);

    public Task AddRoleScopeAssignmentAsync(RoleScopeAssignment assignment, CancellationToken cancellationToken) =>
        dbContext.RoleScopeAssignments.AddAsync(assignment, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
