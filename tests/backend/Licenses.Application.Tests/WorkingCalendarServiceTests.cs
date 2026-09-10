using Licenses.Application.LeaveManagement;
using Licenses.Domain.LeaveManagement;

namespace Licenses.Application.Tests;

public sealed class WorkingCalendarServiceTests
{
    private readonly FakeWorkingCalendarRepository _repository = new();
    private readonly WorkingCalendarService _service;

    public WorkingCalendarServiceTests() => _service = new WorkingCalendarService(_repository, TimeProvider.System);

    [Fact]
    public async Task CreatesCalendarAndCalculatesBusinessDaysWithExceptions()
    {
        var calendar = await _service.CreateCalendarAsync(new(" standard ", "Standard", null, true, Weekdays()), CancellationToken.None);
        await _service.CreateExceptionAsync(calendar.Id, new(new DateOnly(2026, 8, 12), "Sample holiday", false), CancellationToken.None);

        var result = await _service.CalculateAsync("BUSINESS_DAYS", calendar.Id, new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 14), CancellationToken.None);

        Assert.Equal("STANDARD", calendar.Code);
        Assert.Equal(4, result.CalculatedDays);
        Assert.Contains(result.Details, x => x.Date == new DateOnly(2026, 8, 12) && !x.IsCounted);
    }

    [Fact]
    public async Task CalculatesCalendarDaysWithoutCalendarAndRejectsReversedRange()
    {
        var result = await _service.CalculateAsync("CALENDAR_DAYS", null, new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 14), CancellationToken.None);

        Assert.Equal(5, result.CalculatedDays);
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CalculateAsync("CALENDAR_DAYS", null, new DateOnly(2026, 8, 14), new DateOnly(2026, 8, 10), CancellationToken.None));
    }

    private static List<UpsertWorkingCalendarWeekdayCommand> Weekdays() =>
    [
        new("Sunday", false),
        new("Monday", true),
        new("Tuesday", true),
        new("Wednesday", true),
        new("Thursday", true),
        new("Friday", true),
        new("Saturday", false)
    ];

    private sealed class FakeWorkingCalendarRepository : IWorkingCalendarRepository
    {
        private readonly List<WorkingCalendar> _calendars = [];

        public Task<List<WorkingCalendar>> ListCalendarsAsync(CancellationToken cancellationToken) => Task.FromResult(_calendars.ToList());
        public Task<WorkingCalendar?> GetCalendarAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(_calendars.SingleOrDefault(x => x.Id == id));
        public Task<WorkingCalendarException?> GetExceptionAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(_calendars.SelectMany(x => x.Exceptions).SingleOrDefault(x => x.Id == id));
        public Task<bool> CodeExistsAsync(string normalizedCode, Guid? excludingCalendarId, CancellationToken cancellationToken) => Task.FromResult(_calendars.Any(x => x.Code == normalizedCode && (excludingCalendarId is null || x.Id != excludingCalendarId)));
        public Task<bool> ExceptionDateExistsAsync(Guid calendarId, DateOnly date, Guid? excludingExceptionId, CancellationToken cancellationToken) => Task.FromResult(_calendars.Single(x => x.Id == calendarId).Exceptions.Any(x => x.Date == date && (excludingExceptionId is null || x.Id != excludingExceptionId)));
        public Task AddCalendarAsync(WorkingCalendar calendar, CancellationToken cancellationToken) { _calendars.Add(calendar); return Task.CompletedTask; }
        public Task AddExceptionAsync(WorkingCalendarException exception, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
