namespace Licenses.Infrastructure.Notifications;

public sealed class OutboxProcessingOptions
{
    public int BatchSize { get; set; } = 10;
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);
    public int MaxAttempts { get; set; } = 5;
    public TimeSpan InitialRetryDelay { get; set; } = TimeSpan.FromSeconds(10);
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan ProcessingLeaseDuration { get; set; } = TimeSpan.FromMinutes(2);
}
