using Licenses.Application.Authorization;
using Licenses.Application.LeaveManagement;

namespace Licenses.Api.LeaveManagement;

public static class LeaveCalendarEndpoints
{
    public static IEndpointRouteBuilder MapLeaveCalendarEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/leave-calendar", async (
            DateOnly? from,
            DateOnly? to,
            Guid? orgUnitId,
            int? page,
            int? pageSize,
            ICurrentActor actor,
            AuthorizationService authorization,
            LeaveCalendarService service,
            CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeaveCalendarRead, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (from is null || to is null) return Results.BadRequest(new { error = "from and to are required." });

            try
            {
                var result = await service.QueryAsync(new LeaveCalendarQuery(from.Value, to.Value, orgUnitId, page ?? 1, pageSize ?? 50), cancellationToken);
                return Results.Ok(result);
            }
            catch (UnauthorizedAccessException ex) { return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden); }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
        }).WithTags("Leave Calendar");

        return app;
    }
}
