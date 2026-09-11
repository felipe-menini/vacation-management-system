namespace Licenses.Domain.LeaveManagement;

public enum LeaveRequestDayPortion { FullDay = 1, HalfDay = 2 }
public enum LeaveRequestStatus { Draft = 1, PendingApproval = 2, Approved = 3, Rejected = 4, CancellationRequested = 5, Cancelled = 6, Revoked = 7, Completed = 8 }

public sealed class LeaveRequest
{
    public const int CommentMaxLength = 1000;
    private LeaveRequest() { }

    private LeaveRequest(Guid id, Guid userId, Guid orgUnitId, Guid leaveTypeId, DateOnly startDate, DateOnly endDate, LeaveRequestDayPortion dayPortion, string? comment, Guid createdByUserId, DateTime createdAtUtc)
    {
        Id = id == Guid.Empty ? throw new ArgumentException("Id is required.", nameof(id)) : id;
        UserId = userId == Guid.Empty ? throw new ArgumentException("UserId is required.", nameof(userId)) : userId;
        OrgUnitId = orgUnitId == Guid.Empty ? throw new ArgumentException("OrgUnitId is required.", nameof(orgUnitId)) : orgUnitId;
        LeaveTypeId = leaveTypeId == Guid.Empty ? throw new ArgumentException("LeaveTypeId is required.", nameof(leaveTypeId)) : leaveTypeId;
        CreatedByUserId = createdByUserId == Guid.Empty ? throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId)) : createdByUserId;
        Status = LeaveRequestStatus.Draft;
        CreatedAtUtc = EnsureUtc(createdAtUtc, nameof(createdAtUtc));
        UpdatedAtUtc = CreatedAtUtc;
        ApplyDraftFields(orgUnitId, leaveTypeId, startDate, endDate, dayPortion, comment);
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid OrgUnitId { get; private set; }
    public Guid LeaveTypeId { get; private set; }
    public Guid? LeavePolicyVersionId { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public LeaveRequestDayPortion DayPortion { get; private set; }
    public decimal? CalculatedDays { get; private set; }
    public LeaveRequestStatus Status { get; private set; }
    public string? Comment { get; private set; }
    public Guid? BalanceAccountId { get; private set; }
    public Guid? BalanceReservationOperationId { get; private set; }
    public Guid? SubmissionOperationId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public DateTime? SubmittedAtUtc { get; private set; }
    public DateTime? DecidedAtUtc { get; private set; }
    public DateTime? CancellationRequestedAtUtc { get; private set; }
    public DateTime? CancellationDecidedAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }

    public static LeaveRequest CreateDraft(Guid userId, Guid orgUnitId, Guid leaveTypeId, DateOnly startDate, DateOnly endDate, LeaveRequestDayPortion dayPortion, string? comment, Guid createdByUserId, DateTime createdAtUtc) =>
        new(Guid.NewGuid(), userId, orgUnitId, leaveTypeId, startDate, endDate, dayPortion, comment, createdByUserId, createdAtUtc);

    public void UpdateDraft(Guid orgUnitId, Guid leaveTypeId, DateOnly startDate, DateOnly endDate, LeaveRequestDayPortion dayPortion, string? comment, DateTime updatedAtUtc)
    {
        EnsureDraft();
        ApplyDraftFields(orgUnitId, leaveTypeId, startDate, endDate, dayPortion, comment);
        UpdatedAtUtc = EnsureUtc(updatedAtUtc, nameof(updatedAtUtc));
    }

    public Guid EnsureReservationOperationId()
    {
        BalanceReservationOperationId ??= Guid.NewGuid();
        return BalanceReservationOperationId.Value;
    }

    public void Submit(Guid leavePolicyVersionId, decimal calculatedDays, Guid? balanceAccountId, Guid? reservationOperationId, DateTime submittedAtUtc, Guid? submissionOperationId = null)
    {
        EnsureDraft();
        if (leavePolicyVersionId == Guid.Empty) throw new ArgumentException("LeavePolicyVersionId is required.", nameof(leavePolicyVersionId));
        if (calculatedDays <= 0m) throw new ArgumentOutOfRangeException(nameof(calculatedDays), "Calculated days must be positive.");
        if (submissionOperationId == Guid.Empty) throw new ArgumentException("SubmissionOperationId cannot be empty.", nameof(submissionOperationId));
        if (balanceAccountId is null && reservationOperationId is not null) throw new InvalidOperationException("Reservation operation requires a balance account.");
        if (balanceAccountId is not null && reservationOperationId is null) throw new InvalidOperationException("Balance account requires a reservation operation.");
        LeavePolicyVersionId = leavePolicyVersionId;
        CalculatedDays = calculatedDays;
        BalanceAccountId = balanceAccountId;
        BalanceReservationOperationId = reservationOperationId;
        SubmissionOperationId = submissionOperationId ?? reservationOperationId;
        SubmittedAtUtc = EnsureUtc(submittedAtUtc, nameof(submittedAtUtc));
        UpdatedAtUtc = SubmittedAtUtc.Value;
        Status = LeaveRequestStatus.PendingApproval;
    }

    public void Approve(DateTime decidedAtUtc)
    {
        EnsurePendingApproval();
        DecidedAtUtc = EnsureUtc(decidedAtUtc, nameof(decidedAtUtc));
        UpdatedAtUtc = DecidedAtUtc.Value;
        Status = LeaveRequestStatus.Approved;
    }

    public void Reject(DateTime decidedAtUtc)
    {
        EnsurePendingApproval();
        DecidedAtUtc = EnsureUtc(decidedAtUtc, nameof(decidedAtUtc));
        UpdatedAtUtc = DecidedAtUtc.Value;
        Status = LeaveRequestStatus.Rejected;
    }

    public void RequestCancellation(DateTime requestedAtUtc)
    {
        EnsureApproved("Only APPROVED leave requests can request cancellation.");
        CancellationRequestedAtUtc = EnsureUtc(requestedAtUtc, nameof(requestedAtUtc));
        UpdatedAtUtc = CancellationRequestedAtUtc.Value;
        Status = LeaveRequestStatus.CancellationRequested;
    }

    public void ApproveCancellation(DateTime decidedAtUtc)
    {
        EnsureCancellationRequested();
        CancellationDecidedAtUtc = EnsureUtc(decidedAtUtc, nameof(decidedAtUtc));
        UpdatedAtUtc = CancellationDecidedAtUtc.Value;
        Status = LeaveRequestStatus.Cancelled;
    }

    public void RejectCancellation(DateTime decidedAtUtc)
    {
        EnsureCancellationRequested();
        CancellationDecidedAtUtc = EnsureUtc(decidedAtUtc, nameof(decidedAtUtc));
        UpdatedAtUtc = CancellationDecidedAtUtc.Value;
        Status = LeaveRequestStatus.Approved;
    }

    public void Revoke(DateTime revokedAtUtc)
    {
        EnsureApproved("Only APPROVED leave requests can be revoked.");
        RevokedAtUtc = EnsureUtc(revokedAtUtc, nameof(revokedAtUtc));
        UpdatedAtUtc = RevokedAtUtc.Value;
        Status = LeaveRequestStatus.Revoked;
    }

    public static bool IsActiveOverlapStatus(LeaveRequestStatus status) => status is LeaveRequestStatus.PendingApproval or LeaveRequestStatus.Approved or LeaveRequestStatus.CancellationRequested;

    private void ApplyDraftFields(Guid orgUnitId, Guid leaveTypeId, DateOnly startDate, DateOnly endDate, LeaveRequestDayPortion dayPortion, string? comment)
    {
        if (orgUnitId == Guid.Empty) throw new ArgumentException("OrgUnitId is required.", nameof(orgUnitId));
        if (leaveTypeId == Guid.Empty) throw new ArgumentException("LeaveTypeId is required.", nameof(leaveTypeId));
        if (endDate < startDate) throw new ArgumentException("EndDate must be greater than or equal to StartDate.");
        if (!Enum.IsDefined(dayPortion)) throw new ArgumentOutOfRangeException(nameof(dayPortion), "Day portion is invalid.");
        if (dayPortion == LeaveRequestDayPortion.HalfDay && startDate != endDate) throw new InvalidOperationException("HALF_DAY requests must start and end on the same date.");
        OrgUnitId = orgUnitId;
        LeaveTypeId = leaveTypeId;
        StartDate = startDate;
        EndDate = endDate;
        DayPortion = dayPortion;
        Comment = NormalizeOptional(comment, CommentMaxLength, nameof(comment));
    }

    private void EnsureDraft()
    {
        if (Status != LeaveRequestStatus.Draft) throw new InvalidOperationException("Only DRAFT leave requests can be edited or submitted by this operation.");
    }

    private void EnsurePendingApproval()
    {
        if (Status != LeaveRequestStatus.PendingApproval) throw new InvalidOperationException("Only PENDING_APPROVAL leave requests can be decided.");
    }

    private void EnsureApproved(string message)
    {
        if (Status != LeaveRequestStatus.Approved) throw new InvalidOperationException(message);
    }

    private void EnsureCancellationRequested()
    {
        if (Status != LeaveRequestStatus.CancellationRequested) throw new InvalidOperationException("Only CANCELLATION_REQUESTED leave requests can be decided.");
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
