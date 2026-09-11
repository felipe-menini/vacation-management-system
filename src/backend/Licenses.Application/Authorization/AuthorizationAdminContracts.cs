namespace Licenses.Application.Authorization;

public sealed record RoleDto(Guid Id, string Code, string Name, string Description, bool IsActive);

public sealed record RoleScopeAssignmentDto(
    Guid Id,
    Guid UserId,
    Guid RoleId,
    string RoleCode,
    Guid OrgUnitId,
    bool IncludeDescendants,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc);

public sealed record AssignRoleScopeCommand(Guid UserId, Guid RoleId, Guid OrgUnitId, bool IncludeDescendants, DateTime EffectiveFromUtc, DateTime? EffectiveToUtc);
public sealed record UpdateRoleScopeCommand(Guid OrgUnitId, bool IncludeDescendants, DateTime EffectiveFromUtc, DateTime? EffectiveToUtc);
public sealed record RevokeRoleScopeCommand(DateTime EffectiveToUtc);
