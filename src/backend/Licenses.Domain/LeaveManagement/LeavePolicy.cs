namespace Licenses.Domain.LeaveManagement;

public sealed class LeavePolicy
{
    private LeavePolicy() { }

    private LeavePolicy(Guid id, Guid leaveTypeId, Guid? orgUnitId, bool appliesToDescendants, bool isActive, DateTime createdAtUtc)
    {
        Id = id == Guid.Empty ? throw new ArgumentException("Id is required.", nameof(id)) : id;
        LeaveTypeId = leaveTypeId == Guid.Empty ? throw new ArgumentException("Leave type is required.", nameof(leaveTypeId)) : leaveTypeId;
        OrgUnitId = orgUnitId;
        AppliesToDescendants = orgUnitId is null ? false : appliesToDescendants;
        IsActive = isActive;
        CreatedAtUtc = EnsureUtc(createdAtUtc, nameof(createdAtUtc));
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid LeaveTypeId { get; private set; }
    public Guid? OrgUnitId { get; private set; }
    public bool AppliesToDescendants { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public List<LeavePolicyVersion> Versions { get; private set; } = [];

    public static LeavePolicy Create(Guid leaveTypeId, Guid? orgUnitId, bool appliesToDescendants, bool isActive, DateTime createdAtUtc) =>
        new(Guid.NewGuid(), leaveTypeId, orgUnitId, appliesToDescendants, isActive, createdAtUtc);

    public void UpdateScope(Guid? orgUnitId, bool appliesToDescendants, bool isActive, DateTime updatedAtUtc)
    {
        OrgUnitId = orgUnitId;
        AppliesToDescendants = orgUnitId is null ? false : appliesToDescendants;
        IsActive = isActive;
        UpdatedAtUtc = EnsureUtc(updatedAtUtc, nameof(updatedAtUtc));
    }

    private static DateTime EnsureUtc(DateTime value, string name)
    {
        if (value.Kind != DateTimeKind.Utc) throw new ArgumentException("Timestamp must be UTC.", name);
        return value;
    }
}
