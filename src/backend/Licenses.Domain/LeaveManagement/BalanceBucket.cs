namespace Licenses.Domain.LeaveManagement;

public enum BalanceBucketUnit
{
    Day = 1
}

public sealed class BalanceBucket
{
    public const int CodeMaxLength = 64;
    public const int NameMaxLength = 200;
    public const int DescriptionMaxLength = 1000;

    private BalanceBucket() { }

    private BalanceBucket(Guid id, string code, string name, string? description, BalanceBucketUnit unit, bool isActive, DateTime createdAtUtc)
    {
        Id = id == Guid.Empty ? throw new ArgumentException("Id is required.", nameof(id)) : id;
        Code = NormalizeCode(code);
        Name = RequireText(name, nameof(name), NameMaxLength);
        Description = NormalizeOptional(description, DescriptionMaxLength, nameof(description));
        Unit = Enum.IsDefined(unit) ? unit : throw new ArgumentOutOfRangeException(nameof(unit), "Balance bucket unit is invalid.");
        IsActive = isActive;
        CreatedAtUtc = EnsureUtc(createdAtUtc, nameof(createdAtUtc));
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public BalanceBucketUnit Unit { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public static BalanceBucket Create(string code, string name, string? description, BalanceBucketUnit unit, bool isActive, DateTime createdAtUtc) =>
        new(Guid.NewGuid(), code, name, description, unit, isActive, createdAtUtc);

    public void UpdateDetails(string name, string? description, BalanceBucketUnit unit, bool isActive, DateTime updatedAtUtc)
    {
        Name = RequireText(name, nameof(name), NameMaxLength);
        Description = NormalizeOptional(description, DescriptionMaxLength, nameof(description));
        Unit = Enum.IsDefined(unit) ? unit : throw new ArgumentOutOfRangeException(nameof(unit), "Balance bucket unit is invalid.");
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
