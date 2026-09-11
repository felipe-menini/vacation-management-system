using Licenses.Application.Audit;
using Licenses.Application.Authorization;
using Licenses.Application.LeaveManagement;
using Licenses.Domain.LeaveManagement;

namespace Licenses.Application.Tests;

public sealed class WorkingCalendarServiceTests
{
    private readonly FakeWorkingCalendarRepository _repository = new();
    private readonly FakeAuditWriter _audit = new();
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly WorkingCalendarService _service;

    public WorkingCalendarServiceTests() => _service = new WorkingCalendarService(_repository, TimeProvider.System, new FixedCurrentActor(_actorId), _audit);

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

    [Fact]
    public async Task AdministrativeCalendarMutationsProduceFocusedAuditEvents()
    {
        var calendar = await _service.CreateCalendarAsync(new("standard", "Standard", null, true, Weekdays()), CancellationToken.None);
        await _service.UpdateCalendarAsync(calendar.Id, new("Standard Updated", null, true, Weekdays()), CancellationToken.None);
        var exception = await _service.CreateExceptionAsync(calendar.Id, new(new DateOnly(2026, 8, 12), "Sample holiday", false), CancellationToken.None);
        await _service.UpdateExceptionAsync(exception.Id, new(new DateOnly(2026, 8, 13), "Moved holiday", false), CancellationToken.None);

        Assert.Equal([
            "leave.calendar.create",
            "leave.calendar.update",
            "leave.calendar.exception.create",
            "leave.calendar.exception.update"
        ], _audit.Events.Select(x => x.Action));
        Assert.All(_audit.Events, x => Assert.Equal(_actorId, x.ActorUserId));
        Assert.DoesNotContain(_audit.Events, x => x.MetadataJson?.Contains("Weekdays", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task FailedCalendarMutationDoesNotProduceAuditEvent()
    {
        await _service.CreateCalendarAsync(new("standard", "Standard", null, true, Weekdays()), CancellationToken.None);
        _audit.Events.Clear();

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateCalendarAsync(new("standard", "Duplicate", null, true, Weekdays()), CancellationToken.None));

        Assert.Empty(_audit.Events);
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

    private sealed class FakeAuditWriter : IAuditWriter
    {
        public List<AuditEventData> Events { get; } = [];
        public Task WriteAsync(AuditEventData auditEvent, CancellationToken cancellationToken)
        {
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedCurrentActor(Guid userId) : ICurrentActor { public Guid? UserId => userId; }
}
