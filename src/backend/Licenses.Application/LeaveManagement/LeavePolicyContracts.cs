using Licenses.Domain.LeaveManagement;
using Licenses.Domain.Organization;

namespace Licenses.Application.LeaveManagement;

public sealed record LeavePolicyDto(Guid Id, Guid LeaveTypeId, string LeaveTypeCode, string LeaveTypeName, Guid? OrgUnitId, string? OrgUnitCode, string? OrgUnitName, bool AppliesToDescendants, bool IsActive, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);
public sealed record CreateLeavePolicyCommand(Guid LeaveTypeId, Guid? OrgUnitId, bool AppliesToDescendants, bool? IsActive);
public sealed record UpdateLeavePolicyCommand(Guid? OrgUnitId, bool AppliesToDescendants, bool IsActive);

public sealed record LeavePolicyVersionDto(Guid Id, Guid LeavePolicyId, int VersionNumber, string Status, DateOnly EffectiveFrom, DateOnly? EffectiveTo, string DayCountMode, bool AllowHalfDay, int? MinimumNoticeDays, string NoticeDayCountMode, decimal? MaximumRequestDays, string OverlapBehavior, bool ConsumesBalance, Guid? BalanceBucketId, string? BalanceBucketCode, string? BalanceBucketName, Guid? WorkingCalendarId, string? WorkingCalendarCode, string? WorkingCalendarName, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, DateTime? PublishedAtUtc);
public sealed record CreateLeavePolicyVersionCommand(DateOnly EffectiveFrom, DateOnly? EffectiveTo, string DayCountMode, bool AllowHalfDay, int? MinimumNoticeDays, string NoticeDayCountMode, decimal? MaximumRequestDays, string OverlapBehavior, bool ConsumesBalance, Guid? BalanceBucketId, Guid? WorkingCalendarId);
public sealed record UpdateLeavePolicyVersionCommand(DateOnly EffectiveFrom, DateOnly? EffectiveTo, string DayCountMode, bool AllowHalfDay, int? MinimumNoticeDays, string NoticeDayCountMode, decimal? MaximumRequestDays, string OverlapBehavior, bool ConsumesBalance, Guid? BalanceBucketId, Guid? WorkingCalendarId);
public sealed record ResolveLeavePolicyResultDto(bool Found, LeavePolicyDto? Policy, LeavePolicyVersionDto? Version, string? Reason);

public interface ILeavePolicyRepository
{
    Task<List<LeavePolicy>> ListPoliciesAsync(CancellationToken cancellationToken);
    Task<LeavePolicy?> GetPolicyAsync(Guid id, CancellationToken cancellationToken);
    Task<LeavePolicyVersion?> GetVersionAsync(Guid id, CancellationToken cancellationToken);
    Task<List<LeavePolicyVersion>> ListVersionsAsync(Guid policyId, CancellationToken cancellationToken);
    Task<bool> ExactPolicyScopeExistsAsync(Guid leaveTypeId, Guid? orgUnitId, Guid? excludingPolicyId, CancellationToken cancellationToken);
    Task<bool> HasPublishedVersionsAsync(Guid policyId, CancellationToken cancellationToken);
    Task<int> GetNextVersionNumberAsync(Guid policyId, CancellationToken cancellationToken);
    Task<bool> HasOverlappingPublishedVersionAsync(Guid policyId, DateOnly effectiveFrom, DateOnly? effectiveTo, Guid? excludingVersionId, CancellationToken cancellationToken);
    Task<List<LeavePolicy>> ListPoliciesForLeaveTypeWithPublishedVersionsAsync(Guid leaveTypeId, DateOnly date, CancellationToken cancellationToken);
    Task AddPolicyAsync(LeavePolicy policy, CancellationToken cancellationToken);
    Task AddVersionAsync(LeavePolicyVersion version, CancellationToken cancellationToken);
    Task<LeaveType?> GetLeaveTypeAsync(Guid id, CancellationToken cancellationToken);
    Task<BalanceBucket?> GetBalanceBucketAsync(Guid id, CancellationToken cancellationToken);
    Task<WorkingCalendar?> GetWorkingCalendarAsync(Guid id, CancellationToken cancellationToken);
    Task<OrgUnit?> GetOrgUnitAsync(Guid id, CancellationToken cancellationToken);
    Task<List<OrgUnit>> ListOrgUnitsAsync(CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
