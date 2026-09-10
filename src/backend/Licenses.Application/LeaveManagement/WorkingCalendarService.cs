using Licenses.Domain.LeaveManagement;

namespace Licenses.Application.LeaveManagement;

public sealed class WorkingCalendarService(IWorkingCalendarRepository repository, TimeProvider timeProvider)
{
    private readonly DayCalculator _calculator = new();

    public async Task<IReadOnlyList<WorkingCalendarDto>> ListCalendarsAsync(CancellationToken cancellationToken) =>
        (await repository.ListCalendarsAsync(cancellationToken)).Select(ToDto).ToList();

    public async Task<WorkingCalendarDto?> GetCalendarAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.GetCalendarAsync(id, cancellationToken) is { } calendar ? ToDto(calendar) : null;

    public async Task<WorkingCalendarDto> CreateCalendarAsync(CreateWorkingCalendarCommand command, CancellationToken cancellationToken)
    {
        var code = WorkingCalendar.NormalizeCode(command.Code);
        if (await repository.CodeExistsAsync(code, null, cancellationToken)) throw new InvalidOperationException("A working calendar with this code already exists.");
        var calendar = WorkingCalendar.Create(code, command.Name, command.Description, command.IsActive ?? true, ParseWeekdays(command.Weekdays), UtcNow());
        await repository.AddCalendarAsync(calendar, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return ToDto(calendar);
    }

    public async Task<WorkingCalendarDto?> UpdateCalendarAsync(Guid id, UpdateWorkingCalendarCommand command, CancellationToken cancellationToken)
    {
        var calendar = await repository.GetCalendarAsync(id, cancellationToken);
        if (calendar is null) return null;
        calendar.UpdateDetails(command.Name, command.Description, command.IsActive, ParseWeekdays(command.Weekdays), UtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return ToDto(calendar);
    }

    public async Task<IReadOnlyList<WorkingCalendarExceptionDto>?> ListExceptionsAsync(Guid calendarId, CancellationToken cancellationToken)
    {
        var calendar = await repository.GetCalendarAsync(calendarId, cancellationToken);
        return calendar?.Exceptions.OrderBy(x => x.Date).Select(ToExceptionDto).ToList();
    }

    public async Task<WorkingCalendarExceptionDto> CreateExceptionAsync(Guid calendarId, UpsertWorkingCalendarExceptionCommand command, CancellationToken cancellationToken)
    {
        var calendar = await repository.GetCalendarAsync(calendarId, cancellationToken) ?? throw new InvalidOperationException("Working calendar does not exist.");
        if (await repository.ExceptionDateExistsAsync(calendarId, command.Date, null, cancellationToken)) throw new InvalidOperationException("An exception already exists for this calendar date.");
        var exception = calendar.AddException(command.Date, command.Name, command.IsWorkingDay, UtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return ToExceptionDto(exception);
    }

    public async Task<WorkingCalendarExceptionDto?> UpdateExceptionAsync(Guid id, UpsertWorkingCalendarExceptionCommand command, CancellationToken cancellationToken)
    {
        var exception = await repository.GetExceptionAsync(id, cancellationToken);
        if (exception is null) return null;
        if (await repository.ExceptionDateExistsAsync(exception.WorkingCalendarId, command.Date, id, cancellationToken)) throw new InvalidOperationException("An exception already exists for this calendar date.");
        exception.Update(command.Date, command.Name, command.IsWorkingDay, UtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return ToExceptionDto(exception);
    }

    public async Task<DayCalculationDto> CalculateAsync(string mode, Guid? workingCalendarId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken)
    {
        var parsedMode = LeavePolicyService.ParseDayCountMode(mode);
        WorkingCalendar? calendar = null;
        if (parsedMode == PolicyDayCountMode.BusinessDays)
        {
            if (workingCalendarId is null) throw new InvalidOperationException("WorkingCalendarId is required for BUSINESS_DAYS calculation.");
            calendar = await repository.GetCalendarAsync(workingCalendarId.Value, cancellationToken) ?? throw new InvalidOperationException("Working calendar does not exist.");
        }

        var result = _calculator.Calculate(startDate, endDate, parsedMode, calendar);
        return new(result.StartDate, result.EndDate, ToDayCountCode(result.DayCountMode), workingCalendarId, result.CalculatedDays, result.Details.Select(x => new DayCalculationDetailDto(x.Date, x.IsCounted)).ToList());
    }

    private static WorkingCalendarDto ToDto(WorkingCalendar calendar) =>
        new(calendar.Id, calendar.Code, calendar.Name, calendar.Description, calendar.IsActive, calendar.Weekdays.OrderBy(x => (int)x.DayOfWeek).Select(x => new WorkingCalendarWeekdayDto(x.DayOfWeek.ToString(), x.IsWorkingDay)).ToList(), calendar.Exceptions.OrderBy(x => x.Date).Select(ToExceptionDto).ToList(), calendar.CreatedAtUtc, calendar.UpdatedAtUtc);

    private static WorkingCalendarExceptionDto ToExceptionDto(WorkingCalendarException exception) =>
        new(exception.Id, exception.WorkingCalendarId, exception.Date, exception.Name, exception.IsWorkingDay, exception.CreatedAtUtc, exception.UpdatedAtUtc);

    private static Dictionary<DayOfWeek, bool> ParseWeekdays(IReadOnlyList<UpsertWorkingCalendarWeekdayCommand> weekdays)
    {
        var result = new Dictionary<DayOfWeek, bool>();
        foreach (var item in weekdays)
        {
            if (!Enum.TryParse<DayOfWeek>(item.DayOfWeek, ignoreCase: true, out var day) || !Enum.IsDefined(day)) throw new ArgumentException("DayOfWeek is invalid.");
            if (!result.TryAdd(day, item.IsWorkingDay)) throw new InvalidOperationException("A calendar cannot contain duplicate weekday rules.");
        }
        return result;
    }

    private static string ToDayCountCode(PolicyDayCountMode mode) => mode switch { PolicyDayCountMode.BusinessDays => "BUSINESS_DAYS", PolicyDayCountMode.CalendarDays => "CALENDAR_DAYS", _ => throw new ArgumentOutOfRangeException(nameof(mode)) };
    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;
}
