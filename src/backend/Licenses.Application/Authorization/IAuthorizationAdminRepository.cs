using Licenses.Domain.Authorization;
using Licenses.Domain.Identity;
using Licenses.Domain.Organization;

namespace Licenses.Application.Authorization;

public interface IAuthorizationAdminRepository
{
    Task<User?> GetUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<Role?> GetRoleAsync(Guid roleId, CancellationToken cancellationToken);
    Task<List<Role>> ListRolesAsync(CancellationToken cancellationToken);
    Task<OrgUnit?> GetOrgUnitAsync(Guid orgUnitId, CancellationToken cancellationToken);
    Task<RoleScopeAssignment?> GetRoleScopeAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken);
    Task<List<RoleScopeAssignment>> ListRoleScopeAssignmentsAsync(Guid userId, CancellationToken cancellationToken);
    Task<bool> HasOverlappingActiveRoleScopeAssignmentAsync(Guid userId, Guid roleId, Guid orgUnitId, DateTime effectiveFromUtc, DateTime? effectiveToUtc, Guid? excludingAssignmentId, CancellationToken cancellationToken);
    Task AddRoleScopeAssignmentAsync(RoleScopeAssignment assignment, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
