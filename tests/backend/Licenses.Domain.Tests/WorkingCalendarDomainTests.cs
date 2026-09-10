using Licenses.Domain.LeaveManagement;

namespace Licenses.Domain.Tests;

public sealed class WorkingCalendarDomainTests
{
    [Fact]
    public void WorkingCalendarNormalizesCodeAndRequiresEveryWeekday()
    {
        var calendar = StandardCalendar(code: " standard_uy_dev ");

        Assert.Equal("STANDARD_UY_DEV", calendar.Code);
        Assert.Equal(7, calendar.Weekdays.Count);
        Assert.Throws<InvalidOperationException>(() => WorkingCalendar.Create("BROKEN", "Broken", null, true, new Dictionary<DayOfWeek, bool> { [DayOfWeek.Monday] = true }, DateTime.UtcNow));
    }

    [Fact]
    public void ExceptionOverridesWeekdayConfiguration()
    {
        var calendar = StandardCalendar();
        calendar.AddException(new DateOnly(2026, 8, 12), "Sample holiday", false, DateTime.UtcNow);
        calendar.AddException(new DateOnly(2026, 8, 15), "Sample working Saturday", true, DateTime.UtcNow);

        Assert.True(calendar.IsWorkingDay(new DateOnly(2026, 8, 10)));
        Assert.False(calendar.IsWorkingDay(new DateOnly(2026, 8, 9)));
        Assert.False(calendar.IsWorkingDay(new DateOnly(2026, 8, 12)));
        Assert.True(calendar.IsWorkingDay(new DateOnly(2026, 8, 15)));
    }

    [Fact]
    public void DayCalculatorCountsInclusiveCalendarAndBusinessDays()
    {
        var calculator = new DayCalculator();
        var calendar = StandardCalendar();
        calendar.AddException(new DateOnly(2026, 8, 12), "Sample holiday", false, DateTime.UtcNow);

        Assert.Equal(5, calculator.Calculate(new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 14), PolicyDayCountMode.CalendarDays, null).CalculatedDays);
        Assert.Equal(4, calculator.Calculate(new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 14), PolicyDayCountMode.BusinessDays, calendar).CalculatedDays);
        Assert.Equal(2, calculator.Calculate(new DateOnly(2026, 8, 14), new DateOnly(2026, 8, 17), PolicyDayCountMode.BusinessDays, calendar).CalculatedDays);

        calendar.AddException(new DateOnly(2026, 8, 15), "Sample working Saturday", true, DateTime.UtcNow);
        Assert.Equal(1, calculator.Calculate(new DateOnly(2026, 8, 15), new DateOnly(2026, 8, 16), PolicyDayCountMode.BusinessDays, calendar).CalculatedDays);
    }

    [Fact]
    public void DayCalculatorRejectsReversedRangeAndBusinessDaysWithoutCalendar()
    {
        var calculator = new DayCalculator();

        Assert.Throws<ArgumentException>(() => calculator.Calculate(new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 1), PolicyDayCountMode.CalendarDays, null));
        Assert.Throws<InvalidOperationException>(() => calculator.Calculate(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 2), PolicyDayCountMode.BusinessDays, null));
    }

    private static WorkingCalendar StandardCalendar(string code = "STANDARD") =>
        WorkingCalendar.Create(code, "Standard", null, true, new Dictionary<DayOfWeek, bool>
        {
            [DayOfWeek.Sunday] = false,
            [DayOfWeek.Monday] = true,
            [DayOfWeek.Tuesday] = true,
            [DayOfWeek.Wednesday] = true,
            [DayOfWeek.Thursday] = true,
            [DayOfWeek.Friday] = true,
            [DayOfWeek.Saturday] = false
        }, DateTime.UtcNow);
}
