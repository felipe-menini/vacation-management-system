using Licenses.Application.Authorization;
using Licenses.Application.LeaveManagement;

namespace Licenses.Api.LeaveManagement;

public static class LeaveCatalogEndpoints
{
    public static IEndpointRouteBuilder MapLeaveCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var leaveTypes = app.MapGroup("/api/leave-types").WithTags("Leave Catalog");
        leaveTypes.MapGet("/", async (bool? isActive, ICurrentActor actor, AuthorizationService authorization, LeaveCatalogService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeaveCatalogRead, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return Results.Ok(await service.ListLeaveTypesAsync(isActive, cancellationToken));
        });
        leaveTypes.MapGet("/{id:guid}", async (Guid id, ICurrentActor actor, AuthorizationService authorization, LeaveCatalogService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeaveCatalogRead, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return await service.GetLeaveTypeAsync(id, cancellationToken) is { } leaveType ? Results.Ok(leaveType) : Results.NotFound();
        });
        leaveTypes.MapPost("/", async (CreateLeaveTypeCommand command, ICurrentActor actor, AuthorizationService authorization, LeaveCatalogService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeaveCatalogManage, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var created = await service.CreateLeaveTypeAsync(command, cancellationToken);
            return Results.Created($"/api/leave-types/{created.Id}", created);
        });
        leaveTypes.MapPut("/{id:guid}", async (Guid id, UpdateLeaveTypeCommand command, ICurrentActor actor, AuthorizationService authorization, LeaveCatalogService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeaveCatalogManage, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return await service.UpdateLeaveTypeAsync(id, command, cancellationToken) is { } updated ? Results.Ok(updated) : Results.NotFound();
        });

        var balanceBuckets = app.MapGroup("/api/balance-buckets").WithTags("Leave Catalog");
        balanceBuckets.MapGet("/", async (bool? isActive, ICurrentActor actor, AuthorizationService authorization, LeaveCatalogService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeaveCatalogRead, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return Results.Ok(await service.ListBalanceBucketsAsync(isActive, cancellationToken));
        });
        balanceBuckets.MapGet("/{id:guid}", async (Guid id, ICurrentActor actor, AuthorizationService authorization, LeaveCatalogService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeaveCatalogRead, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return await service.GetBalanceBucketAsync(id, cancellationToken) is { } bucket ? Results.Ok(bucket) : Results.NotFound();
        });
        balanceBuckets.MapPost("/", async (CreateBalanceBucketCommand command, ICurrentActor actor, AuthorizationService authorization, LeaveCatalogService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeaveCatalogManage, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var created = await service.CreateBalanceBucketAsync(command, cancellationToken);
            return Results.Created($"/api/balance-buckets/{created.Id}", created);
        });
        balanceBuckets.MapPut("/{id:guid}", async (Guid id, UpdateBalanceBucketCommand command, ICurrentActor actor, AuthorizationService authorization, LeaveCatalogService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeaveCatalogManage, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return await service.UpdateBalanceBucketAsync(id, command, cancellationToken) is { } updated ? Results.Ok(updated) : Results.NotFound();
        });

        return app;
    }
}
