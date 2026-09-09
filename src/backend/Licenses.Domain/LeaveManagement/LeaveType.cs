namespace Licenses.Domain.LeaveManagement;

public sealed class LeaveType
{
    public const int CodeMaxLength = 64;
    public const int NameMaxLength = 200;
    public const int DescriptionMaxLength = 1000;

    private LeaveType() { }

    private LeaveType(Guid id, string code, string name, string? description, int sortOrder, bool isActive, DateTime createdAtUtc)
    {
        Id = id == Guid.Empty ? throw new ArgumentException("Id is required.", nameof(id)) : id;
        Code = NormalizeCode(code);
        Name = RequireText(name, nameof(name), NameMaxLength);
        Description = NormalizeOptional(description, DescriptionMaxLength, nameof(description));
        SortOrder = sortOrder < 0 ? throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order must be non-negative.") : sortOrder;
        IsActive = isActive;
        CreatedAtUtc = EnsureUtc(createdAtUtc, nameof(createdAtUtc));
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public int SortOrder { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public static LeaveType Create(string code, string name, string? description, int sortOrder, bool isActive, DateTime createdAtUtc) =>
        new(Guid.NewGuid(), code, name, description, sortOrder, isActive, createdAtUtc);

    public void UpdateDetails(string name, string? description, int sortOrder, bool isActive, DateTime updatedAtUtc)
    {
        Name = RequireText(name, nameof(name), NameMaxLength);
        Description = NormalizeOptional(description, DescriptionMaxLength, nameof(description));
        SortOrder = sortOrder < 0 ? throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order must be non-negative.") : sortOrder;
        IsActive = isActive;
        UpdatedAtUtc = EnsureUtc(updatedAtUtc, nameof(updatedAtUtc));
    }

    public static string NormalizeCode(string value) => RequireText(value, nameof(value), CodeMaxLength).ToUpperInvariant();

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
