namespace Licenses.Application.Authorization;

public sealed class AuthorizationService(IAuthorizationRepository repository, TimeProvider timeProvider)
{
    public async Task<bool> CanUserPerformGlobalAsync(Guid actorUserId, string permissionCode, CancellationToken cancellationToken)
    {
        var now = UtcNow();
        var actor = await repository.GetUserAsync(actorUserId, cancellationToken);
        if (actor is null || !actor.IsActive) return false;

        var assignments = await repository.ListActiveRoleScopeAssignmentsAsync(actorUserId, now, cancellationToken);
        foreach (var assignment in assignments)
        {
            if (!await repository.IsRoleActiveAsync(assignment.RoleId, cancellationToken)) continue;
            if (await repository.RoleHasPermissionAsync(assignment.RoleId, NormalizePermissionCode(permissionCode), cancellationToken)) return true;
        }

        return false;
    }
    public async Task<bool> CanUserPerformAsync(Guid actorUserId, string permissionCode, Guid targetOrgUnitId, CancellationToken cancellationToken)
    {
        var allowedOrgUnits = await GetAuthorizedOrgUnitIdsAsync(actorUserId, permissionCode, cancellationToken);
        return allowedOrgUnits.Contains(targetOrgUnitId);
    }

    public async Task<IReadOnlySet<Guid>> GetAuthorizedOrgUnitIdsAsync(Guid actorUserId, string permissionCode, CancellationToken cancellationToken)
    {
        var now = UtcNow();
        var actor = await repository.GetUserAsync(actorUserId, cancellationToken);
        if (actor is null || !actor.IsActive) return new HashSet<Guid>();

        var units = await repository.ListOrgUnitsAsync(cancellationToken);
        var activeUnitIds = units.Where(x => x.IsActive).Select(x => x.Id).ToHashSet();
        var childrenByParent = units.Where(x => x.ParentId is not null).GroupBy(x => x.ParentId!.Value).ToDictionary(x => x.Key, x => x.Select(y => y.Id).ToList());
        var assignments = await repository.ListActiveRoleScopeAssignmentsAsync(actorUserId, now, cancellationToken);
        var result = new HashSet<Guid>();

        foreach (var assignment in assignments)
        {
            if (!activeUnitIds.Contains(assignment.OrgUnitId)) continue;
            if (!await repository.IsRoleActiveAsync(assignment.RoleId, cancellationToken)) continue;
            if (!await repository.RoleHasPermissionAsync(assignment.RoleId, NormalizePermissionCode(permissionCode), cancellationToken)) continue;

            result.Add(assignment.OrgUnitId);
            if (assignment.IncludeDescendants) AddDescendants(assignment.OrgUnitId, childrenByParent, activeUnitIds, result);
        }

        return result;
    }


    public async Task<bool> CanUserReadGlobalScopedResourceAsync(Guid actorUserId, string permissionCode, CancellationToken cancellationToken)
    {
        var allowedOrgUnits = await GetAuthorizedOrgUnitIdsAsync(actorUserId, permissionCode, cancellationToken);
        if (allowedOrgUnits.Count == 0) return false;
        var rootOrgUnits = (await repository.ListOrgUnitsAsync(cancellationToken)).Where(x => x.IsActive && x.ParentId is null).Select(x => x.Id).ToHashSet();
        return rootOrgUnits.Any(allowedOrgUnits.Contains);
    }
    public async Task<bool> CanAccessUserAsync(Guid actorUserId, string permissionCode, Guid targetUserId, CancellationToken cancellationToken)
    {
        var allowedOrgUnits = await GetAuthorizedOrgUnitIdsAsync(actorUserId, permissionCode, cancellationToken);
        if (allowedOrgUnits.Count == 0) return false;
        var now = UtcNow();
        var targetAssignments = await repository.ListActiveUserOrgAssignmentsAsync(targetUserId, now, cancellationToken);
        return targetAssignments.Any(x => allowedOrgUnits.Contains(x.OrgUnitId));
    }

    public async Task<IReadOnlyList<DevelopmentActorDto>> ListDevelopmentActorsAsync(CancellationToken cancellationToken) =>
        await repository.ListDevelopmentActorsAsync(UtcNow(), cancellationToken);

    private static void AddDescendants(Guid orgUnitId, IReadOnlyDictionary<Guid, List<Guid>> childrenByParent, IReadOnlySet<Guid> activeUnitIds, HashSet<Guid> result)
    {
        if (!childrenByParent.TryGetValue(orgUnitId, out var children)) return;
        foreach (var child in children)
        {
            if (!activeUnitIds.Contains(child)) continue;
            if (result.Add(child)) AddDescendants(child, childrenByParent, activeUnitIds, result);
        }
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;
    private static string NormalizePermissionCode(string code) => code.Trim().ToLowerInvariant();
}