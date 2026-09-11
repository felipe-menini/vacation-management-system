namespace Licenses.Domain.Organization;

public sealed class UserOrgAssignment
{
    private UserOrgAssignment() { }

    private UserOrgAssignment(Guid id, Guid userId, Guid orgUnitId, bool isPrimary, DateTime effectiveFromUtc, DateTime? effectiveToUtc)
    {
        Id = id;
        UserId = userId;
        OrgUnitId = orgUnitId;
        IsPrimary = isPrimary;
        EffectiveFromUtc = EnsureUtc(effectiveFromUtc, nameof(effectiveFromUtc));
        EffectiveToUtc = effectiveToUtc is null ? null : EnsureUtc(effectiveToUtc.Value, nameof(effectiveToUtc));
        if (EffectiveToUtc <= EffectiveFromUtc) throw new InvalidOperationException("Assignment end must be after its start.");
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid OrgUnitId { get; private set; }
    public bool IsPrimary { get; private set; }
    public DateTime EffectiveFromUtc { get; private set; }
    public DateTime? EffectiveToUtc { get; private set; }

    public bool IsActiveAt(DateTime utcNow) => EffectiveFromUtc <= utcNow && (EffectiveToUtc is null || EffectiveToUtc > utcNow);

    public static UserOrgAssignment Create(Guid userId, Guid orgUnitId, bool isPrimary, DateTime effectiveFromUtc, DateTime? effectiveToUtc) =>
        new(Guid.NewGuid(), userId, orgUnitId, isPrimary, effectiveFromUtc, effectiveToUtc);

    public void Update(Guid orgUnitId, bool isPrimary, DateTime effectiveFromUtc, DateTime? effectiveToUtc)
    {
        OrgUnitId = orgUnitId == Guid.Empty ? throw new ArgumentException("Organizational unit id is required.", nameof(orgUnitId)) : orgUnitId;
        IsPrimary = isPrimary;
        EffectiveFromUtc = EnsureUtc(effectiveFromUtc, nameof(effectiveFromUtc));
        EffectiveToUtc = effectiveToUtc is null ? null : EnsureUtc(effectiveToUtc.Value, nameof(effectiveToUtc));
        if (EffectiveToUtc <= EffectiveFromUtc) throw new InvalidOperationException("Assignment end must be after its start.");
    }

    public void End(DateTime effectiveToUtc)
    {
        var utcEnd = EnsureUtc(effectiveToUtc, nameof(effectiveToUtc));
        if (utcEnd <= EffectiveFromUtc) throw new InvalidOperationException("Assignment end must be after its start.");
        EffectiveToUtc = utcEnd;
    }

    private static DateTime EnsureUtc(DateTime value, string name)
    {
        if (value.Kind != DateTimeKind.Utc) throw new ArgumentException("Timestamp must be UTC.", name);
        return value;
    }
}
