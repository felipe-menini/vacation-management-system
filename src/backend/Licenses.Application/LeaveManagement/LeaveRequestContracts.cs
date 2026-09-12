using Licenses.Domain.Identity;
using Licenses.Domain.LeaveManagement;
using Licenses.Domain.Organization;

namespace Licenses.Application.LeaveManagement;

public sealed record LeaveRequestDocumentDto(Guid Id, Guid LeaveRequestId, string Kind, string OriginalFileName, string ContentType, long SizeBytes, string Sha256, Guid UploadedByUserId, string? UploadedByUserDisplayName, DateTime CreatedAtUtc);
public sealed record UploadedPrivateDocument(string StorageKey, string Sha256, long SizeBytes);
public sealed record PrivateDocumentReadStream(Stream Content, string ContentType, long SizeBytes) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}

public interface IPrivateDocumentStorage
{
    Task<UploadedPrivateDocument> StoreAsync(string storageKey, Stream content, CancellationToken cancellationToken);
    Task<PrivateDocumentReadStream> OpenReadAsync(string storageKey, string contentType, CancellationToken cancellationToken);
}

public sealed record LeaveRequestDecisionDto(Guid Id, Guid LeaveRequestId, string Decision, Guid DecidedByUserId, string? DecidedByUserDisplayName, string? Comment, Guid OperationId, Guid? BalanceSettlementOperationId, DateTime CreatedAtUtc);
public sealed record LeaveRequestCancellationDto(Guid Id, Guid LeaveRequestId, Guid RequestedByUserId, string? RequestedByUserDisplayName, string Reason, Guid OperationId, DateTime RequestedAtUtc, string? Decision, Guid? DecidedByUserId, string? DecidedByUserDisplayName, string? DecisionComment, Guid? DecisionOperationId, Guid? BalanceSettlementOperationId, DateTime? DecidedAtUtc);
public sealed record LeaveRequestRevocationDto(Guid Id, Guid LeaveRequestId, Guid RevokedByUserId, string? RevokedByUserDisplayName, string Reason, Guid OperationId, Guid? BalanceSettlementOperationId, DateTime CreatedAtUtc);
public sealed record LeaveRequestDto(Guid Id, Guid UserId, string? UserDisplayName, Guid OrgUnitId, string? OrgUnitCode, string? OrgUnitName, Guid LeaveTypeId, string? LeaveTypeCode, string? LeaveTypeName, Guid? LeavePolicyVersionId, DateOnly StartDate, DateOnly EndDate, string DayPortion, decimal? CalculatedDays, string Status, string? Comment, Guid? BalanceAccountId, Guid? BalanceReservationOperationId, Guid? SubmissionOperationId, Guid CreatedByUserId, string? CreatedByUserDisplayName, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, DateTime? SubmittedAtUtc, DateTime? DecidedAtUtc, DateTime? CancellationRequestedAtUtc, DateTime? CancellationDecidedAtUtc, DateTime? RevokedAtUtc, DateTime? CompletedAtUtc, LeaveRequestDecisionDto? Decision, LeaveRequestCancellationDto? Cancellation, LeaveRequestRevocationDto? Revocation, IReadOnlyList<LeaveRequestDocumentDto> Documents);
public sealed record CreateLeaveRequestCommand(Guid OrgUnitId, Guid LeaveTypeId, DateOnly StartDate, DateOnly EndDate, string DayPortion, string? Comment);
public sealed record CreateLeaveRequestForUserCommand(Guid OrgUnitId, Guid LeaveTypeId, DateOnly StartDate, DateOnly EndDate, string DayPortion, string? Comment, Guid SubmissionOperationId);
public sealed record UpdateLeaveRequestCommand(Guid OrgUnitId, Guid LeaveTypeId, DateOnly StartDate, DateOnly EndDate, string DayPortion, string? Comment);
public sealed record SubmitLeaveRequestResultDto(LeaveRequestDto Request, IReadOnlyList<string> Warnings, bool WasAlreadySubmitted);
public sealed record DecideLeaveRequestCommand(Guid OperationId, string? Comment);
public sealed record DecideLeaveRequestResultDto(LeaveRequestDto Request, LeaveRequestDecisionDto Decision, bool WasAlreadyApplied);
public sealed record RequestCancellationCommand(Guid OperationId, string Reason);
public sealed record RequestCancellationResultDto(LeaveRequestDto Request, LeaveRequestCancellationDto Cancellation, bool WasAlreadyApplied);
public sealed record DecideCancellationCommand(Guid OperationId, string? Comment);
public sealed record DecideCancellationResultDto(LeaveRequestDto Request, LeaveRequestCancellationDto Cancellation, bool WasAlreadyApplied);
public sealed record RevokeLeaveRequestCommand(Guid OperationId, string Reason);
public sealed record RevokeLeaveRequestResultDto(LeaveRequestDto Request, LeaveRequestRevocationDto Revocation, bool WasAlreadyApplied);
public sealed record CompleteEligibleLeaveRequestsResultDto(DateOnly BusinessToday, int RequestedBatchSize, int ScannedCount, int CompletedCount, int SkippedCount, IReadOnlyList<Guid> CompletedRequestIds);

public interface ILeaveRequestRepository
{
    Task<IReadOnlyList<LeaveRequest>> ListByUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<LeaveRequest>> ListByUsersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);
    Task<IReadOnlyList<LeaveRequest>> ListPendingByOrgUnitsAsync(IReadOnlyCollection<Guid> orgUnitIds, CancellationToken cancellationToken);
    Task<IReadOnlyList<LeaveRequest>> ListPendingCancellationByOrgUnitsAsync(IReadOnlyCollection<Guid> orgUnitIds, CancellationToken cancellationToken);
    Task<IReadOnlyList<LeaveRequest>> ListEligibleApprovedForCompletionAsync(DateOnly businessToday, int limit, CancellationToken cancellationToken);
    Task<LeaveRequest?> GetAsync(Guid id, bool tracking, CancellationToken cancellationToken);
    Task<LeaveRequest?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken);
    Task<LeaveRequest?> GetBySubmissionOperationIdAsync(Guid operationId, bool tracking, CancellationToken cancellationToken);
    Task<LeaveRequestDocument?> GetDocumentAsync(Guid id, bool tracking, CancellationToken cancellationToken);
    Task<IReadOnlyList<LeaveRequestDocument>> ListDocumentsByRequestIdAsync(Guid requestId, CancellationToken cancellationToken);
    Task<IReadOnlyList<LeaveRequestDocument>> ListDocumentsByRequestIdsAsync(IReadOnlyCollection<Guid> requestIds, CancellationToken cancellationToken);
    Task AddDocumentAsync(LeaveRequestDocument document, CancellationToken cancellationToken);
    Task<LeaveRequestDecision?> GetDecisionByOperationIdAsync(Guid operationId, CancellationToken cancellationToken);
    Task<LeaveRequestDecision?> GetDecisionByRequestIdAsync(Guid requestId, CancellationToken cancellationToken);
    Task<IReadOnlyList<LeaveRequestDecision>> ListDecisionsByRequestIdsAsync(IReadOnlyCollection<Guid> requestIds, CancellationToken cancellationToken);
    Task<LeaveRequestCancellation?> GetCancellationByOperationIdAsync(Guid operationId, CancellationToken cancellationToken);
    Task<LeaveRequestCancellation?> GetCancellationByDecisionOperationIdAsync(Guid operationId, CancellationToken cancellationToken);
    Task<LeaveRequestCancellation?> GetCancellationByRequestIdAsync(Guid requestId, CancellationToken cancellationToken);
    Task<IReadOnlyList<LeaveRequestCancellation>> ListCancellationsByRequestIdsAsync(IReadOnlyCollection<Guid> requestIds, CancellationToken cancellationToken);
    Task<LeaveRequestRevocation?> GetRevocationByOperationIdAsync(Guid operationId, CancellationToken cancellationToken);
    Task<LeaveRequestRevocation?> GetRevocationByRequestIdAsync(Guid requestId, CancellationToken cancellationToken);
    Task<IReadOnlyList<LeaveRequestRevocation>> ListRevocationsByRequestIdsAsync(IReadOnlyCollection<Guid> requestIds, CancellationToken cancellationToken);
    Task AddAsync(LeaveRequest request, CancellationToken cancellationToken);
    Task AddDecisionAsync(LeaveRequestDecision decision, CancellationToken cancellationToken);
    Task AddCancellationAsync(LeaveRequestCancellation cancellation, CancellationToken cancellationToken);
    Task AddRevocationAsync(LeaveRequestRevocation revocation, CancellationToken cancellationToken);
    Task<User?> GetUserAsync(Guid id, CancellationToken cancellationToken);
    Task<OrgUnit?> GetOrgUnitAsync(Guid id, CancellationToken cancellationToken);
    Task<LeaveType?> GetLeaveTypeAsync(Guid id, CancellationToken cancellationToken);
    Task<LeavePolicy?> GetPolicyAsync(Guid id, CancellationToken cancellationToken);
    Task<LeavePolicyVersion?> GetPolicyVersionAsync(Guid id, CancellationToken cancellationToken);
    Task<WorkingCalendar?> GetWorkingCalendarAsync(Guid id, CancellationToken cancellationToken);
    Task<Guid?> GetBalanceAccountIdAsync(Guid userId, Guid balanceBucketId, CancellationToken cancellationToken);
    Task<bool> HasEffectiveAssignmentAsync(Guid userId, Guid orgUnitId, DateOnly date, CancellationToken cancellationToken);
    Task<IReadOnlyList<LeaveRequest>> ListOverlappingAsync(Guid userId, DateOnly startDate, DateOnly endDate, Guid excludingRequestId, CancellationToken cancellationToken);
    Task<IReadOnlyList<User>> ListUsersInOrgUnitsAsync(IReadOnlyCollection<Guid> orgUnitIds, DateTime utcNow, CancellationToken cancellationToken);
    Task<IDisposable> BeginTransactionAsync(CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
    Task CommitTransactionAsync(CancellationToken cancellationToken);
}
