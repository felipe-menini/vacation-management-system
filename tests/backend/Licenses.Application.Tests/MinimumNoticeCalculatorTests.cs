using Licenses.Application.Common;
using Licenses.Application.LeaveManagement;
using Licenses.Domain.LeaveManagement;

namespace Licenses.Application.Tests;

public sealed class MinimumNoticeCalculatorTests
{
    private static readonly TimeZoneInfo Montevideo = TimeZoneInfo.FindSystemTimeZoneById("America/Montevideo");

    [Fact]
    public async Task BusinessTodayIsDerivedFromConfiguredTimezone()
    {
        var calculator = Calculator(new DateTimeOffset(2026, 9, 11, 1, 30, 0, TimeSpan.Zero), Montevideo);

        var result = await calculator.CalculateAsync(new(new DateOnly(2026, 9, 11), PolicyDayCountMode.CalendarDays, null), CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 9, 10), result.BusinessToday);
        Assert.Equal(1, result.NoticeDays);
    }

    [Fact]
    public async Task CalendarNoticeCountsDaysBeforeStartDate()
    {
        var calculator = Calculator(new DateTimeOffset(2026, 9, 11, 15, 0, 0, TimeSpan.Zero), Montevideo);

        Assert.Equal(0, (await calculator.CalculateAsync(new(new DateOnly(2026, 9, 11), PolicyDayCountMode.CalendarDays, null), CancellationToken.None)).NoticeDays);
        Assert.Equal(1, (await calculator.CalculateAsync(new(new DateOnly(2026, 9, 12), PolicyDayCountMode.CalendarDays, null), CancellationToken.None)).NoticeDays);
        Assert.Equal(5, (await calculator.CalculateAsync(new(new DateOnly(2026, 9, 16), PolicyDayCountMode.CalendarDays, null), CancellationToken.None)).NoticeDays);
    }

    [Fact]
    public async Task PastStartDateIsExplicitFailure()
    {
        var calculator = Calculator(new DateTimeOffset(2026, 9, 11, 15, 0, 0, TimeSpan.Zero), Montevideo);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            calculator.CalculateAsync(new(new DateOnly(2026, 9, 10), PolicyDayCountMode.CalendarDays, null), CancellationToken.None));
    }

    [Fact]
    public async Task BusinessNoticeCountsWorkingDaysBeforeStartDateOnly()
    {
        var calendar = StandardCalendar();
        var repository = new FakeWorkingCalendarRepository(calendar);
        var calculator = Calculator(new DateTimeOffset(2026, 9, 11, 15, 0, 0, TimeSpan.Zero), Montevideo, repository);

        var result = await calculator.CalculateAsync(new(new DateOnly(2026, 9, 14), PolicyDayCountMode.BusinessDays, calendar.Id), CancellationToken.None);

        Assert.Equal(1, result.NoticeDays);
    }

    [Fact]
    public async Task BusinessNoticeHonorsHolidayAndWorkingExceptions()
    {
        var calendar = StandardCalendar();
        calendar.AddException(new DateOnly(2026, 9, 11), "Current-day holiday", false, DateTime.UtcNow);
        calendar.AddException(new DateOnly(2026, 9, 12), "Working Saturday", true, DateTime.UtcNow);
        var repository = new FakeWorkingCalendarRepository(calendar);
        var calculator = Calculator(new DateTimeOffset(2026, 9, 11, 15, 0, 0, TimeSpan.Zero), Montevideo, repository);

        var result = await calculator.CalculateAsync(new(new DateOnly(2026, 9, 14), PolicyDayCountMode.BusinessDays, calendar.Id), CancellationToken.None);

        Assert.Equal(1, result.NoticeDays);
    }

    [Fact]
    public async Task BusinessNoticeRequiresExistingWorkingCalendar()
    {
        var calculator = Calculator(new DateTimeOffset(2026, 9, 11, 15, 0, 0, TimeSpan.Zero), Montevideo);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            calculator.CalculateAsync(new(new DateOnly(2026, 9, 14), PolicyDayCountMode.BusinessDays, null), CancellationToken.None));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            calculator.CalculateAsync(new(new DateOnly(2026, 9, 14), PolicyDayCountMode.BusinessDays, Guid.NewGuid()), CancellationToken.None));
    }

    private static MinimumNoticeCalculator Calculator(DateTimeOffset utcNow, TimeZoneInfo timeZone, IWorkingCalendarRepository? repository = null) =>
        new(new BusinessDateProvider(new FakeClock(utcNow), timeZone), repository ?? new FakeWorkingCalendarRepository());

    private static WorkingCalendar StandardCalendar() =>
        WorkingCalendar.Create("STANDARD", "Standard", null, true, new Dictionary<DayOfWeek, bool>
        {
            [DayOfWeek.Sunday] = false,
            [DayOfWeek.Monday] = true,
            [DayOfWeek.Tuesday] = true,
            [DayOfWeek.Wednesday] = true,
            [DayOfWeek.Thursday] = true,
            [DayOfWeek.Friday] = true,
            [DayOfWeek.Saturday] = false
        }, DateTime.UtcNow);

    private sealed class FakeClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class FakeWorkingCalendarRepository(params WorkingCalendar[] calendars) : IWorkingCalendarRepository
    {
        public Task<List<WorkingCalendar>> ListCalendarsAsync(CancellationToken cancellationToken) => Task.FromResult(calendars.ToList());
        public Task<WorkingCalendar?> GetCalendarAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(calendars.SingleOrDefault(x => x.Id == id));
        public Task<WorkingCalendarException?> GetExceptionAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(calendars.SelectMany(x => x.Exceptions).SingleOrDefault(x => x.Id == id));
        public Task<bool> CodeExistsAsync(string normalizedCode, Guid? excludingCalendarId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<bool> ExceptionDateExistsAsync(Guid calendarId, DateOnly date, Guid? excludingExceptionId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task AddCalendarAsync(WorkingCalendar calendar, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task AddExceptionAsync(WorkingCalendarException exception, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
