namespace Licenses.Domain.LeaveManagement;

public enum LeaveRequestDecisionKind { Approve = 1, Reject = 2 }

public sealed class LeaveRequestDecision
{
    public const int CommentMaxLength = 1000;

    private LeaveRequestDecision() { }

    private LeaveRequestDecision(Guid leaveRequestId, LeaveRequestDecisionKind decision, Guid decidedByUserId, string? comment, Guid operationId, Guid? balanceSettlementOperationId, DateTime createdAtUtc)
    {
        if (leaveRequestId == Guid.Empty) throw new ArgumentException("LeaveRequestId is required.", nameof(leaveRequestId));
        if (!Enum.IsDefined(decision)) throw new ArgumentOutOfRangeException(nameof(decision), "Decision is invalid.");
        if (decidedByUserId == Guid.Empty) throw new ArgumentException("DecidedByUserId is required.", nameof(decidedByUserId));
        if (operationId == Guid.Empty) throw new ArgumentException("OperationId is required.", nameof(operationId));
        if (decision == LeaveRequestDecisionKind.Reject && string.IsNullOrWhiteSpace(comment)) throw new ArgumentException("Rejection comment is required.", nameof(comment));

        Id = Guid.NewGuid();
        LeaveRequestId = leaveRequestId;
        Decision = decision;
        DecidedByUserId = decidedByUserId;
        Comment = NormalizeOptional(comment, CommentMaxLength, nameof(comment));
        OperationId = operationId;
        BalanceSettlementOperationId = balanceSettlementOperationId;
        CreatedAtUtc = EnsureUtc(createdAtUtc, nameof(createdAtUtc));
    }

    public Guid Id { get; private set; }
    public Guid LeaveRequestId { get; private set; }
    public LeaveRequestDecisionKind Decision { get; private set; }
    public Guid DecidedByUserId { get; private set; }
    public string? Comment { get; private set; }
    public Guid OperationId { get; private set; }
    public Guid? BalanceSettlementOperationId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public static LeaveRequestDecision Create(Guid leaveRequestId, LeaveRequestDecisionKind decision, Guid decidedByUserId, string? comment, Guid operationId, Guid? balanceSettlementOperationId, DateTime createdAtUtc) =>
        new(leaveRequestId, decision, decidedByUserId, comment, operationId, balanceSettlementOperationId, createdAtUtc);

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
