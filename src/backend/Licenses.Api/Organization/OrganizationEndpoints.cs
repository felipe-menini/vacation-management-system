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
            var created = await service.CreateOrgUnitAsync(command, cancellationToken);
            return Results.Created($"/api/org-units/{created.Id}", created);
        });
        orgUnits.MapPut("/{id:guid}", async (Guid id, UpdateOrgUnitCommand command, ICurrentActor actor, AuthorizationService authorization, OrganizationService service, CancellationToken cancellationToken) =>
        {
            var actorId = actor.UserId;
            if (actorId is null) return Results.Unauthorized();
            if (!await authorization.CanUserPerformAsync(actorId.Value, PermissionCodes.OrgUnitsManage, id, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (command.ParentId is not null && !await authorization.CanUserPerformAsync(actorId.Value, PermissionCodes.OrgUnitsManage, command.ParentId.Value, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return await service.UpdateOrgUnitAsync(id, command, cancellationToken) is { } updated ? Results.Ok(updated) : Results.NotFound();
        });

        var users = app.MapGroup("/api/users").WithTags("Users");
        users.MapGet("/", async (ICurrentActor actor, AuthorizationService authorization, OrganizationService service, CancellationToken cancellationToken) =>
        {
            var actorId = actor.UserId;
            if (actorId is null) return Results.Unauthorized();
            var allowed = await authorization.GetAuthorizedOrgUnitIdsAsync(actorId.Value, PermissionCodes.OrgUsersRead, cancellationToken);
            if (allowed.Count == 0) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return Results.Ok(await service.ListUsersAsync(allowed, cancellationToken));
        });
        users.MapGet("/{id:guid}", async (Guid id, ICurrentActor actor, AuthorizationService authorization, OrganizationService service, CancellationToken cancellationToken) =>
        {
            var actorId = actor.UserId;
            if (actorId is null) return Results.Unauthorized();
            if (!await authorization.CanAccessUserAsync(actorId.Value, PermissionCodes.OrgUsersRead, id, cancellationToken)) return Results.NotFound();
            return await service.GetUserAsync(id, cancellationToken) is { } user ? Results.Ok(user) : Results.NotFound();
        });
        users.MapPost("/", async (CreateUserCommand command, ICurrentActor actor, AuthorizationService authorization, OrganizationService service, CancellationToken cancellationToken) =>
        {
            var actorId = actor.UserId;
            if (actorId is null) return Results.Unauthorized();
            var allowed = await authorization.GetAuthorizedOrgUnitIdsAsync(actorId.Value, PermissionCodes.OrgUsersManage, cancellationToken);
            if (allowed.Count == 0) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var created = await service.CreateUserAsync(command, cancellationToken);
            return Results.Created($"/api/users/{created.Id}", created);
        });
        users.MapPut("/{id:guid}", async (Guid id, UpdateUserCommand command, ICurrentActor actor, AuthorizationService authorization, OrganizationService service, CancellationToken cancellationToken) =>
        {
            var actorId = actor.UserId;
            if (actorId is null) return Results.Unauthorized();
            if (!await authorization.CanAccessUserAsync(actorId.Value, PermissionCodes.OrgUsersManage, id, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return await service.UpdateUserAsync(id, command, cancellationToken) is { } updated ? Results.Ok(updated) : Results.NotFound();
        });
        users.MapGet("/{id:guid}/org-assignments", async (Guid id, ICurrentActor actor, AuthorizationService authorization, OrganizationService service, CancellationToken cancellationToken) =>
        {
            var actorId = actor.UserId;
            if (actorId is null) return Results.Unauthorized();
            if (!await authorization.CanAccessUserAsync(actorId.Value, PermissionCodes.OrgAssignmentsRead, id, cancellationToken)) return Results.NotFound();
            return await service.ListAssignmentsAsync(id, cancellationToken) is { } assignments ? Results.Ok(assignments) : Results.NotFound();
        });
        users.MapPost("/{id:guid}/org-assignments", async (Guid id, CreateUserOrgAssignmentCommand command, ICurrentActor actor, AuthorizationService authorization, OrganizationService service, CancellationToken cancellationToken) =>
        {
            var actorId = actor.UserId;
            if (actorId is null) return Results.Unauthorized();
            if (!await authorization.CanAccessUserAsync(actorId.Value, PermissionCodes.OrgAssignmentsManage, id, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (!await authorization.CanUserPerformAsync(actorId.Value, PermissionCodes.OrgAssignmentsManage, command.OrgUnitId, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return await service.CreateAssignmentAsync(id, command, cancellationToken) is { } created ? Results.Created($"/api/users/{id}/org-assignments/{created.Id}", created) : Results.NotFound();
        });

        return app;
    }
}
