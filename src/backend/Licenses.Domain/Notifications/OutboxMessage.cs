namespace Licenses.Domain.Notifications;

public sealed class OutboxMessage
{
    public const int EventTypeMaxLength = 200;
    public const int LastErrorMaxLength = 4000;

    private OutboxMessage() { }

    private OutboxMessage(Guid id, string eventType, string payload, Guid correlationId, DateTime occurredAtUtc, DateTime createdAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(eventType)) throw new ArgumentException("EventType is required.", nameof(eventType));
        if (eventType.Length > EventTypeMaxLength) throw new ArgumentException($"EventType cannot exceed {EventTypeMaxLength} characters.", nameof(eventType));
        if (string.IsNullOrWhiteSpace(payload)) throw new ArgumentException("Payload is required.", nameof(payload));
        if (correlationId == Guid.Empty) throw new ArgumentException("CorrelationId is required.", nameof(correlationId));

        Id = id;
        EventType = eventType;
        Payload = payload;
        CorrelationId = correlationId;
        OccurredAtUtc = EnsureUtc(occurredAtUtc, nameof(occurredAtUtc));
        CreatedAtUtc = EnsureUtc(createdAtUtc, nameof(createdAtUtc));
    }

    public Guid Id { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public Guid CorrelationId { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }
    public int Attempts { get; private set; }
    public string? LastError { get; private set; }
    public DateTime? NextAttemptAtUtc { get; private set; }
    public DateTime? ProcessingLeaseExpiresAtUtc { get; private set; }
    public DateTime? DeadLetteredAtUtc { get; private set; }

    public static OutboxMessage Create(string eventType, string payload, Guid correlationId, DateTime occurredAtUtc, DateTime createdAtUtc) =>
        new(Guid.NewGuid(), eventType, payload, correlationId, occurredAtUtc, createdAtUtc);

    public void MarkProcessed(DateTime processedAtUtc)
    {
        ProcessedAtUtc = EnsureUtc(processedAtUtc, nameof(processedAtUtc));
        LastError = null;
        NextAttemptAtUtc = null;
        ProcessingLeaseExpiresAtUtc = null;
    }

    public void RecordAttemptFailure(string error, DateTime nowUtc, DateTime? nextAttemptAtUtc, int maxAttempts)
    {
        Attempts++;
        LastError = string.IsNullOrWhiteSpace(error) ? null : error.Trim()[..Math.Min(error.Trim().Length, LastErrorMaxLength)];
        ProcessingLeaseExpiresAtUtc = null;
        if (Attempts >= maxAttempts)
        {
            DeadLetteredAtUtc = EnsureUtc(nowUtc, nameof(nowUtc));
            NextAttemptAtUtc = null;
            return;
        }

        NextAttemptAtUtc = nextAttemptAtUtc is null ? null : EnsureUtc(nextAttemptAtUtc.Value, nameof(nextAttemptAtUtc));
    }

    private static DateTime EnsureUtc(DateTime value, string name)
    {
        if (value.Kind != DateTimeKind.Utc) throw new ArgumentException("Timestamp must be UTC.", name);
        return value;
    }
}
