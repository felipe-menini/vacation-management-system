namespace Licenses.Domain.LeaveManagement;

public sealed record DayCalculationDetail(DateOnly Date, bool IsCounted);
public sealed record DayCalculationResult(DateOnly StartDate, DateOnly EndDate, PolicyDayCountMode DayCountMode, decimal CalculatedDays, IReadOnlyList<DayCalculationDetail> Details);

public sealed class DayCalculator
{
    public DayCalculationResult Calculate(DateOnly startDate, DateOnly endDate, PolicyDayCountMode mode, WorkingCalendar? calendar)
    {
        if (endDate < startDate) throw new ArgumentException("EndDate must be greater than or equal to StartDate.");
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode), "Day count mode is invalid.");
        if (mode == PolicyDayCountMode.BusinessDays && calendar is null) throw new InvalidOperationException("WorkingCalendarId is required for BUSINESS_DAYS calculation.");

        var details = new List<DayCalculationDetail>();
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            var counted = mode == PolicyDayCountMode.CalendarDays || calendar!.IsWorkingDay(date);
            details.Add(new DayCalculationDetail(date, counted));
        }

        return new DayCalculationResult(startDate, endDate, mode, details.Count(x => x.IsCounted), details);
    }
}
