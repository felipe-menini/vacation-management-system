using Licenses.Domain.Authorization;
using Licenses.Domain.Identity;
using Licenses.Domain.Organization;

namespace Licenses.Application.Authorization;

public interface IAuthorizationRepository
{
    Task<User?> GetUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<OrgUnit?> GetOrgUnitAsync(Guid orgUnitId, CancellationToken cancellationToken);
    Task<List<OrgUnit>> ListOrgUnitsAsync(CancellationToken cancellationToken);
    Task<List<UserOrgAssignment>> ListActiveUserOrgAssignmentsAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken);
    Task<List<RoleScopeAssignment>> ListActiveRoleScopeAssignmentsAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken);
    Task<bool> RoleHasPermissionAsync(Guid roleId, string permissionCode, CancellationToken cancellationToken);
    Task<bool> IsRoleActiveAsync(Guid roleId, CancellationToken cancellationToken);
    Task<List<User>> ListUsersInOrgUnitsAsync(IReadOnlyCollection<Guid> orgUnitIds, DateTime utcNow, CancellationToken cancellationToken);
    Task<List<DevelopmentActorDto>> ListDevelopmentActorsAsync(DateTime utcNow, CancellationToken cancellationToken);
}
