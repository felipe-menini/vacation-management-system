using Licenses.Domain.LeaveManagement;

namespace Licenses.Application.LeaveManagement;

public sealed record LeaveCalendarQuery(DateOnly From, DateOnly To, Guid? OrgUnitId, int Page = 1, int PageSize = 50);
public sealed record LeaveCalendarPageDto(int Page, int PageSize, int TotalCount, IReadOnlyList<LeaveCalendarEntryDto> Items);
public sealed record LeaveCalendarEntryDto(Guid LeaveRequestId, Guid SubjectUserId, string? SubjectDisplayName, Guid OrgUnitId, string? OrgUnitName, DateOnly StartDate, DateOnly EndDate, string DayPortion, string Status);
public sealed record LeaveCalendarQueryCriteria(DateOnly From, DateOnly To, IReadOnlyCollection<Guid> OrgUnitIds, int Page, int PageSize);

public interface ILeaveCalendarReader
{
    Task<IReadOnlySet<Guid>?> ExpandOrgUnitScopeAsync(Guid rootOrgUnitId, CancellationToken cancellationToken);
    Task<LeaveCalendarPageDto> QueryAsync(LeaveCalendarQueryCriteria criteria, CancellationToken cancellationToken);
}

public static class LeaveCalendarFormatting
{
    public static string ToDayPortion(LeaveRequestDayPortion value) => value switch
    {
        LeaveRequestDayPortion.FullDay => "FULL_DAY",
        LeaveRequestDayPortion.HalfDay => "HALF_DAY",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    public static string ToStatus(LeaveRequestStatus value) => value switch
    {
        LeaveRequestStatus.Approved => "APPROVED",
        LeaveRequestStatus.CancellationRequested => "CANCELLATION_REQUESTED",
        LeaveRequestStatus.Completed => "COMPLETED",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
}
