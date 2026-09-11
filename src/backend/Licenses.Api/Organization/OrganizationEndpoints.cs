using Licenses.Application.Authorization;
using Licenses.Application.Organization;

namespace Licenses.Api.Organization;

public static class OrganizationEndpoints
{
    public static IEndpointRouteBuilder MapOrganizationEndpoints(this IEndpointRouteBuilder app)
    {
        var orgUnits = app.MapGroup("/api/org-units").WithTags("Organization");
        orgUnits.MapGet("/", async (ICurrentActor actor, AuthorizationService authorization, OrganizationService service, CancellationToken cancellationToken) =>
        {
            var actorId = actor.UserId;
            if (actorId is null) return Results.Unauthorized();
            var allowed = await authorization.GetAuthorizedOrgUnitIdsAsync(actorId.Value, PermissionCodes.OrgUnitsRead, cancellationToken);
            if (allowed.Count == 0) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return Results.Ok(await service.ListOrgUnitsAsync(allowed, cancellationToken));
        });
        orgUnits.MapGet("/tree", async (ICurrentActor actor, AuthorizationService authorization, OrganizationService service, CancellationToken cancellationToken) =>
        {
            var actorId = actor.UserId;
            if (actorId is null) return Results.Unauthorized();
            var allowed = await authorization.GetAuthorizedOrgUnitIdsAsync(actorId.Value, PermissionCodes.OrgUnitsRead, cancellationToken);
            if (allowed.Count == 0) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return Results.Ok(await service.GetOrgUnitTreeAsync(allowed, cancellationToken));
        });
        orgUnits.MapGet("/{id:guid}", async (Guid id, ICurrentActor actor, AuthorizationService authorization, OrganizationService service, CancellationToken cancellationToken) =>
        {
            var actorId = actor.UserId;
            if (actorId is null) return Results.Unauthorized();
            if (!await authorization.CanUserPerformAsync(actorId.Value, PermissionCodes.OrgUnitsRead, id, cancellationToken)) return Results.NotFound();
            return await service.GetOrgUnitAsync(id, cancellationToken) is { } unit ? Results.Ok(unit) : Results.NotFound();
        });
        orgUnits.MapPost("/", async (CreateOrgUnitCommand command, ICurrentActor actor, AuthorizationService authorization, OrganizationService service, CancellationToken cancellationToken) =>
        {
            var actorId = actor.UserId;
            if (actorId is null) return Results.Unauthorized();
            if (command.ParentId is null || !await authorization.CanUserPerformAsync(actorId.Value, PermissionCodes.OrgUnitsManage, command.ParentId.Value, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var created = await service.CreateOrgUnitAsync(command, cancellationToken, actorId.Value);
            return Results.Created($"/api/org-units/{created.Id}", created);
        });
        orgUnits.MapPut("/{id:guid}", async (Guid id, UpdateOrgUnitCommand command, ICurrentActor actor, AuthorizationService authorization, OrganizationService service, CancellationToken cancellationToken) =>
        {
            var actorId = actor.UserId;
            if (actorId is null) return Results.Unauthorized();
            if (!await authorization.CanUserPerformAsync(actorId.Value, PermissionCodes.OrgUnitsManage, id, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (command.ParentId is not null && !await authorization.CanUserPerformAsync(actorId.Value, PermissionCodes.OrgUnitsManage, command.ParentId.Value, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return await service.UpdateOrgUnitAsync(id, command, cancellationToken, actorId.Value) is { } updated ? Results.Ok(updated) : Results.NotFound();
        });

        var users = app.MapGroup("/api/users").WithTags("Users");
        users.MapGet("/", async (ICurrentActor actor, AuthorizationService authorization, OrganizationService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            var allowed = await authorization.GetAuthorizedOrgUnitIdsAsync(actorId, PermissionCodes.OrgUsersRead, cancellationToken);
            if (allowed.Count == 0) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return Results.Ok(await service.ListUsersAsync(allowed, cancellationToken));
        });
        users.MapGet("/{id:guid}", async (Guid id, ICurrentActor actor, AuthorizationService authorization, OrganizationService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanAccessUserAsync(actorId, PermissionCodes.OrgUsersRead, id, cancellationToken)) return Results.NotFound();
            return await service.GetUserAsync(id, cancellationToken) is { } user ? Results.Ok(user) : Results.NotFound();
        });
        users.MapPost("/", async (AdminUserWriteRequest request, ICurrentActor actor, AuthorizationService authorization, OrganizationService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanUserPerformAtRootAsync(actorId, PermissionCodes.OrgUsersManage, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            try
            {
                var created = await service.CreateUserAsync(new CreateUserCommand(request.DisplayName, request.Email, null), cancellationToken, actorId);
                return Results.Created($"/api/users/{created.Id}", created);
            }
            catch (InvalidOperationException ex) when (IsConflict(ex)) { return Results.Conflict(new { error = ex.Message }); }
        });
        users.MapPut("/{id:guid}", async (Guid id, AdminUserWriteRequest request, ICurrentActor actor, AuthorizationService authorization, OrganizationService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanUserPerformAtRootAsync(actorId, PermissionCodes.OrgUsersManage, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var existing = await service.GetUserAsync(id, cancellationToken);
            if (existing is null) return Results.NotFound();
            try
            {
                var updated = await service.UpdateUserAsync(id, new UpdateUserCommand(request.DisplayName, request.Email, existing.ExternalIdentityId, existing.IsActive), cancellationToken, actorId);
                return Results.Ok(updated);
            }
            catch (InvalidOperationException ex) when (IsConflict(ex)) { return Results.Conflict(new { error = ex.Message }); }
        });
        users.MapPost("/{id:guid}/activate", async (Guid id, ICurrentActor actor, AuthorizationService authorization, OrganizationService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanUserPerformAtRootAsync(actorId, PermissionCodes.OrgUsersManage, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var existing = await service.GetUserAsync(id, cancellationToken);
            if (existing is null) return Results.NotFound();
            return Results.Ok(await service.UpdateUserAsync(id, new UpdateUserCommand(existing.DisplayName, existing.Email, existing.ExternalIdentityId, true), cancellationToken, actorId));
        });
        users.MapPost("/{id:guid}/deactivate", async (Guid id, ICurrentActor actor, AuthorizationService authorization, OrganizationService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanUserPerformAtRootAsync(actorId, PermissionCodes.OrgUsersManage, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var existing = await service.GetUserAsync(id, cancellationToken);
            if (existing is null) return Results.NotFound();
            return Results.Ok(await service.UpdateUserAsync(id, new UpdateUserCommand(existing.DisplayName, existing.Email, existing.ExternalIdentityId, false), cancellationToken, actorId));
        });
        users.MapGet("/{id:guid}/org-assignments", async (Guid id, ICurrentActor actor, AuthorizationService authorization, OrganizationService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanAccessUserAsync(actorId, PermissionCodes.OrgAssignmentsRead, id, cancellationToken)) return Results.NotFound();
            return await service.ListAssignmentsAsync(id, cancellationToken) is { } assignments ? Results.Ok(assignments) : Results.NotFound();
        });
        users.MapPost("/{id:guid}/org-assignments", async (Guid id, CreateUserOrgAssignmentCommand command, ICurrentActor actor, AuthorizationService authorization, OrganizationService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanUserPerformAsync(actorId, PermissionCodes.OrgAssignmentsManage, command.OrgUnitId, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            try
            {
                return await service.CreateAssignmentAsync(id, command, cancellationToken, actorId) is { } created ? Results.Created($"/api/users/{id}/org-assignments/{created.Id}", created) : Results.NotFound();
            }
            catch (InvalidOperationException ex) when (IsConflict(ex)) { return Results.Conflict(new { error = ex.Message }); }
        });
        users.MapPut("/{id:guid}/org-assignments/{assignmentId:guid}", async (Guid id, Guid assignmentId, UpdateUserOrgAssignmentCommand command, ICurrentActor actor, AuthorizationService authorization, OrganizationService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            var assignments = await service.ListAssignmentsAsync(id, cancellationToken);
            var existing = assignments?.SingleOrDefault(x => x.Id == assignmentId);
            if (assignments is null || existing is null) return Results.NotFound();
            if (!await authorization.CanUserPerformAsync(actorId, PermissionCodes.OrgAssignmentsManage, existing.OrgUnitId, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (!await authorization.CanUserPerformAsync(actorId, PermissionCodes.OrgAssignmentsManage, command.OrgUnitId, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            try
            {
                return await service.UpdateAssignmentAsync(id, assignmentId, command, cancellationToken, actorId) is { } updated ? Results.Ok(updated) : Results.NotFound();
            }
            catch (InvalidOperationException ex) when (IsConflict(ex)) { return Results.Conflict(new { error = ex.Message }); }
        });
        users.MapPost("/{id:guid}/org-assignments/{assignmentId:guid}/end", async (Guid id, Guid assignmentId, EndUserOrgAssignmentCommand command, ICurrentActor actor, AuthorizationService authorization, OrganizationService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            var assignments = await service.ListAssignmentsAsync(id, cancellationToken);
            var existing = assignments?.SingleOrDefault(x => x.Id == assignmentId);
            if (assignments is null || existing is null) return Results.NotFound();
            if (!await authorization.CanUserPerformAsync(actorId, PermissionCodes.OrgAssignmentsManage, existing.OrgUnitId, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return await service.EndAssignmentAsync(id, assignmentId, command, cancellationToken, actorId) is { } ended ? Results.Ok(ended) : Results.NotFound();
        });

        users.MapGet("/{id:guid}/role-scopes", async (Guid id, ICurrentActor actor, AuthorizationService authorization, AuthorizationAdminService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.AuthorizationRoleScopesManage, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return await service.ListRoleScopesAsync(id, cancellationToken) is { } assignments ? Results.Ok(assignments) : Results.NotFound();
        });
        users.MapPost("/{id:guid}/role-scopes", async (Guid id, AssignRoleScopeRequest request, ICurrentActor actor, AuthorizationService authorization, AuthorizationAdminService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (id == actorId) return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (!await authorization.CanUserAdministerScopeAsync(actorId, PermissionCodes.AuthorizationRoleScopesManage, request.OrgUnitId, request.IncludeDescendants, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            try
            {
                var created = await service.AssignRoleScopeAsync(new AssignRoleScopeCommand(id, request.RoleId, request.OrgUnitId, request.IncludeDescendants, request.EffectiveFromUtc, request.EffectiveToUtc), cancellationToken, actorId);
                return Results.Created($"/api/users/{id}/role-scopes/{created.Id}", created);
            }
            catch (InvalidOperationException ex) when (IsConflict(ex)) { return Results.Conflict(new { error = ex.Message }); }
        });
        users.MapPut("/{id:guid}/role-scopes/{assignmentId:guid}", async (Guid id, Guid assignmentId, UpdateRoleScopeCommand command, ICurrentActor actor, AuthorizationService authorization, AuthorizationAdminService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            var assignments = await service.ListRoleScopesAsync(id, cancellationToken);
            var existing = assignments?.SingleOrDefault(x => x.Id == assignmentId);
            if (assignments is null || existing is null) return Results.NotFound();
            if (existing.UserId == actorId) return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (!await authorization.CanUserAdministerScopeAsync(actorId, PermissionCodes.AuthorizationRoleScopesManage, existing.OrgUnitId, existing.IncludeDescendants, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (!await authorization.CanUserAdministerScopeAsync(actorId, PermissionCodes.AuthorizationRoleScopesManage, command.OrgUnitId, command.IncludeDescendants, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            try
            {
                return await service.UpdateRoleScopeAsync(assignmentId, command, cancellationToken, actorId) is { } updated ? Results.Ok(updated) : Results.NotFound();
            }
            catch (InvalidOperationException ex) when (IsConflict(ex)) { return Results.Conflict(new { error = ex.Message }); }
        });
        users.MapPost("/{id:guid}/role-scopes/{assignmentId:guid}/revoke", async (Guid id, Guid assignmentId, RevokeRoleScopeCommand command, ICurrentActor actor, AuthorizationService authorization, AuthorizationAdminService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            var assignments = await service.ListRoleScopesAsync(id, cancellationToken);
            var existing = assignments?.SingleOrDefault(x => x.Id == assignmentId);
            if (assignments is null || existing is null) return Results.NotFound();
            if (existing.UserId == actorId) return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (!await authorization.CanUserAdministerScopeAsync(actorId, PermissionCodes.AuthorizationRoleScopesManage, existing.OrgUnitId, existing.IncludeDescendants, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return await service.RevokeRoleScopeAsync(assignmentId, command, cancellationToken, actorId) is { } revoked ? Results.Ok(revoked) : Results.NotFound();
        });

        var roles = app.MapGroup("/api/roles").WithTags("Authorization");
        roles.MapGet("/", async (ICurrentActor actor, AuthorizationService authorization, AuthorizationAdminService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.AuthorizationRoleScopesManage, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return Results.Ok(await service.ListRolesAsync(cancellationToken));
        });

        return app;
    }

    private static bool IsConflict(InvalidOperationException exception) =>
        exception.Message.Contains("unique", StringComparison.OrdinalIgnoreCase)
        || exception.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase)
        || exception.Message.Contains("overlap", StringComparison.OrdinalIgnoreCase)
        || exception.Message.Contains("at most one", StringComparison.OrdinalIgnoreCase);
}

public sealed record AdminUserWriteRequest(string DisplayName, string Email);
public sealed record AssignRoleScopeRequest(Guid RoleId, Guid OrgUnitId, bool IncludeDescendants, DateTime EffectiveFromUtc, DateTime? EffectiveToUtc);
