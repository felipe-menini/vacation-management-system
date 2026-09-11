using Licenses.Application.Audit;
using Licenses.Domain.Audit;
using Licenses.Infrastructure.Persistence;

namespace Licenses.Infrastructure.Audit;

public sealed class EfAuditWriter(ApplicationDbContext dbContext) : IAuditWriter
{
    public async Task WriteAsync(AuditEventData auditEvent, CancellationToken cancellationToken)
    {
        var entity = AuditEvent.Create(
            auditEvent.ActorUserId,
            auditEvent.Action,
            auditEvent.ResourceType,
            auditEvent.ResourceId,
            auditEvent.SubjectUserId,
            auditEvent.OrgUnitId,
            auditEvent.CorrelationId,
            auditEvent.OccurredAtUtc,
            auditEvent.MetadataJson);

        await dbContext.AuditEvents.AddAsync(entity, cancellationToken);
    }
}
