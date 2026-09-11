namespace Licenses.Application.Notifications;

public sealed record NotificationMessage(
    string RecipientEmail,
    string RecipientDisplayName,
    string Subject,
    string BodyText,
    string IdempotencyKey);

public interface INotificationSender
{
    Task SendAsync(NotificationMessage message, CancellationToken cancellationToken);
}

public interface INotificationDeliveryPipeline
{
    Task DeliverAsync(OutboxMessageEnvelope envelope, CancellationToken cancellationToken);
}

public sealed record OutboxMessageEnvelope(
    Guid Id,
    string EventType,
    string Payload,
    Guid CorrelationId,
    DateTime OccurredAtUtc);
