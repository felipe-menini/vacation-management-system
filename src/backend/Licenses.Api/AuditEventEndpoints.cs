using Licenses.Application.Audit;
using Licenses.Application.Authorization;

namespace Licenses.Api.Audit;

public static class AuditEventEndpoints
{
    public static IEndpointRouteBuilder MapAuditEventEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/audit-events").WithTags("Audit Events");

        group.MapGet("/", async (DateTime? fromUtc, DateTime? toUtc, string? action, string? resourceType, Guid? resourceId, Guid? actorUserId, Guid? subjectUserId, Guid? orgUnitId, int? page, int? pageSize, ICurrentActor actor, AuthorizationService authorization, IAuditEventReader reader, CancellationToken cancellationToken) =>
        {
            if (actor.UserId is not { } actorId) return Results.Unauthorized();

            var allowedOrgUnits = await authorization.GetAuthorizedOrgUnitIdsAsync(actorId, PermissionCodes.AuditEventsRead, cancellationToken);
            if (allowedOrgUnits.Count == 0) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var canReadGlobalEvents = await authorization.CanUserReadGlobalScopedResourceAsync(actorId, PermissionCodes.AuditEventsRead, cancellationToken);
            var query = new AuditEventSearchQuery(fromUtc, toUtc, action, resourceType, resourceId, actorUserId, subjectUserId, orgUnitId, page ?? 1, pageSize ?? 50);
            return Results.Ok(await reader.SearchAsync(query, allowedOrgUnits, canReadGlobalEvents, cancellationToken));
        });

        return app;
    }
}
