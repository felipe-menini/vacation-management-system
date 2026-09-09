namespace Licenses.Domain.Authorization;

public sealed class Permission
{
    private Permission() { }

    private Permission(Guid id, string code, string description)
    {
        Id = id;
        Code = NormalizeCode(code);
        Description = RequireText(description, nameof(description));
    }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;

    public static Permission Create(string code, string description) => new(Guid.NewGuid(), code, description);

    private static string NormalizeCode(string value) => RequireText(value, nameof(value)).ToLowerInvariant();

    private static string RequireText(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Value is required.", name);
        return value.Trim();
    }
}
