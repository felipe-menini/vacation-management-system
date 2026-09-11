namespace Licenses.Domain.Audit;

public sealed class AuditEvent
{
    public const int ActionMaxLength = 200;
    public const int ResourceTypeMaxLength = 100;
    public const int MetadataJsonMaxLength = 8000;

    private AuditEvent() { }

    private AuditEvent(Guid id, Guid? actorUserId, string action, string resourceType, Guid? resourceId, Guid? subjectUserId, Guid? orgUnitId, Guid? correlationId, DateTime occurredAtUtc, string? metadataJson)
    {
        Id = id == Guid.Empty ? throw new ArgumentException("Id is required.", nameof(id)) : id;
        ActorUserId = actorUserId;
        Action = RequireText(action, nameof(action), ActionMaxLength);
        ResourceType = RequireText(resourceType, nameof(resourceType), ResourceTypeMaxLength);
        ResourceId = resourceId;
        SubjectUserId = subjectUserId;
        OrgUnitId = orgUnitId;
        CorrelationId = correlationId;
        OccurredAtUtc = EnsureUtc(occurredAtUtc, nameof(occurredAtUtc));
        MetadataJson = NormalizeOptional(metadataJson, MetadataJsonMaxLength, nameof(metadataJson));
    }

    public Guid Id { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string ResourceType { get; private set; } = string.Empty;
    public Guid? ResourceId { get; private set; }
    public Guid? SubjectUserId { get; private set; }
    public Guid? OrgUnitId { get; private set; }
    public Guid? CorrelationId { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public string? MetadataJson { get; private set; }

    public static AuditEvent Create(Guid? actorUserId, string action, string resourceType, Guid? resourceId, Guid? subjectUserId, Guid? orgUnitId, Guid? correlationId, DateTime occurredAtUtc, string? metadataJson) =>
        new(Guid.NewGuid(), actorUserId, action, resourceType, resourceId, subjectUserId, orgUnitId, correlationId, occurredAtUtc, metadataJson);

    private static string RequireText(string value, string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Value is required.", name);
        var normalized = value.Trim();
        if (normalized.Length > maxLength) throw new ArgumentException($"Value cannot exceed {maxLength} characters.", name);
        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        if (normalized.Length > maxLength) throw new ArgumentException($"Value cannot exceed {maxLength} characters.", name);
        return normalized;
    }

    private static DateTime EnsureUtc(DateTime value, string name)
    {
        if (value.Kind != DateTimeKind.Utc) throw new ArgumentException("Timestamp must be UTC.", name);
        return value;
    }
}
