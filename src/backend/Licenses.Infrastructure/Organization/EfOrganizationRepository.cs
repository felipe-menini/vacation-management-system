using Licenses.Application.Organization;
using Licenses.Domain.Identity;
using Licenses.Domain.Organization;
using Licenses.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Licenses.Infrastructure.Organization;

public sealed class EfOrganizationRepository(ApplicationDbContext dbContext) : IOrganizationRepository
{
    public Task<List<OrgUnit>> ListOrgUnitsAsync(CancellationToken cancellationToken) =>
        dbContext.OrgUnits.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);

    public Task<OrgUnit?> GetOrgUnitAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.OrgUnits.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> OrgUnitCodeExistsAsync(string code, Guid? excludingId, CancellationToken cancellationToken) =>
        dbContext.OrgUnits.AnyAsync(x => x.Code == code && (excludingId == null || x.Id != excludingId), cancellationToken);

    public Task<bool> OrgUnitExistsAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.OrgUnits.AnyAsync(x => x.Id == id, cancellationToken);

    public async Task<List<Guid>> GetOrgUnitAncestorIdsAsync(Guid orgUnitId, CancellationToken cancellationToken)
    {
        var units = await dbContext.OrgUnits.AsNoTracking().Select(x => new { x.Id, x.ParentId }).ToListAsync(cancellationToken);
        var byId = units.ToDictionary(x => x.Id, x => x.ParentId);
        var ancestors = new List<Guid>();
        var visited = new HashSet<Guid>();
        var current = orgUnitId;
        while (byId.TryGetValue(current, out var parentId) && parentId is not null)
        {
            if (!visited.Add(parentId.Value)) throw new InvalidOperationException("Existing organizational hierarchy contains a cycle.");
            ancestors.Add(parentId.Value);
            current = parentId.Value;
        }
        return ancestors;
    }

    public Task AddOrgUnitAsync(OrgUnit orgUnit, CancellationToken cancellationToken) => dbContext.OrgUnits.AddAsync(orgUnit, cancellationToken).AsTask();

    public Task<List<User>> ListUsersAsync(CancellationToken cancellationToken) =>
        dbContext.Users.AsNoTracking().OrderBy(x => x.DisplayName).ToListAsync(cancellationToken);

    public Task<List<User>> ListUsersInOrgUnitsAsync(IReadOnlyCollection<Guid> orgUnitIds, DateTime utcNow, CancellationToken cancellationToken) =>
        dbContext.Users.AsNoTracking()
            .Where(user => user.IsActive && dbContext.UserOrgAssignments.Any(assignment =>
                assignment.UserId == user.Id
                && orgUnitIds.Contains(assignment.OrgUnitId)
                && assignment.EffectiveFromUtc <= utcNow
                && (assignment.EffectiveToUtc == null || assignment.EffectiveToUtc > utcNow)))
            .OrderBy(x => x.DisplayName)
            .ToListAsync(cancellationToken);

    public Task<User?> GetUserAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Users.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> EmailExistsAsync(string email, Guid? excludingId, CancellationToken cancellationToken) =>
        dbContext.Users.AnyAsync(x => x.Email == email && (excludingId == null || x.Id != excludingId), cancellationToken);

    public Task<bool> ExternalIdentityIdExistsAsync(string externalIdentityId, Guid? excludingId, CancellationToken cancellationToken) =>
        dbContext.Users.AnyAsync(x => x.ExternalIdentityId == externalIdentityId && (excludingId == null || x.Id != excludingId), cancellationToken);

    public Task AddUserAsync(User user, CancellationToken cancellationToken) => dbContext.Users.AddAsync(user, cancellationToken).AsTask();

    public Task<List<UserOrgAssignment>> ListAssignmentsAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.UserOrgAssignments.AsNoTracking().Where(x => x.UserId == userId).OrderByDescending(x => x.EffectiveFromUtc).ToListAsync(cancellationToken);

    public Task<UserOrgAssignment?> GetAssignmentAsync(Guid userId, Guid assignmentId, CancellationToken cancellationToken) =>
        dbContext.UserOrgAssignments.FirstOrDefaultAsync(x => x.UserId == userId && x.Id == assignmentId, cancellationToken);

    public Task<bool> HasOverlappingPrimaryAssignmentAsync(Guid userId, DateTime effectiveFromUtc, DateTime? effectiveToUtc, Guid? excludingAssignmentId, CancellationToken cancellationToken) =>
        dbContext.UserOrgAssignments.AnyAsync(x =>
            x.UserId == userId
            && (excludingAssignmentId == null || x.Id != excludingAssignmentId)
            && x.IsPrimary
            && x.EffectiveFromUtc < (effectiveToUtc ?? DateTime.MaxValue)
            && (x.EffectiveToUtc ?? DateTime.MaxValue) > effectiveFromUtc,
            cancellationToken);

    public Task AddAssignmentAsync(UserOrgAssignment assignment, CancellationToken cancellationToken) => dbContext.UserOrgAssignments.AddAsync(assignment, cancellationToken).AsTask();
    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
