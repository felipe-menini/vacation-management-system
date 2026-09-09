namespace Licenses.Domain.Authorization;

public sealed class Role
{
    private Role() { }

    private Role(Guid id, string code, string name, string description, bool isSystem, DateTime createdAtUtc)
    {
        Id = id;
        Code = NormalizeCode(code);
        Name = RequireText(name, nameof(name));
        Description = RequireText(description, nameof(description));
        IsSystem = isSystem;
        IsActive = true;
        CreatedAtUtc = EnsureUtc(createdAtUtc, nameof(createdAtUtc));
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public bool IsSystem { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public static Role Create(string code, string name, string description, bool isSystem, DateTime createdAtUtc) =>
        new(Guid.NewGuid(), code, name, description, isSystem, createdAtUtc);

    public void Update(string name, string description, bool isActive, DateTime updatedAtUtc)
    {
        Name = RequireText(name, nameof(name));
        Description = RequireText(description, nameof(description));
        IsActive = isActive;
        UpdatedAtUtc = EnsureUtc(updatedAtUtc, nameof(updatedAtUtc));
    }

    private static string NormalizeCode(string value) => RequireText(value, nameof(value)).ToUpperInvariant();

    private static string RequireText(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Value is required.", name);
        return value.Trim();
    }

    private static DateTime EnsureUtc(DateTime value, string name)
    {
        if (value.Kind != DateTimeKind.Utc) throw new ArgumentException("Timestamp must be UTC.", name);
        return value;
    }
}
