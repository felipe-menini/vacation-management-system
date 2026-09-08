namespace Licenses.Domain.Identity;

public sealed class User
{
    private User() { }

    private User(Guid id, string displayName, string email, string? externalIdentityId, DateTime createdAtUtc)
    {
        Id = id;
        DisplayName = RequireText(displayName, nameof(displayName));
        Email = NormalizeEmail(email);
        ExternalIdentityId = NormalizeOptional(externalIdentityId);
        IsActive = true;
        CreatedAtUtc = EnsureUtc(createdAtUtc, nameof(createdAtUtc));
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }
    public string? ExternalIdentityId { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public static User Create(string displayName, string email, string? externalIdentityId, DateTime createdAtUtc) =>
        new(Guid.NewGuid(), displayName, email, externalIdentityId, createdAtUtc);

    public void Update(string displayName, string email, string? externalIdentityId, bool isActive, DateTime updatedAtUtc)
    {
        DisplayName = RequireText(displayName, nameof(displayName));
        Email = NormalizeEmail(email);
        ExternalIdentityId = NormalizeOptional(externalIdentityId);
        IsActive = isActive;
        UpdatedAtUtc = EnsureUtc(updatedAtUtc, nameof(updatedAtUtc));
    }

    private static string RequireText(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Value is required.", name);
        return value.Trim();
    }

    private static string NormalizeEmail(string value)
    {
        var email = RequireText(value, nameof(value)).ToUpperInvariant();
        if (!email.Contains('@', StringComparison.Ordinal)) throw new ArgumentException("Email must be valid.", nameof(value));
        return email;
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateTime EnsureUtc(DateTime value, string name)
    {
        if (value.Kind != DateTimeKind.Utc) throw new ArgumentException("Timestamp must be UTC.", name);
        return value;
    }
}
