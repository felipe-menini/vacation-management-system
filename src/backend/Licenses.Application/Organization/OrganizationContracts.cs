namespace Licenses.Application.Organization;

public sealed record OrgUnitDto(Guid Id, string Name, string Code, Guid? ParentId, bool IsActive, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);
public sealed record OrgUnitTreeNodeDto(Guid Id, string Name, string Code, bool IsActive, IReadOnlyList<OrgUnitTreeNodeDto> Children);
public sealed record UserDto(Guid Id, string? ExternalIdentityId, string DisplayName, string Email, bool IsActive, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, OrgUnitDto? PrimaryOrgUnit);
public sealed record UserOrgAssignmentDto(Guid Id, Guid UserId, Guid OrgUnitId, string OrgUnitName, bool IsPrimary, DateTime EffectiveFromUtc, DateTime? EffectiveToUtc);
public sealed record CreateOrgUnitCommand(string Name, string Code, Guid? ParentId);
public sealed record UpdateOrgUnitCommand(string Name, string Code, Guid? ParentId, bool IsActive);
public sealed record CreateUserCommand(string DisplayName, string Email, string? ExternalIdentityId);
public sealed record UpdateUserCommand(string DisplayName, string Email, string? ExternalIdentityId, bool IsActive);
public sealed record CreateUserOrgAssignmentCommand(Guid OrgUnitId, bool IsPrimary, DateTime EffectiveFromUtc, DateTime? EffectiveToUtc);
