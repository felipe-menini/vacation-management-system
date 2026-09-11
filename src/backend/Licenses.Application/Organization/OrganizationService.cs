using Licenses.Application.Audit;
using Licenses.Domain.Identity;
using Licenses.Domain.Organization;

namespace Licenses.Application.Organization;

public sealed class OrganizationService(IOrganizationRepository repository, TimeProvider timeProvider, IAuditWriter auditWriter)
{
    public async Task<IReadOnlyList<OrgUnitDto>> ListOrgUnitsAsync(CancellationToken cancellationToken)
    {
        var units = await repository.ListOrgUnitsAsync(cancellationToken);
        return units.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<OrgUnitDto>> ListOrgUnitsAsync(IReadOnlySet<Guid> allowedOrgUnitIds, CancellationToken cancellationToken)
    {
        var units = await repository.ListOrgUnitsAsync(cancellationToken);
        return units.Where(x => allowedOrgUnitIds.Contains(x.Id)).Select(ToDto).ToList();
    }

    public async Task<OrgUnitDto?> GetOrgUnitAsync(Guid id, CancellationToken cancellationToken)
    {
        var unit = await repository.GetOrgUnitAsync(id, cancellationToken);
        return unit is null ? null : ToDto(unit);
    }

    public async Task<IReadOnlyList<OrgUnitTreeNodeDto>> GetOrgUnitTreeAsync(CancellationToken cancellationToken)
    {
        var units = await repository.ListOrgUnitsAsync(cancellationToken);
        return BuildTree(units, null);
    }

    public async Task<IReadOnlyList<OrgUnitTreeNodeDto>> GetOrgUnitTreeAsync(IReadOnlySet<Guid> allowedOrgUnitIds, CancellationToken cancellationToken)
    {
        var units = (await repository.ListOrgUnitsAsync(cancellationToken)).Where(x => allowedOrgUnitIds.Contains(x.Id)).ToList();
        return BuildFilteredTree(units);
    }

    public async Task<OrgUnitDto> CreateOrgUnitAsync(CreateOrgUnitCommand command, CancellationToken cancellationToken, Guid? actorUserId = null)
    {
        await ValidateOrgUnitAsync(command.Code, command.ParentId, excludingId: null, cancellationToken);
        var unit = OrgUnit.Create(command.Name, command.Code, command.ParentId, UtcNow());
        await repository.AddOrgUnitAsync(unit, cancellationToken);
        await auditWriter.WriteAsync(new AuditEventData(
            actorUserId,
            "organization.unit.create",
            "OrgUnit",
            unit.Id,
            null,
            unit.Id,
            null,
            UtcNow(),
            AuditMetadataJson.Serialize(new
            {
                orgUnitId = unit.Id,
                parentOrgUnitId = unit.ParentId,
                code = unit.Code,
                isActive = unit.IsActive
            })),
            cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return ToDto(unit);
    }

    public async Task<OrgUnitDto?> UpdateOrgUnitAsync(Guid id, UpdateOrgUnitCommand command, CancellationToken cancellationToken, Guid? actorUserId = null)
    {
        var unit = await repository.GetOrgUnitAsync(id, cancellationToken);
        if (unit is null) return null;
        await ValidateOrgUnitAsync(command.Code, command.ParentId, id, cancellationToken);
        if (command.ParentId == id) throw new InvalidOperationException("An organizational unit cannot be its own parent.");
        if (command.ParentId is not null)
        {
            var ancestorIds = await repository.GetOrgUnitAncestorIdsAsync(command.ParentId.Value, cancellationToken);
            if (ancestorIds.Contains(id)) throw new InvalidOperationException("Organizational hierarchy cycles are not allowed.");
        }
        var previousParentId = unit.ParentId;
        var previousCode = unit.Code;
        var previousIsActive = unit.IsActive;
        unit.Update(command.Name, command.Code, command.ParentId, command.IsActive, UtcNow());
        await auditWriter.WriteAsync(new AuditEventData(
            actorUserId,
            previousIsActive && !unit.IsActive ? "organization.unit.deactivate" : "organization.unit.update",
            "OrgUnit",
            unit.Id,
            null,
            unit.Id,
            null,
            UtcNow(),
            AuditMetadataJson.Serialize(new
            {
                orgUnitId = unit.Id,
                previousParentOrgUnitId = previousParentId,
                parentOrgUnitId = unit.ParentId,
                previousCode,
                code = unit.Code,
                previousIsActive,
                isActive = unit.IsActive
            })),
            cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return ToDto(unit);
    }

    public async Task<IReadOnlyList<UserDto>> ListUsersAsync(CancellationToken cancellationToken)
    {
        var users = await repository.ListUsersAsync(cancellationToken);
        return await ToUserDtosAsync(users, cancellationToken);
    }

    public async Task<IReadOnlyList<UserDto>> ListUsersAsync(IReadOnlySet<Guid> allowedOrgUnitIds, CancellationToken cancellationToken)
    {
        if (allowedOrgUnitIds.Count == 0) return [];
        var users = await repository.ListUsersInOrgUnitsAsync(allowedOrgUnitIds.ToList(), UtcNow(), cancellationToken);
        return await ToUserDtosAsync(users, cancellationToken);
    }

    public async Task<UserDto?> GetUserAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await repository.GetUserAsync(id, cancellationToken);
        if (user is null) return null;
        return (await ToUserDtosAsync([user], cancellationToken)).Single();
    }

    public async Task<UserDto> CreateUserAsync(CreateUserCommand command, CancellationToken cancellationToken)
    {
        await ValidateExternalIdentityAsync(command.ExternalIdentityId, excludingId: null, cancellationToken);
        var user = User.Create(command.DisplayName, command.Email, command.ExternalIdentityId, UtcNow());
        await repository.AddUserAsync(user, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return ToDto(user, null);
    }

    public async Task<UserDto?> UpdateUserAsync(Guid id, UpdateUserCommand command, CancellationToken cancellationToken)
    {
        var user = await repository.GetUserAsync(id, cancellationToken);
        if (user is null) return null;
        await ValidateExternalIdentityAsync(command.ExternalIdentityId, id, cancellationToken);
        user.Update(command.DisplayName, command.Email, command.ExternalIdentityId, command.IsActive, UtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return (await ToUserDtosAsync([user], cancellationToken)).Single();
    }

    public async Task<IReadOnlyList<UserOrgAssignmentDto>?> ListAssignmentsAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (await repository.GetUserAsync(userId, cancellationToken) is null) return null;
        var units = (await repository.ListOrgUnitsAsync(cancellationToken)).ToDictionary(x => x.Id);
        var assignments = await repository.ListAssignmentsAsync(userId, cancellationToken);
        return assignments.Select(x => ToDto(x, units[x.OrgUnitId].Name)).ToList();
    }

    public async Task<UserOrgAssignmentDto?> CreateAssignmentAsync(Guid userId, CreateUserOrgAssignmentCommand command, CancellationToken cancellationToken, Guid? actorUserId = null)
    {
        if (await repository.GetUserAsync(userId, cancellationToken) is null) return null;
        if (!await repository.OrgUnitExistsAsync(command.OrgUnitId, cancellationToken)) throw new InvalidOperationException("Organizational unit does not exist.");
        if (command.IsPrimary && await repository.HasOverlappingPrimaryAssignmentAsync(userId, command.EffectiveFromUtc, command.EffectiveToUtc, cancellationToken))
        {
            throw new InvalidOperationException("A user can have at most one active primary organizational assignment.");
        }
        var assignment = UserOrgAssignment.Create(userId, command.OrgUnitId, command.IsPrimary, command.EffectiveFromUtc, command.EffectiveToUtc);
        await repository.AddAssignmentAsync(assignment, cancellationToken);
        await auditWriter.WriteAsync(new AuditEventData(
            actorUserId,
            "organization.assignment.create",
            "UserOrgAssignment",
            assignment.Id,
            userId,
            assignment.OrgUnitId,
            null,
            UtcNow(),
            AuditMetadataJson.Serialize(new
            {
                orgUnitId = assignment.OrgUnitId,
                isPrimary = assignment.IsPrimary,
                effectiveFromUtc = assignment.EffectiveFromUtc,
                effectiveToUtc = assignment.EffectiveToUtc
            })),
            cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        var unit = await repository.GetOrgUnitAsync(command.OrgUnitId, cancellationToken);
        return ToDto(assignment, unit?.Name ?? string.Empty);
    }

    private async Task<List<UserDto>> ToUserDtosAsync(IReadOnlyCollection<User> users, CancellationToken cancellationToken)
    {
        var units = (await repository.ListOrgUnitsAsync(cancellationToken)).ToDictionary(x => x.Id);
        var result = new List<UserDto>();
        foreach (var user in users)
        {
            var primary = (await repository.ListAssignmentsAsync(user.Id, cancellationToken))
                .Where(x => x.IsPrimary && x.IsActiveAt(UtcNow()))
                .OrderByDescending(x => x.EffectiveFromUtc)
                .FirstOrDefault();
            result.Add(ToDto(user, primary is not null && units.TryGetValue(primary.OrgUnitId, out var unit) ? ToDto(unit) : null));
        }
        return result;
    }

    private async Task ValidateOrgUnitAsync(string code, Guid? parentId, Guid? excludingId, CancellationToken cancellationToken)
    {
        if (await repository.OrgUnitCodeExistsAsync(code.Trim().ToUpperInvariant(), excludingId, cancellationToken))
            throw new InvalidOperationException("Organizational unit code must be unique.");
        if (parentId is not null && !await repository.OrgUnitExistsAsync(parentId.Value, cancellationToken))
            throw new InvalidOperationException("Parent organizational unit does not exist.");
    }

    private async Task ValidateExternalIdentityAsync(string? externalIdentityId, Guid? excludingId, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(externalIdentityId) && await repository.ExternalIdentityIdExistsAsync(externalIdentityId.Trim(), excludingId, cancellationToken))
            throw new InvalidOperationException("External identity id must be unique when present.");
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;
    private static OrgUnitDto ToDto(OrgUnit unit) => new(unit.Id, unit.Name, unit.Code, unit.ParentId, unit.IsActive, unit.CreatedAtUtc, unit.UpdatedAtUtc);
    private static UserDto ToDto(User user, OrgUnitDto? primaryOrgUnit) => new(user.Id, user.ExternalIdentityId, user.DisplayName, user.Email, user.IsActive, user.CreatedAtUtc, user.UpdatedAtUtc, primaryOrgUnit);
    private static UserOrgAssignmentDto ToDto(UserOrgAssignment assignment, string orgUnitName) => new(assignment.Id, assignment.UserId, assignment.OrgUnitId, orgUnitName, assignment.IsPrimary, assignment.EffectiveFromUtc, assignment.EffectiveToUtc);

    private static List<OrgUnitTreeNodeDto> BuildTree(IReadOnlyCollection<OrgUnit> units, Guid? parentId) =>
        units.Where(x => x.ParentId == parentId)
            .OrderBy(x => x.Name)
            .Select(x => new OrgUnitTreeNodeDto(x.Id, x.Name, x.Code, x.IsActive, BuildTree(units, x.Id)))
            .ToList();

    private static List<OrgUnitTreeNodeDto> BuildFilteredTree(IReadOnlyCollection<OrgUnit> units)
    {
        var unitIds = units.Select(x => x.Id).ToHashSet();
        var roots = units.Where(x => x.ParentId is null || !unitIds.Contains(x.ParentId.Value)).Select(x => x.ParentId).Distinct().ToList();
        return roots.SelectMany(parentId => BuildTree(units, parentId)).ToList();
    }
}
