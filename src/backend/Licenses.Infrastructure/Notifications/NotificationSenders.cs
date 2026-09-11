using Licenses.Application.Notifications;
using Microsoft.Extensions.Logging;

namespace Licenses.Infrastructure.Notifications;

public sealed class DevelopmentLoggingNotificationSender(ILogger<DevelopmentLoggingNotificationSender> logger) : INotificationSender
{
    public Task SendAsync(NotificationMessage message, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Development notification only. To={RecipientEmail} ({RecipientDisplayName}); Subject={Subject}; IdempotencyKey={IdempotencyKey}; Body={BodyText}",
            message.RecipientEmail,
            message.RecipientDisplayName,
            message.Subject,
            message.IdempotencyKey,
            message.BodyText);

        return Task.CompletedTask;
    }
}

public sealed class UnconfiguredNotificationSender : INotificationSender
{
    public Task SendAsync(NotificationMessage message, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Notification delivery provider is not configured for this environment.");
}
