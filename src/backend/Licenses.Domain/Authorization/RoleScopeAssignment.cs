namespace Licenses.Domain.Authorization;

public sealed class RoleScopeAssignment
{
    private RoleScopeAssignment() { }

    private RoleScopeAssignment(Guid id, Guid userId, Guid roleId, Guid orgUnitId, bool includeDescendants, DateTime effectiveFromUtc, DateTime? effectiveToUtc)
    {
        Id = id;
        UserId = userId == Guid.Empty ? throw new ArgumentException("User id is required.", nameof(userId)) : userId;
        RoleId = roleId == Guid.Empty ? throw new ArgumentException("Role id is required.", nameof(roleId)) : roleId;
        OrgUnitId = orgUnitId == Guid.Empty ? throw new ArgumentException("Organizational unit id is required.", nameof(orgUnitId)) : orgUnitId;
        IncludeDescendants = includeDescendants;
        EffectiveFromUtc = EnsureUtc(effectiveFromUtc, nameof(effectiveFromUtc));
        EffectiveToUtc = effectiveToUtc is null ? null : EnsureUtc(effectiveToUtc.Value, nameof(effectiveToUtc));
        if (EffectiveToUtc <= EffectiveFromUtc) throw new InvalidOperationException("Role scope assignment end must be after its start.");
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid RoleId { get; private set; }
    public Guid OrgUnitId { get; private set; }
    public bool IncludeDescendants { get; private set; }
    public DateTime EffectiveFromUtc { get; private set; }
    public DateTime? EffectiveToUtc { get; private set; }

    public bool IsActiveAt(DateTime utcNow) => EffectiveFromUtc <= utcNow && (EffectiveToUtc is null || EffectiveToUtc > utcNow);

    public static RoleScopeAssignment Create(Guid userId, Guid roleId, Guid orgUnitId, bool includeDescendants, DateTime effectiveFromUtc, DateTime? effectiveToUtc) =>
        new(Guid.NewGuid(), userId, roleId, orgUnitId, includeDescendants, effectiveFromUtc, effectiveToUtc);

    public void UpdateScope(Guid orgUnitId, bool includeDescendants, DateTime effectiveFromUtc, DateTime? effectiveToUtc)
    {
        OrgUnitId = orgUnitId == Guid.Empty ? throw new ArgumentException("Organizational unit id is required.", nameof(orgUnitId)) : orgUnitId;
        IncludeDescendants = includeDescendants;
        EffectiveFromUtc = EnsureUtc(effectiveFromUtc, nameof(effectiveFromUtc));
        EffectiveToUtc = effectiveToUtc is null ? null : EnsureUtc(effectiveToUtc.Value, nameof(effectiveToUtc));
        if (EffectiveToUtc <= EffectiveFromUtc) throw new InvalidOperationException("Role scope assignment end must be after its start.");
    }

    public void Revoke(DateTime effectiveToUtc)
    {
        var utcEnd = EnsureUtc(effectiveToUtc, nameof(effectiveToUtc));
        if (utcEnd <= EffectiveFromUtc) throw new InvalidOperationException("Role scope assignment end must be after its start.");
        EffectiveToUtc = utcEnd;
    }

    private static DateTime EnsureUtc(DateTime value, string name)
    {
        if (value.Kind != DateTimeKind.Utc) throw new ArgumentException("Timestamp must be UTC.", name);
        return value;
    }
}
