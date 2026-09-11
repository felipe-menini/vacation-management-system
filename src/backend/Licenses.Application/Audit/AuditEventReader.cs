namespace Licenses.Application.Audit;

public sealed record AuditEventSearchQuery(
    DateTime? FromUtc,
    DateTime? ToUtc,
    string? Action,
    string? ResourceType,
    Guid? ResourceId,
    Guid? ActorUserId,
    Guid? SubjectUserId,
    Guid? OrgUnitId,
    int Page = 1,
    int PageSize = 50);

public sealed record AuditEventListResult(IReadOnlyList<AuditEventReadModel> Items, int Page, int PageSize, int TotalCount, bool HasNextPage);

public sealed record AuditEventReadModel(
    Guid Id,
    DateTime OccurredAtUtc,
    string Action,
    string ResourceType,
    Guid? ResourceId,
    Guid? ActorUserId,
    string? ActorDisplayName,
    Guid? SubjectUserId,
    string? SubjectDisplayName,
    Guid? OrgUnitId,
    string? OrgUnitName,
    Guid? CorrelationId,
    string? MetadataJson);

public interface IAuditEventReader
{
    Task<AuditEventListResult> SearchAsync(AuditEventSearchQuery query, IReadOnlySet<Guid> authorizedOrgUnitIds, bool canReadGlobalEvents, CancellationToken cancellationToken);
}
