namespace Licenses.Domain.Organization;

public sealed class OrgUnit
{
    private OrgUnit() { }

    private OrgUnit(Guid id, string name, string code, Guid? parentId, DateTime createdAtUtc)
    {
        Id = id;
        Name = RequireText(name, nameof(name));
        Code = NormalizeCode(code);
        ParentId = parentId;
        IsActive = true;
        CreatedAtUtc = EnsureUtc(createdAtUtc, nameof(createdAtUtc));
        UpdatedAtUtc = CreatedAtUtc;
        EnsureNotOwnParent(parentId);
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public Guid? ParentId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public static OrgUnit Create(string name, string code, Guid? parentId, DateTime createdAtUtc) =>
        new(Guid.NewGuid(), name, code, parentId, createdAtUtc);

    public void Update(string name, string code, Guid? parentId, bool isActive, DateTime updatedAtUtc)
    {
        Name = RequireText(name, nameof(name));
        Code = NormalizeCode(code);
        ParentId = parentId;
        IsActive = isActive;
        UpdatedAtUtc = EnsureUtc(updatedAtUtc, nameof(updatedAtUtc));
        EnsureNotOwnParent(parentId);
    }

    private void EnsureNotOwnParent(Guid? parentId)
    {
        if (parentId == Id) throw new InvalidOperationException("An organizational unit cannot be its own parent.");
    }

    private static string RequireText(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Value is required.", name);
        return value.Trim();
    }

    private static string NormalizeCode(string value) => RequireText(value, nameof(value)).ToUpperInvariant();

    private static DateTime EnsureUtc(DateTime value, string name)
    {
        if (value.Kind != DateTimeKind.Utc) throw new ArgumentException("Timestamp must be UTC.", name);
        return value;
    }
}
