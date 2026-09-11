namespace Licenses.Domain.LeaveManagement;

public enum LeaveRequestCancellationDecision { Approve = 1, Reject = 2 }

public sealed class LeaveRequestCancellation
{
    public const int ReasonMaxLength = 1000;
    public const int CommentMaxLength = 1000;

    private LeaveRequestCancellation() { }

    private LeaveRequestCancellation(Guid leaveRequestId, Guid requestedByUserId, string reason, Guid operationId, DateTime requestedAtUtc)
    {
        if (leaveRequestId == Guid.Empty) throw new ArgumentException("LeaveRequestId is required.", nameof(leaveRequestId));
        if (requestedByUserId == Guid.Empty) throw new ArgumentException("RequestedByUserId is required.", nameof(requestedByUserId));
        if (operationId == Guid.Empty) throw new ArgumentException("OperationId is required.", nameof(operationId));
        Id = Guid.NewGuid();
        LeaveRequestId = leaveRequestId;
        RequestedByUserId = requestedByUserId;
        Reason = NormalizeRequired(reason, ReasonMaxLength, nameof(reason));
        OperationId = operationId;
        RequestedAtUtc = EnsureUtc(requestedAtUtc, nameof(requestedAtUtc));
    }

    public Guid Id { get; private set; }
    public Guid LeaveRequestId { get; private set; }
    public Guid RequestedByUserId { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public Guid OperationId { get; private set; }
    public DateTime RequestedAtUtc { get; private set; }
    public LeaveRequestCancellationDecision? Decision { get; private set; }
    public Guid? DecidedByUserId { get; private set; }
    public string? DecisionComment { get; private set; }
    public Guid? DecisionOperationId { get; private set; }
    public Guid? BalanceSettlementOperationId { get; private set; }
    public DateTime? DecidedAtUtc { get; private set; }

    public static LeaveRequestCancellation Create(Guid leaveRequestId, Guid requestedByUserId, string reason, Guid operationId, DateTime requestedAtUtc) =>
        new(leaveRequestId, requestedByUserId, reason, operationId, requestedAtUtc);

    public void Decide(LeaveRequestCancellationDecision decision, Guid decidedByUserId, string? comment, Guid decisionOperationId, Guid? balanceSettlementOperationId, DateTime decidedAtUtc)
    {
        if (!Enum.IsDefined(decision)) throw new ArgumentOutOfRangeException(nameof(decision), "Decision is invalid.");
        if (decidedByUserId == Guid.Empty) throw new ArgumentException("DecidedByUserId is required.", nameof(decidedByUserId));
        if (decisionOperationId == Guid.Empty) throw new ArgumentException("DecisionOperationId is required.", nameof(decisionOperationId));
        if (decision == LeaveRequestCancellationDecision.Reject && string.IsNullOrWhiteSpace(comment)) throw new ArgumentException("Cancellation rejection comment is required.", nameof(comment));
        if (Decision is not null) throw new InvalidOperationException("Cancellation request already has a decision.");
        Decision = decision;
        DecidedByUserId = decidedByUserId;
        DecisionComment = NormalizeOptional(comment, CommentMaxLength, nameof(comment));
        DecisionOperationId = decisionOperationId;
        BalanceSettlementOperationId = balanceSettlementOperationId;
        DecidedAtUtc = EnsureUtc(decidedAtUtc, nameof(decidedAtUtc));
    }

    private static string NormalizeRequired(string value, int maxLength, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Reason is required.", name);
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
