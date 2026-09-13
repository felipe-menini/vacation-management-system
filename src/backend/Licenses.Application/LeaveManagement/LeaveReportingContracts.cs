using Licenses.Domain.LeaveManagement;

namespace Licenses.Application.LeaveManagement;

public sealed record LeaveSummaryReportQuery(DateOnly From, DateOnly To, Guid? OrgUnitId);
public sealed record LeaveSummaryReportDto(
    DateOnly From,
    DateOnly To,
    Guid? OrgUnitId,
    LeaveReportCurrentWorkloadDto CurrentWorkload,
    LeaveReportPeriodDto Period,
    IReadOnlyList<LeaveReportOrgUnitBreakdownDto> OrgUnitBreakdown);

public sealed record LeaveReportCurrentWorkloadDto(int PendingApprovalCount, int CancellationRequestedCount);
public sealed record LeaveReportPeriodDto(
    int ApprovedAbsenceCount,
    int CompletedAbsenceCount,
    int CancellationRequestedAbsenceCount,
    int ApprovedOrEffectiveAbsenceCount,
    int UniqueEmployeesWithApprovedOrCompletedAbsence);

public sealed record LeaveReportOrgUnitBreakdownDto(Guid OrgUnitId, string? OrgUnitName, int ApprovedOrEffectiveAbsenceCount, int UniqueEmployeeCount);
public sealed record LeaveSummaryReportCriteria(DateOnly From, DateOnly To, Guid? OrgUnitId, IReadOnlyCollection<Guid> OrgUnitIds);

public interface ILeaveReportingReader
{
    Task<IReadOnlySet<Guid>?> ExpandOrgUnitScopeAsync(Guid rootOrgUnitId, CancellationToken cancellationToken);
    Task<LeaveSummaryReportDto> QuerySummaryAsync(LeaveSummaryReportCriteria criteria, CancellationToken cancellationToken);
}

public static class LeaveReportingStatusSets
{
    public static readonly LeaveRequestStatus[] EffectiveAbsenceStatuses =
    [
        LeaveRequestStatus.Approved,
        LeaveRequestStatus.Completed,
        LeaveRequestStatus.CancellationRequested
    ];

    public static readonly LeaveRequestStatus[] ApprovedOrCompletedStatuses =
    [
        LeaveRequestStatus.Approved,
        LeaveRequestStatus.Completed
    ];
}
