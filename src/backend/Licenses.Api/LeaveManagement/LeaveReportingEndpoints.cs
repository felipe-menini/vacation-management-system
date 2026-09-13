using Licenses.Application.Authorization;
using Licenses.Application.LeaveManagement;

namespace Licenses.Api.LeaveManagement;

public static class LeaveReportingEndpoints
{
    public static IEndpointRouteBuilder MapLeaveReportingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/reports/leave-summary", async (
            DateOnly? from,
            DateOnly? to,
            Guid? orgUnitId,
            ICurrentActor actor,
            AuthorizationService authorization,
            LeaveReportingService service,
            CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeaveReportsRead, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (from is null || to is null) return Results.BadRequest(new { error = "from and to are required." });

            try
            {
                var result = await service.GetLeaveSummaryAsync(new LeaveSummaryReportQuery(from.Value, to.Value, orgUnitId), cancellationToken);
                return Results.Ok(result);
            }
            catch (UnauthorizedAccessException ex) { return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden); }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
        }).WithTags("Leave Reports");

        return app;
    }
}
