using Licenses.Application.Authorization;
using Licenses.Application.LeaveManagement;

namespace Licenses.Api.LeaveManagement;

public static class WorkingCalendarEndpoints
{
    public static IEndpointRouteBuilder MapWorkingCalendarEndpoints(this IEndpointRouteBuilder app)
    {
        var calendars = app.MapGroup("/api/working-calendars").WithTags("Working Calendars");
        calendars.MapGet("/", async (ICurrentActor actor, AuthorizationService authorization, WorkingCalendarService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeaveCalendarsRead, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return Results.Ok(await service.ListCalendarsAsync(cancellationToken));
        });
        calendars.MapGet("/{id:guid}", async (Guid id, ICurrentActor actor, AuthorizationService authorization, WorkingCalendarService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeaveCalendarsRead, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return await service.GetCalendarAsync(id, cancellationToken) is { } calendar ? Results.Ok(calendar) : Results.NotFound();
        });
        calendars.MapPost("/", async (CreateWorkingCalendarCommand command, ICurrentActor actor, AuthorizationService authorization, WorkingCalendarService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeaveCalendarsManage, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var created = await service.CreateCalendarAsync(command, cancellationToken);
            return Results.Created($"/api/working-calendars/{created.Id}", created);
        });
        calendars.MapPut("/{id:guid}", async (Guid id, UpdateWorkingCalendarCommand command, ICurrentActor actor, AuthorizationService authorization, WorkingCalendarService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeaveCalendarsManage, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return await service.UpdateCalendarAsync(id, command, cancellationToken) is { } updated ? Results.Ok(updated) : Results.NotFound();
        });
        calendars.MapGet("/{id:guid}/exceptions", async (Guid id, ICurrentActor actor, AuthorizationService authorization, WorkingCalendarService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeaveCalendarsRead, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return await service.ListExceptionsAsync(id, cancellationToken) is { } exceptions ? Results.Ok(exceptions) : Results.NotFound();
        });
        calendars.MapPost("/{id:guid}/exceptions", async (Guid id, UpsertWorkingCalendarExceptionCommand command, ICurrentActor actor, AuthorizationService authorization, WorkingCalendarService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeaveCalendarsManage, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var created = await service.CreateExceptionAsync(id, command, cancellationToken);
            return Results.Created($"/api/working-calendar-exceptions/{created.Id}", created);
        });

        var exceptions = app.MapGroup("/api/working-calendar-exceptions").WithTags("Working Calendars");
        exceptions.MapPut("/{id:guid}", async (Guid id, UpsertWorkingCalendarExceptionCommand command, ICurrentActor actor, AuthorizationService authorization, WorkingCalendarService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeaveCalendarsManage, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return await service.UpdateExceptionAsync(id, command, cancellationToken) is { } updated ? Results.Ok(updated) : Results.NotFound();
        });

        app.MapGet("/api/day-calculation", async (string mode, Guid? workingCalendarId, DateOnly startDate, DateOnly endDate, ICurrentActor actor, AuthorizationService authorization, WorkingCalendarService service, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();
            if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeaveCalendarsRead, cancellationToken)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return Results.Ok(await service.CalculateAsync(mode, workingCalendarId, startDate, endDate, cancellationToken));
        }).WithTags("Day Calculation");

        return app;
    }
}
