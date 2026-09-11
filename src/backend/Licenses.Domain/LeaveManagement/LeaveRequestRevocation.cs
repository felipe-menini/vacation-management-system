namespace Licenses.Domain.LeaveManagement;

public sealed class LeaveRequestRevocation
{
    public const int ReasonMaxLength = 1000;

    private LeaveRequestRevocation() { }

    private LeaveRequestRevocation(Guid leaveRequestId, Guid revokedByUserId, string reason, Guid operationId, Guid? balanceSettlementOperationId, DateTime createdAtUtc)
    {
        if (leaveRequestId == Guid.Empty) throw new ArgumentException("LeaveRequestId is required.", nameof(leaveRequestId));
        if (revokedByUserId == Guid.Empty) throw new ArgumentException("RevokedByUserId is required.", nameof(revokedByUserId));
        if (operationId == Guid.Empty) throw new ArgumentException("OperationId is required.", nameof(operationId));
        Id = Guid.NewGuid();
        LeaveRequestId = leaveRequestId;
        RevokedByUserId = revokedByUserId;
        Reason = NormalizeRequired(reason, ReasonMaxLength, nameof(reason));
        OperationId = operationId;
        BalanceSettlementOperationId = balanceSettlementOperationId;
        CreatedAtUtc = EnsureUtc(createdAtUtc, nameof(createdAtUtc));
    }

    public Guid Id { get; private set; }
    public Guid LeaveRequestId { get; private set; }
    public Guid RevokedByUserId { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public Guid OperationId { get; private set; }
    public Guid? BalanceSettlementOperationId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public static LeaveRequestRevocation Create(Guid leaveRequestId, Guid revokedByUserId, string reason, Guid operationId, Guid? balanceSettlementOperationId, DateTime createdAtUtc) =>
        new(leaveRequestId, revokedByUserId, reason, operationId, balanceSettlementOperationId, createdAtUtc);

    private static string NormalizeRequired(string value, int maxLength, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Reason is required.", name);
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
