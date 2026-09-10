using Licenses.Domain.LeaveManagement;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Licenses.Infrastructure.Tests;

public sealed class WorkingCalendarPersistenceTests
{
    [Fact]
    public async Task EnforcesWorkingCalendarUniquenessAndChecksAgainstPostgreSql()
    {
        await PostgreSqlTestDatabase.WithFreshDatabaseAsync(async db =>
        {
            var calendar = await InsertCalendarAsync(db, "STANDARD");

            await Assert.ThrowsAsync<DbUpdateException>(() => InsertCalendarAsync(db, "STANDARD"));
            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO licenses.working_calendar_weekdays (working_calendar_id, day_of_week, is_working_day)
                VALUES ({calendar.Id}, 7, true);
                """));
        });
    }

    [Fact]
    public async Task EnforcesUniqueWeekdayAndExceptionDateAgainstPostgreSql()
    {
        await PostgreSqlTestDatabase.WithFreshDatabaseAsync(async db =>
        {
            var calendar = await InsertCalendarAsync(db, "STANDARD");
            await InsertExceptionAsync(db, calendar.Id, new DateOnly(2026, 8, 12));

            await Assert.ThrowsAsync<PostgresException>(() => InsertWeekdayAsync(db, calendar.Id, 1, false));
            await Assert.ThrowsAsync<PostgresException>(() => InsertExceptionAsync(db, calendar.Id, new DateOnly(2026, 8, 12)));
        });
    }

    [Fact]
    public async Task ProtectsCalendarReferencesAgainstPostgreSql()
    {
        await PostgreSqlTestDatabase.WithFreshDatabaseAsync(async db =>
        {
            var calendar = await InsertCalendarAsync(db, "STANDARD");
            await InsertExceptionAsync(db, calendar.Id, new DateOnly(2026, 8, 12));

            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM licenses.working_calendars WHERE id = {calendar.Id};"));
        });
    }

    private static async Task<WorkingCalendar> InsertCalendarAsync(DbContext db, string code)
    {
        var calendar = WorkingCalendar.Create(code, code, null, true, new Dictionary<DayOfWeek, bool>
        {
            [DayOfWeek.Sunday] = false,
            [DayOfWeek.Monday] = true,
            [DayOfWeek.Tuesday] = true,
            [DayOfWeek.Wednesday] = true,
            [DayOfWeek.Thursday] = true,
            [DayOfWeek.Friday] = true,
            [DayOfWeek.Saturday] = false
        }, DateTime.UtcNow);
        db.Set<WorkingCalendar>().Add(calendar);
        await db.SaveChangesAsync();
        return calendar;
    }

    private static Task InsertWeekdayAsync(DbContext db, Guid calendarId, int dayOfWeek, bool isWorkingDay) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO licenses.working_calendar_weekdays (working_calendar_id, day_of_week, is_working_day)
            VALUES ({calendarId}, {dayOfWeek}, {isWorkingDay});
            """);

    private static Task InsertExceptionAsync(DbContext db, Guid calendarId, DateOnly date) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO licenses.working_calendar_exceptions (id, working_calendar_id, date, name, is_working_day, created_at_utc, updated_at_utc)
            VALUES ({Guid.NewGuid()}, {calendarId}, {date}, 'Sample exception', false, {DateTime.UtcNow}, {DateTime.UtcNow});
            """);
}
