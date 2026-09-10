namespace Licenses.Domain.LeaveManagement;

public enum BalanceLedgerEntryType
{
    Grant = 1,
    Reserve = 2,
    Release = 3,
    Consume = 4,
    Refund = 5,
    Adjustment = 6,
    Expire = 7
}

public sealed class BalanceLedgerEntry
{
    public const int ReasonMaxLength = 1000;

    private BalanceLedgerEntry() { }

    private BalanceLedgerEntry(Guid id, Guid balanceAccountId, Guid operationId, BalanceLedgerEntryType type, decimal availableDelta, decimal reservedDelta, string reason, Guid? createdByUserId, DateTime createdAtUtc)
    {
        Id = id == Guid.Empty ? throw new ArgumentException("Id is required.", nameof(id)) : id;
        BalanceAccountId = balanceAccountId == Guid.Empty ? throw new ArgumentException("BalanceAccountId is required.", nameof(balanceAccountId)) : balanceAccountId;
        OperationId = operationId == Guid.Empty ? throw new ArgumentException("OperationId is required.", nameof(operationId)) : operationId;
        Type = Enum.IsDefined(type) ? type : throw new ArgumentOutOfRangeException(nameof(type), "Ledger entry type is invalid.");
        if (availableDelta == 0m && reservedDelta == 0m) throw new ArgumentException("Ledger entry cannot have zero effect.");
        AvailableDelta = availableDelta;
        ReservedDelta = reservedDelta;
        Reason = RequireText(reason, nameof(reason), ReasonMaxLength);
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = EnsureUtc(createdAtUtc, nameof(createdAtUtc));
    }

    public Guid Id { get; private set; }
    public Guid BalanceAccountId { get; private set; }
    public Guid OperationId { get; private set; }
    public BalanceLedgerEntryType Type { get; private set; }
    public decimal AvailableDelta { get; private set; }
    public decimal ReservedDelta { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public Guid? CreatedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public static BalanceLedgerEntry Create(Guid balanceAccountId, Guid operationId, BalanceLedgerEntryType type, decimal availableDelta, decimal reservedDelta, string reason, Guid? createdByUserId, DateTime createdAtUtc) =>
        new(Guid.NewGuid(), balanceAccountId, operationId, type, availableDelta, reservedDelta, reason, createdByUserId, createdAtUtc);

    public static (decimal AvailableDelta, decimal ReservedDelta) GetDeltas(BalanceLedgerEntryType type, decimal amount)
    {
        if (type == BalanceLedgerEntryType.Adjustment)
        {
            if (amount == 0m) throw new ArgumentOutOfRangeException(nameof(amount), "Adjustment amount cannot be zero.");
            return (amount, 0m);
        }

        if (amount <= 0m) throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be strictly positive.");
        return type switch
        {
            BalanceLedgerEntryType.Grant => (amount, 0m),
            BalanceLedgerEntryType.Reserve => (-amount, amount),
            BalanceLedgerEntryType.Release => (amount, -amount),
            BalanceLedgerEntryType.Consume => (0m, -amount),
            BalanceLedgerEntryType.Refund => (amount, 0m),
            BalanceLedgerEntryType.Expire => (-amount, 0m),
            _ => throw new ArgumentOutOfRangeException(nameof(type), "Ledger entry type is invalid.")
        };
    }

    private static string RequireText(string value, string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Value is required.", name);
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
