using Licenses.Domain.Identity;
using Licenses.Domain.LeaveManagement;
using Licenses.Domain.Organization;

namespace Licenses.Application.LeaveManagement;

public sealed record LeaveRequestDto(Guid Id, Guid UserId, string? UserDisplayName, Guid OrgUnitId, string? OrgUnitCode, string? OrgUnitName, Guid LeaveTypeId, string? LeaveTypeCode, string? LeaveTypeName, Guid? LeavePolicyVersionId, DateOnly StartDate, DateOnly EndDate, string DayPortion, decimal? CalculatedDays, string Status, string? Comment, Guid? BalanceAccountId, Guid? BalanceReservationOperationId, Guid CreatedByUserId, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, DateTime? SubmittedAtUtc);
public sealed record CreateLeaveRequestCommand(Guid OrgUnitId, Guid LeaveTypeId, DateOnly StartDate, DateOnly EndDate, string DayPortion, string? Comment);
public sealed record UpdateLeaveRequestCommand(Guid OrgUnitId, Guid LeaveTypeId, DateOnly StartDate, DateOnly EndDate, string DayPortion, string? Comment);
public sealed record SubmitLeaveRequestResultDto(LeaveRequestDto Request, IReadOnlyList<string> Warnings, bool WasAlreadySubmitted);

public interface ILeaveRequestRepository
{
    Task<IReadOnlyList<LeaveRequest>> ListByUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<LeaveRequest>> ListByUsersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);
    Task<LeaveRequest?> GetAsync(Guid id, bool tracking, CancellationToken cancellationToken);
    Task<LeaveRequest?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken);
    Task AddAsync(LeaveRequest request, CancellationToken cancellationToken);
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