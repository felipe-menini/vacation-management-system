using Licenses.Application.Audit;
using Licenses.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Licenses.Infrastructure.Audit;

public sealed class EfAuditEventReader(ApplicationDbContext dbContext) : IAuditEventReader
{
    public async Task<AuditEventListResult> SearchAsync(AuditEventSearchQuery query, IReadOnlySet<Guid> authorizedOrgUnitIds, bool canReadGlobalEvents, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var allowedOrgUnits = authorizedOrgUnitIds.ToArray();

        var events = dbContext.AuditEvents.AsNoTracking()
            .Where(x => (x.OrgUnitId != null && allowedOrgUnits.Contains(x.OrgUnitId.Value)) || (x.OrgUnitId == null && canReadGlobalEvents));

        if (query.FromUtc is { } fromUtc) events = events.Where(x => x.OccurredAtUtc >= EnsureUtc(fromUtc, nameof(query.FromUtc)));
        if (query.ToUtc is { } toUtc) events = events.Where(x => x.OccurredAtUtc <= EnsureUtc(toUtc, nameof(query.ToUtc)));
        if (!string.IsNullOrWhiteSpace(query.Action)) events = events.Where(x => x.Action == query.Action.Trim());
        if (!string.IsNullOrWhiteSpace(query.ResourceType)) events = events.Where(x => x.ResourceType == query.ResourceType.Trim());
        if (query.ResourceId is { } resourceId) events = events.Where(x => x.ResourceId == resourceId);
        if (query.ActorUserId is { } actorUserId) events = events.Where(x => x.ActorUserId == actorUserId);
        if (query.SubjectUserId is { } subjectUserId) events = events.Where(x => x.SubjectUserId == subjectUserId);
        if (query.OrgUnitId is { } orgUnitId) events = events.Where(x => x.OrgUnitId == orgUnitId);

        var total = await events.CountAsync(cancellationToken);
        var items = await events
            .OrderByDescending(x => x.OccurredAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AuditEventReadModel(
                x.Id,
                x.OccurredAtUtc,
                x.Action,
                x.ResourceType,
                x.ResourceId,
                x.ActorUserId,
                x.ActorUserId == null ? null : dbContext.Users.Where(user => user.Id == x.ActorUserId).Select(user => user.DisplayName).FirstOrDefault(),
                x.SubjectUserId,
                x.SubjectUserId == null ? null : dbContext.Users.Where(user => user.Id == x.SubjectUserId).Select(user => user.DisplayName).FirstOrDefault(),
                x.OrgUnitId,
                x.OrgUnitId == null ? null : dbContext.OrgUnits.Where(unit => unit.Id == x.OrgUnitId).Select(unit => unit.Name).FirstOrDefault(),
                x.CorrelationId,
                x.MetadataJson))
            .ToListAsync(cancellationToken);

        return new AuditEventListResult(items, page, pageSize, total, page * pageSize < total);
    }

    private static DateTime EnsureUtc(DateTime value, string name) => value.Kind == DateTimeKind.Utc ? value : throw new ArgumentException("Timestamp must be UTC.", name);
}
