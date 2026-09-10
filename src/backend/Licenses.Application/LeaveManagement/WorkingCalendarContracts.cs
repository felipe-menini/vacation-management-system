using Licenses.Domain.LeaveManagement;

namespace Licenses.Application.LeaveManagement;

public sealed record WorkingCalendarWeekdayDto(string DayOfWeek, bool IsWorkingDay);
public sealed record WorkingCalendarExceptionDto(Guid Id, Guid WorkingCalendarId, DateOnly Date, string Name, bool IsWorkingDay, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);
public sealed record WorkingCalendarDto(Guid Id, string Code, string Name, string? Description, bool IsActive, IReadOnlyList<WorkingCalendarWeekdayDto> Weekdays, IReadOnlyList<WorkingCalendarExceptionDto> Exceptions, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);
public sealed record UpsertWorkingCalendarWeekdayCommand(string DayOfWeek, bool IsWorkingDay);
public sealed record CreateWorkingCalendarCommand(string Code, string Name, string? Description, bool? IsActive, IReadOnlyList<UpsertWorkingCalendarWeekdayCommand> Weekdays);
public sealed record UpdateWorkingCalendarCommand(string Name, string? Description, bool IsActive, IReadOnlyList<UpsertWorkingCalendarWeekdayCommand> Weekdays);
public sealed record UpsertWorkingCalendarExceptionCommand(DateOnly Date, string Name, bool IsWorkingDay);
public sealed record DayCalculationDto(DateOnly StartDate, DateOnly EndDate, string DayCountMode, Guid? WorkingCalendarId, decimal CalculatedDays, IReadOnlyList<DayCalculationDetailDto> Details);
public sealed record DayCalculationDetailDto(DateOnly Date, bool IsCounted);

public interface IWorkingCalendarRepository
{
    Task<List<WorkingCalendar>> ListCalendarsAsync(CancellationToken cancellationToken);
    Task<WorkingCalendar?> GetCalendarAsync(Guid id, CancellationToken cancellationToken);
    Task<WorkingCalendarException?> GetExceptionAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> CodeExistsAsync(string normalizedCode, Guid? excludingCalendarId, CancellationToken cancellationToken);
    Task<bool> ExceptionDateExistsAsync(Guid calendarId, DateOnly date, Guid? excludingExceptionId, CancellationToken cancellationToken);
    Task AddCalendarAsync(WorkingCalendar calendar, CancellationToken cancellationToken);
    Task AddExceptionAsync(WorkingCalendarException exception, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
