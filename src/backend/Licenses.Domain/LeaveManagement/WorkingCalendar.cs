namespace Licenses.Domain.LeaveManagement;

public sealed class WorkingCalendar
{
    private readonly List<WorkingCalendarWeekday> _weekdays = [];
    private readonly List<WorkingCalendarException> _exceptions = [];

    private WorkingCalendar() { }

    private WorkingCalendar(Guid id, string code, string name, string? description, bool isActive, DateTime createdAtUtc)
    {
        Id = id == Guid.Empty ? throw new ArgumentException("Id is required.", nameof(id)) : id;
        Code = NormalizeCode(code);
        Name = Required(name, nameof(name));
        Description = NormalizeOptional(description);
        IsActive = isActive;
        CreatedAtUtc = EnsureUtc(createdAtUtc, nameof(createdAtUtc));
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public IReadOnlyCollection<WorkingCalendarWeekday> Weekdays => _weekdays;
    public IReadOnlyCollection<WorkingCalendarException> Exceptions => _exceptions;

    public static WorkingCalendar Create(string code, string name, string? description, bool isActive, IReadOnlyDictionary<DayOfWeek, bool> weekdays, DateTime createdAtUtc)
    {
        var calendar = new WorkingCalendar(Guid.NewGuid(), code, name, description, isActive, createdAtUtc);
        calendar.ReplaceWeekdays(weekdays);
        return calendar;
    }

    public void UpdateDetails(string name, string? description, bool isActive, IReadOnlyDictionary<DayOfWeek, bool> weekdays, DateTime updatedAtUtc)
    {
        Name = Required(name, nameof(name));
        Description = NormalizeOptional(description);
        IsActive = isActive;
        ReplaceWeekdays(weekdays);
        UpdatedAtUtc = EnsureUtc(updatedAtUtc, nameof(updatedAtUtc));
    }

    public WorkingCalendarException AddException(DateOnly date, string name, bool isWorkingDay, DateTime createdAtUtc)
    {
        if (_exceptions.Any(x => x.Date == date)) throw new InvalidOperationException("An exception already exists for this calendar date.");
        var exception = WorkingCalendarException.Create(Id, date, name, isWorkingDay, createdAtUtc);
        _exceptions.Add(exception);
        UpdatedAtUtc = EnsureUtc(createdAtUtc, nameof(createdAtUtc));
        return exception;
    }

    public bool IsWorkingDay(DateOnly date)
    {
        var exception = _exceptions.SingleOrDefault(x => x.Date == date);
        if (exception is not null) return exception.IsWorkingDay;
        return _weekdays.Single(x => x.DayOfWeek == date.DayOfWeek).IsWorkingDay;
    }

    private void ReplaceWeekdays(IReadOnlyDictionary<DayOfWeek, bool> weekdays)
    {
        var expected = Enum.GetValues<DayOfWeek>();
        if (weekdays.Count != expected.Length || expected.Any(day => !weekdays.ContainsKey(day)))
            throw new InvalidOperationException("A working calendar must define exactly one rule for each weekday.");

        _weekdays.Clear();
        foreach (var day in expected.OrderBy(x => (int)x))
        {
            _weekdays.Add(WorkingCalendarWeekday.Create(Id, day, weekdays[day]));
        }
    }

    public static string NormalizeCode(string value)
    {
        var normalized = Required(value, nameof(value)).Trim().ToUpperInvariant();
        if (normalized.Length > 64) throw new ArgumentOutOfRangeException(nameof(value), "Code must be 64 characters or fewer.");
        return normalized;
    }

    internal static string Required(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Value is required.", name);
        return value.Trim();
    }

    internal static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    internal static DateTime EnsureUtc(DateTime value, string name)
    {
        if (value.Kind != DateTimeKind.Utc) throw new ArgumentException("Timestamp must be UTC.", name);
        return value;
    }
}

public sealed class WorkingCalendarWeekday
{
    private WorkingCalendarWeekday() { }

    private WorkingCalendarWeekday(Guid workingCalendarId, DayOfWeek dayOfWeek, bool isWorkingDay)
    {
        WorkingCalendarId = workingCalendarId == Guid.Empty ? throw new ArgumentException("Working calendar is required.", nameof(workingCalendarId)) : workingCalendarId;
        DayOfWeek = Enum.IsDefined(dayOfWeek) ? dayOfWeek : throw new ArgumentOutOfRangeException(nameof(dayOfWeek));
        IsWorkingDay = isWorkingDay;
    }

    public Guid WorkingCalendarId { get; private set; }
    public DayOfWeek DayOfWeek { get; private set; }
    public bool IsWorkingDay { get; private set; }

    public static WorkingCalendarWeekday Create(Guid workingCalendarId, DayOfWeek dayOfWeek, bool isWorkingDay) => new(workingCalendarId, dayOfWeek, isWorkingDay);
}

public sealed class WorkingCalendarException
{
    private WorkingCalendarException() { }

    private WorkingCalendarException(Guid id, Guid workingCalendarId, DateOnly date, string name, bool isWorkingDay, DateTime createdAtUtc)
    {
        Id = id == Guid.Empty ? throw new ArgumentException("Id is required.", nameof(id)) : id;
        WorkingCalendarId = workingCalendarId == Guid.Empty ? throw new ArgumentException("Working calendar is required.", nameof(workingCalendarId)) : workingCalendarId;
        Date = date;
        Name = WorkingCalendar.Required(name, nameof(name));
        IsWorkingDay = isWorkingDay;
        CreatedAtUtc = WorkingCalendar.EnsureUtc(createdAtUtc, nameof(createdAtUtc));
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid WorkingCalendarId { get; private set; }
    public DateOnly Date { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsWorkingDay { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public static WorkingCalendarException Create(Guid workingCalendarId, DateOnly date, string name, bool isWorkingDay, DateTime createdAtUtc) =>
        new(Guid.NewGuid(), workingCalendarId, date, name, isWorkingDay, createdAtUtc);

    public void Update(DateOnly date, string name, bool isWorkingDay, DateTime updatedAtUtc)
    {
        Date = date;
        Name = WorkingCalendar.Required(name, nameof(name));
        IsWorkingDay = isWorkingDay;
        UpdatedAtUtc = WorkingCalendar.EnsureUtc(updatedAtUtc, nameof(updatedAtUtc));
    }
}
