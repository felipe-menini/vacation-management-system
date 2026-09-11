using Licenses.Application.Audit;
using Licenses.Domain.Authorization;

namespace Licenses.Application.Authorization;

public sealed class AuthorizationAdminService(IAuthorizationAdminRepository repository, TimeProvider timeProvider, IAuditWriter auditWriter)
{

    public async Task<IReadOnlyList<RoleDto>> ListRolesAsync(CancellationToken cancellationToken) =>
        (await repository.ListRolesAsync(cancellationToken)).Select(ToDto).ToList();

    public async Task<IReadOnlyList<RoleScopeAssignmentDto>?> ListRoleScopesAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (await repository.GetUserAsync(userId, cancellationToken) is null) return null;
        var roles = (await repository.ListRolesAsync(cancellationToken)).ToDictionary(x => x.Id, x => x.Code);
        var assignments = await repository.ListRoleScopeAssignmentsAsync(userId, cancellationToken);
        return assignments.Select(x => ToDto(x, roles.TryGetValue(x.RoleId, out var code) ? code : string.Empty)).ToList();
    }

    public async Task<RoleScopeAssignmentDto> AssignRoleScopeAsync(AssignRoleScopeCommand command, CancellationToken cancellationToken, Guid? actorUserId = null)
    {
        var user = await repository.GetUserAsync(command.UserId, cancellationToken) ?? throw new InvalidOperationException("User does not exist.");
        if (!user.IsActive) throw new InvalidOperationException("User must be active.");
        var role = await repository.GetRoleAsync(command.RoleId, cancellationToken) ?? throw new InvalidOperationException("Role does not exist.");
        if (!role.IsActive) throw new InvalidOperationException("Role must be active.");
        var orgUnit = await repository.GetOrgUnitAsync(command.OrgUnitId, cancellationToken) ?? throw new InvalidOperationException("Organizational unit does not exist.");
        if (!orgUnit.IsActive) throw new InvalidOperationException("Organizational unit must be active.");
        await EnsureNoDuplicateActiveAssignmentAsync(command.UserId, command.RoleId, command.OrgUnitId, command.EffectiveFromUtc, command.EffectiveToUtc, excludingAssignmentId: null, cancellationToken);

        var assignment = RoleScopeAssignment.Create(command.UserId, command.RoleId, command.OrgUnitId, command.IncludeDescendants, command.EffectiveFromUtc, command.EffectiveToUtc);
        await repository.AddRoleScopeAssignmentAsync(assignment, cancellationToken);
        await auditWriter.WriteAsync(new AuditEventData(
            actorUserId,
            "authorization.role_scope.assign",
            "RoleScopeAssignment",
            assignment.Id,
            assignment.UserId,
            assignment.OrgUnitId,
            null,
            UtcNow(),
            AuditMetadataJson.Serialize(new
            {
                subjectUserId = assignment.UserId,
                roleId = assignment.RoleId,
                roleCode = role.Code,
                orgUnitId = assignment.OrgUnitId,
                includeDescendants = assignment.IncludeDescendants,
                effectiveFromUtc = assignment.EffectiveFromUtc,
                effectiveToUtc = assignment.EffectiveToUtc
            })),
            cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return ToDto(assignment, role.Code);
    }

    public async Task<RoleScopeAssignmentDto?> UpdateRoleScopeAsync(Guid assignmentId, UpdateRoleScopeCommand command, CancellationToken cancellationToken, Guid? actorUserId = null)
    {
        var assignment = await repository.GetRoleScopeAssignmentAsync(assignmentId, cancellationToken);
        if (assignment is null) return null;
        var user = await repository.GetUserAsync(assignment.UserId, cancellationToken) ?? throw new InvalidOperationException("User does not exist.");
        if (!user.IsActive) throw new InvalidOperationException("User must be active.");
        var role = await repository.GetRoleAsync(assignment.RoleId, cancellationToken) ?? throw new InvalidOperationException("Role does not exist.");
        if (!role.IsActive) throw new InvalidOperationException("Role must be active.");
        var orgUnit = await repository.GetOrgUnitAsync(command.OrgUnitId, cancellationToken) ?? throw new InvalidOperationException("Organizational unit does not exist.");
        if (!orgUnit.IsActive) throw new InvalidOperationException("Organizational unit must be active.");
        await EnsureNoDuplicateActiveAssignmentAsync(assignment.UserId, assignment.RoleId, command.OrgUnitId, command.EffectiveFromUtc, command.EffectiveToUtc, assignment.Id, cancellationToken);

        var previousOrgUnitId = assignment.OrgUnitId;
        var previousIncludeDescendants = assignment.IncludeDescendants;
        var previousEffectiveFromUtc = assignment.EffectiveFromUtc;
        var previousEffectiveToUtc = assignment.EffectiveToUtc;
        assignment.UpdateScope(command.OrgUnitId, command.IncludeDescendants, command.EffectiveFromUtc, command.EffectiveToUtc);
        await auditWriter.WriteAsync(new AuditEventData(
            actorUserId,
            "authorization.role_scope.update",
            "RoleScopeAssignment",
            assignment.Id,
            assignment.UserId,
            assignment.OrgUnitId,
            null,
            UtcNow(),
            AuditMetadataJson.Serialize(new
            {
                subjectUserId = assignment.UserId,
                roleId = assignment.RoleId,
                roleCode = role.Code,
                previousOrgUnitId,
                orgUnitId = assignment.OrgUnitId,
                previousIncludeDescendants,
                includeDescendants = assignment.IncludeDescendants,
                previousEffectiveFromUtc,
                effectiveFromUtc = assignment.EffectiveFromUtc,
                previousEffectiveToUtc,
                effectiveToUtc = assignment.EffectiveToUtc
            })),
            cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return ToDto(assignment, role.Code);
    }

    public async Task<RoleScopeAssignmentDto?> RevokeRoleScopeAsync(Guid assignmentId, RevokeRoleScopeCommand command, CancellationToken cancellationToken, Guid? actorUserId = null)
    {
        var assignment = await repository.GetRoleScopeAssignmentAsync(assignmentId, cancellationToken);
        if (assignment is null) return null;
        var role = await repository.GetRoleAsync(assignment.RoleId, cancellationToken) ?? throw new InvalidOperationException("Role does not exist.");

        var previousEffectiveToUtc = assignment.EffectiveToUtc;
        assignment.Revoke(command.EffectiveToUtc);
        await auditWriter.WriteAsync(new AuditEventData(
            actorUserId,
            "authorization.role_scope.revoke",
            "RoleScopeAssignment",
            assignment.Id,
            assignment.UserId,
            assignment.OrgUnitId,
            null,
            UtcNow(),
            AuditMetadataJson.Serialize(new
            {
                subjectUserId = assignment.UserId,
                roleId = assignment.RoleId,
                roleCode = role.Code,
                orgUnitId = assignment.OrgUnitId,
                includeDescendants = assignment.IncludeDescendants,
                previousEffectiveToUtc,
                effectiveToUtc = assignment.EffectiveToUtc
            })),
            cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return ToDto(assignment, role.Code);
    }

    private async Task EnsureNoDuplicateActiveAssignmentAsync(Guid userId, Guid roleId, Guid orgUnitId, DateTime effectiveFromUtc, DateTime? effectiveToUtc, Guid? excludingAssignmentId, CancellationToken cancellationToken)
    {
        if (await repository.HasOverlappingActiveRoleScopeAssignmentAsync(userId, roleId, orgUnitId, effectiveFromUtc, effectiveToUtc, excludingAssignmentId, cancellationToken))
            throw new InvalidOperationException("An active role scope assignment already exists for the same user, role, and organizational unit.");
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;
    private static RoleDto ToDto(Role role) => new(role.Id, role.Code, role.Name, role.Description, role.IsActive);
    private static RoleScopeAssignmentDto ToDto(RoleScopeAssignment assignment, string roleCode) =>
        new(assignment.Id, assignment.UserId, assignment.RoleId, roleCode, assignment.OrgUnitId, assignment.IncludeDescendants, assignment.EffectiveFromUtc, assignment.EffectiveToUtc);
}
