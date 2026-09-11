using Licenses.Application.Notifications;
using Licenses.Domain.Notifications;
using Licenses.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Licenses.Infrastructure.Notifications;

public sealed class OutboxMessageProcessor(
    ApplicationDbContext dbContext,
    INotificationDeliveryPipeline deliveryPipeline,
    TimeProvider timeProvider,
    IOptions<OutboxProcessingOptions> options,
    ILogger<OutboxMessageProcessor> logger)
{
    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        var now = UtcNow();
        var claimed = await ClaimAsync(now, cancellationToken);
        dbContext.ChangeTracker.Clear();

        foreach (var message in claimed)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ProcessOneAsync(message.Id, cancellationToken);
        }

        return claimed.Count;
    }

    private async Task<List<OutboxMessageClaim>> ClaimAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var batchSize = Math.Max(1, options.Value.BatchSize);
        var leaseUntil = nowUtc.Add(options.Value.ProcessingLeaseDuration);

        return await dbContext.Database
            .SqlQuery<OutboxMessageClaim>($"""
                UPDATE licenses.outbox_messages
                SET processing_lease_expires_at_utc = {leaseUntil}
                WHERE id IN (
                    SELECT id
                    FROM licenses.outbox_messages
                    WHERE processed_at_utc IS NULL
                      AND dead_lettered_at_utc IS NULL
                      AND (next_attempt_at_utc IS NULL OR next_attempt_at_utc <= {nowUtc})
                      AND (processing_lease_expires_at_utc IS NULL OR processing_lease_expires_at_utc <= {nowUtc})
                    ORDER BY created_at_utc
                    FOR UPDATE SKIP LOCKED
                    LIMIT {batchSize}
                )
                RETURNING id AS "Id"
                """)
            .ToListAsync(cancellationToken);
    }

    private async Task ProcessOneAsync(Guid id, CancellationToken cancellationToken)
    {
        var message = await dbContext.OutboxMessages.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (message is null || message.ProcessedAtUtc is not null || message.DeadLetteredAtUtc is not null) return;

        try
        {
            await deliveryPipeline.DeliverAsync(new OutboxMessageEnvelope(
                message.Id,
                message.EventType,
                message.Payload,
                message.CorrelationId,
                message.OccurredAtUtc), cancellationToken);

            message.MarkProcessed(UtcNow());
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var now = UtcNow();
            message.RecordAttemptFailure(ex.Message, now, CalculateNextAttempt(now, message.Attempts), options.Value.MaxAttempts);
            await dbContext.SaveChangesAsync(cancellationToken);

            if (message.DeadLetteredAtUtc is null)
            {
                logger.LogWarning(ex, "Outbox message {OutboxMessageId} failed and will be retried.", message.Id);
            }
            else
            {
                logger.LogError(ex, "Outbox message {OutboxMessageId} reached maximum attempts and was dead-lettered.", message.Id);
            }
        }
        finally
        {
            dbContext.ChangeTracker.Clear();
        }
    }

    private DateTime CalculateNextAttempt(DateTime nowUtc, int nextAttemptNumber)
    {
        var exponent = Math.Max(0, nextAttemptNumber - 1);
        var delayTicks = options.Value.InitialRetryDelay.Ticks * Math.Pow(2, exponent);
        var cappedTicks = Math.Min(delayTicks, options.Value.MaxRetryDelay.Ticks);
        return nowUtc.Add(TimeSpan.FromTicks((long)cappedTicks));
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private sealed record OutboxMessageClaim(Guid Id);
}
