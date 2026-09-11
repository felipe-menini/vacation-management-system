using Licenses.Domain.Identity;
using Licenses.Domain.Organization;

namespace Licenses.Application.Organization;

public interface IOrganizationRepository
{
    Task<List<OrgUnit>> ListOrgUnitsAsync(CancellationToken cancellationToken);
    Task<OrgUnit?> GetOrgUnitAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> OrgUnitCodeExistsAsync(string code, Guid? excludingId, CancellationToken cancellationToken);
    Task<bool> OrgUnitExistsAsync(Guid id, CancellationToken cancellationToken);
    Task<List<Guid>> GetOrgUnitAncestorIdsAsync(Guid orgUnitId, CancellationToken cancellationToken);
    Task AddOrgUnitAsync(OrgUnit orgUnit, CancellationToken cancellationToken);

    Task<List<User>> ListUsersAsync(CancellationToken cancellationToken);
    Task<List<User>> ListUsersInOrgUnitsAsync(IReadOnlyCollection<Guid> orgUnitIds, DateTime utcNow, CancellationToken cancellationToken);
    Task<User?> GetUserAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> EmailExistsAsync(string email, Guid? excludingId, CancellationToken cancellationToken);
    Task<bool> ExternalIdentityIdExistsAsync(string externalIdentityId, Guid? excludingId, CancellationToken cancellationToken);
    Task AddUserAsync(User user, CancellationToken cancellationToken);

    Task<List<UserOrgAssignment>> ListAssignmentsAsync(Guid userId, CancellationToken cancellationToken);
    Task<UserOrgAssignment?> GetAssignmentAsync(Guid userId, Guid assignmentId, CancellationToken cancellationToken);
    Task<bool> HasOverlappingPrimaryAssignmentAsync(Guid userId, DateTime effectiveFromUtc, DateTime? effectiveToUtc, Guid? excludingAssignmentId, CancellationToken cancellationToken);
    Task AddAssignmentAsync(UserOrgAssignment assignment, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
