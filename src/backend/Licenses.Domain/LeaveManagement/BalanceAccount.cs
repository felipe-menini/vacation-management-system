namespace Licenses.Domain.LeaveManagement;

public sealed class BalanceAccount
{
    private BalanceAccount() { }

    private BalanceAccount(Guid id, Guid userId, Guid balanceBucketId, DateTime createdAtUtc)
    {
        Id = id == Guid.Empty ? throw new ArgumentException("Id is required.", nameof(id)) : id;
        UserId = userId == Guid.Empty ? throw new ArgumentException("UserId is required.", nameof(userId)) : userId;
        BalanceBucketId = balanceBucketId == Guid.Empty ? throw new ArgumentException("BalanceBucketId is required.", nameof(balanceBucketId)) : balanceBucketId;
        CreatedAtUtc = EnsureUtc(createdAtUtc, nameof(createdAtUtc));
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid BalanceBucketId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public static BalanceAccount Create(Guid userId, Guid balanceBucketId, DateTime createdAtUtc) => new(Guid.NewGuid(), userId, balanceBucketId, createdAtUtc);

    private static DateTime EnsureUtc(DateTime value, string name)
    {
        if (value.Kind != DateTimeKind.Utc) throw new ArgumentException("Timestamp must be UTC.", name);
        return value;
    }
}
