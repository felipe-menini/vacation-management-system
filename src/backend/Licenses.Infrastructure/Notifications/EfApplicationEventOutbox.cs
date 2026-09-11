using System.Text.Json;
using System.Text.Json.Serialization;
using Licenses.Application.Notifications;
using Licenses.Domain.Notifications;
using Licenses.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Licenses.Infrastructure.Notifications;

public sealed class EfApplicationEventOutbox(ApplicationDbContext dbContext, TimeProvider timeProvider) : IApplicationEventOutbox
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task EnqueueAsync(IApplicationEvent applicationEvent, Guid correlationId, CancellationToken cancellationToken)
    {
        if (correlationId == Guid.Empty) throw new ArgumentException("CorrelationId is required.", nameof(correlationId));

        var eventType = applicationEvent.GetType().Name;
        if (await dbContext.OutboxMessages.AnyAsync(x => x.EventType == eventType && x.CorrelationId == correlationId, cancellationToken)) return;

        var payload = JsonSerializer.Serialize(applicationEvent, applicationEvent.GetType(), JsonOptions);
        var message = OutboxMessage.Create(eventType, payload, correlationId, applicationEvent.OccurredAtUtc, timeProvider.GetUtcNow().UtcDateTime);
        await dbContext.OutboxMessages.AddAsync(message, cancellationToken);
    }
}
