namespace Licenses.Application.Audit;

public sealed record AuditEventData(
    Guid? ActorUserId,
    string Action,
    string ResourceType,
    Guid? ResourceId,
    Guid? SubjectUserId,
    Guid? OrgUnitId,
    Guid? CorrelationId,
    DateTime OccurredAtUtc,
    string? MetadataJson);

public interface IAuditWriter
{
    Task WriteAsync(AuditEventData auditEvent, CancellationToken cancellationToken);
}
