using Licenses.Application.Authorization;
using Licenses.Application.LeaveManagement;

namespace Licenses.Api.LeaveManagement;

public static class LeaveRequestEndpoints
{
    public static IEndpointRouteBuilder MapLeaveRequestEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/leave-requests").WithTags("Leave Requests");

        group.MapGet("/me", async (ICurrentActor actor, AuthorizationService authorization, LeaveRequestService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeaveRequestsReadSelf, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return Results.Ok(await service.ListMyRequestsAsync(cancellationToken));
        });

        group.MapGet("/scoped", async (ICurrentActor actor, AuthorizationService authorization, LeaveRequestService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeaveRequestsRead, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return Results.Ok(await service.ListScopedAsync(cancellationToken));
        });

        group.MapGet("/pending-approval", async (ICurrentActor actor, AuthorizationService authorization, LeaveRequestService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeaveRequestsDecide, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return Results.Ok(await service.GetPendingApprovalsAsync(cancellationToken));
        });

        group.MapGet("/pending-cancellation", async (ICurrentActor actor, AuthorizationService authorization, LeaveRequestService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeaveRequestsCancelDecide, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return Results.Ok(await service.GetPendingCancellationsAsync(cancellationToken));
        });

        group.MapGet("/{id:guid}", async (Guid id, ICurrentActor actor, LeaveRequestService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is null) return Results.Unauthorized();
            return await service.GetAsync(id, cancellationToken) is { } request ? Results.Ok(request) : Results.NotFound();
        });

        group.MapPost("/", async (CreateLeaveRequestCommand command, ICurrentActor actor, AuthorizationService authorization, LeaveRequestService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeaveRequestsCreateSelf, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var created = await service.CreateDraftAsync(command, cancellationToken);
            return Results.Created($"/api/leave-requests/{created.Id}", created);
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateLeaveRequestCommand command, ICurrentActor actor, AuthorizationService authorization, LeaveRequestService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeaveRequestsCreateSelf, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return await service.UpdateDraftAsync(id, command, cancellationToken) is { } updated ? Results.Ok(updated) : Results.NotFound();
        });

        group.MapPost("/{id:guid}/submit", async (Guid id, ICurrentActor actor, AuthorizationService authorization, LeaveRequestService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeaveRequestsCreateSelf, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return await service.SubmitAsync(id, cancellationToken) is { } result ? Results.Ok(result) : Results.NotFound();
        });

        group.MapPost("/{id:guid}/approve", async (Guid id, DecideLeaveRequestCommand command, ICurrentActor actor, AuthorizationService authorization, LeaveRequestService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeaveRequestsDecide, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            try
            {
                return await service.ApproveAsync(id, command, cancellationToken) is { } result ? Results.Ok(result) : Results.NotFound();
            }
            catch (UnauthorizedAccessException ex) { return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden); }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
        });

        group.MapPost("/{id:guid}/reject", async (Guid id, DecideLeaveRequestCommand command, ICurrentActor actor, AuthorizationService authorization, LeaveRequestService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeaveRequestsDecide, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            try
            {
                return await service.RejectAsync(id, command, cancellationToken) is { } result ? Results.Ok(result) : Results.NotFound();
            }
            catch (UnauthorizedAccessException ex) { return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden); }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
        });


        group.MapPost("/{id:guid}/request-cancellation", async (Guid id, RequestCancellationCommand command, ICurrentActor actor, AuthorizationService authorization, LeaveRequestService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeaveRequestsCancelSelf, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            try { return await service.RequestCancellationAsync(id, command, cancellationToken) is { } result ? Results.Ok(result) : Results.NotFound(); }
            catch (UnauthorizedAccessException ex) { return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden); }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
        });

        group.MapPost("/{id:guid}/approve-cancellation", async (Guid id, DecideCancellationCommand command, ICurrentActor actor, AuthorizationService authorization, LeaveRequestService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeaveRequestsCancelDecide, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            try { return await service.ApproveCancellationAsync(id, command, cancellationToken) is { } result ? Results.Ok(result) : Results.NotFound(); }
            catch (UnauthorizedAccessException ex) { return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden); }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
        });

        group.MapPost("/{id:guid}/reject-cancellation", async (Guid id, DecideCancellationCommand command, ICurrentActor actor, AuthorizationService authorization, LeaveRequestService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeaveRequestsCancelDecide, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            try { return await service.RejectCancellationAsync(id, command, cancellationToken) is { } result ? Results.Ok(result) : Results.NotFound(); }
            catch (UnauthorizedAccessException ex) { return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden); }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
        });

        group.MapPost("/{id:guid}/revoke", async (Guid id, RevokeLeaveRequestCommand command, ICurrentActor actor, AuthorizationService authorization, LeaveRequestService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeaveRequestsRevoke, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            try { return await service.RevokeAsync(id, command, cancellationToken) is { } result ? Results.Ok(result) : Results.NotFound(); }
            catch (UnauthorizedAccessException ex) { return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden); }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
        });

        app.MapGet("/api/users/{userId:guid}/leave-requests", async (Guid userId, ICurrentActor actor, LeaveRequestService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is null) return Results.Unauthorized();
            return await service.ListUserRequestsAsync(userId, cancellationToken) is { } requests ? Results.Ok(requests) : Results.NotFound();
        }).WithTags("Leave Requests");

        app.MapPost("/api/users/{userId:guid}/leave-requests", async (Guid userId, CreateLeaveRequestForUserCommand command, ICurrentActor actor, AuthorizationService authorization, LeaveRequestService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeaveRequestsCreateForOthers, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            try
            {
                var result = await service.CreateForUserAsync(userId, command, cancellationToken);
                return Results.Created($"/api/leave-requests/{result.Request.Id}", result);
            }
            catch (UnauthorizedAccessException ex) { return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden); }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
        }).WithTags("Leave Requests");

        return app;
    }
}
