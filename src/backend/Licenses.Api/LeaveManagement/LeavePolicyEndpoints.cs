using Licenses.Application.Authorization;
using Licenses.Application.LeaveManagement;

namespace Licenses.Api.LeaveManagement;

public static class LeavePolicyEndpoints
{
    public static IEndpointRouteBuilder MapLeavePolicyEndpoints(this IEndpointRouteBuilder app)
    {
        var policies = app.MapGroup("/api/leave-policies").WithTags("Leave Policies");
        policies.MapGet("/", async (ICurrentActor actor, AuthorizationService authorization, LeavePolicyService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeavePoliciesRead, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return Results.Ok(await service.ListPoliciesAsync(cancellationToken));
        });
        policies.MapGet("/resolve", async (Guid leaveTypeId, Guid? orgUnitId, DateOnly date, ICurrentActor actor, AuthorizationService authorization, LeavePolicyService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeavePoliciesRead, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return Results.Ok(await service.ResolvePolicyAsync(leaveTypeId, orgUnitId, date, cancellationToken));
        });
        policies.MapGet("/{id:guid}", async (Guid id, ICurrentActor actor, AuthorizationService authorization, LeavePolicyService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeavePoliciesRead, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return await service.GetPolicyAsync(id, cancellationToken) is { } policy ? Results.Ok(policy) : Results.NotFound();
        });
        policies.MapPost("/", async (CreateLeavePolicyCommand command, ICurrentActor actor, AuthorizationService authorization, LeavePolicyService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeavePoliciesManage, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var created = await service.CreatePolicyAsync(command, cancellationToken);
            return Results.Created($"/api/leave-policies/{created.Id}", created);
        });
        policies.MapPut("/{id:guid}", async (Guid id, UpdateLeavePolicyCommand command, ICurrentActor actor, AuthorizationService authorization, LeavePolicyService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeavePoliciesManage, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return await service.UpdatePolicyAsync(id, command, cancellationToken) is { } updated ? Results.Ok(updated) : Results.NotFound();
        });
        policies.MapGet("/{id:guid}/versions", async (Guid id, ICurrentActor actor, AuthorizationService authorization, LeavePolicyService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeavePoliciesRead, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return Results.Ok(await service.ListVersionsAsync(id, cancellationToken));
        });
        policies.MapPost("/{id:guid}/versions", async (Guid id, CreateLeavePolicyVersionCommand command, ICurrentActor actor, AuthorizationService authorization, LeavePolicyService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeavePoliciesManage, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var created = await service.CreateVersionAsync(id, command, cancellationToken);
            return Results.Created($"/api/leave-policy-versions/{created.Id}", created);
        });

        var versions = app.MapGroup("/api/leave-policy-versions").WithTags("Leave Policies");
        versions.MapGet("/{id:guid}", async (Guid id, ICurrentActor actor, AuthorizationService authorization, LeavePolicyService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeavePoliciesRead, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return await service.GetVersionAsync(id, cancellationToken) is { } version ? Results.Ok(version) : Results.NotFound();
        });
        versions.MapPut("/{id:guid}", async (Guid id, UpdateLeavePolicyVersionCommand command, ICurrentActor actor, AuthorizationService authorization, LeavePolicyService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeavePoliciesManage, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return await service.UpdateVersionAsync(id, command, cancellationToken) is { } updated ? Results.Ok(updated) : Results.NotFound();
        });
        versions.MapPost("/{id:guid}/publish", async (Guid id, ICurrentActor actor, AuthorizationService authorization, LeavePolicyService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeavePoliciesManage, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return await service.PublishVersionAsync(id, cancellationToken) is { } published ? Results.Ok(published) : Results.NotFound();
        });

        return app;
    }
}
