using Licenses.Application.LeaveManagement;
using Licenses.Domain.LeaveManagement;
using Licenses.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Licenses.Infrastructure.LeaveManagement;

public sealed class EfWorkingCalendarRepository(ApplicationDbContext dbContext) : IWorkingCalendarRepository
{
    public Task<List<WorkingCalendar>> ListCalendarsAsync(CancellationToken cancellationToken) =>
        dbContext.WorkingCalendars.Include(x => x.Weekdays).Include(x => x.Exceptions).AsNoTracking().OrderBy(x => x.Code).ToListAsync(cancellationToken);

    public Task<WorkingCalendar?> GetCalendarAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.WorkingCalendars.Include(x => x.Weekdays).Include(x => x.Exceptions).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<WorkingCalendarException?> GetExceptionAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.WorkingCalendarExceptions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> CodeExistsAsync(string normalizedCode, Guid? excludingCalendarId, CancellationToken cancellationToken) =>
        dbContext.WorkingCalendars.AnyAsync(x => x.Code == normalizedCode && (excludingCalendarId == null || x.Id != excludingCalendarId), cancellationToken);

    public Task<bool> ExceptionDateExistsAsync(Guid calendarId, DateOnly date, Guid? excludingExceptionId, CancellationToken cancellationToken) =>
        dbContext.WorkingCalendarExceptions.AnyAsync(x => x.WorkingCalendarId == calendarId && x.Date == date && (excludingExceptionId == null || x.Id != excludingExceptionId), cancellationToken);

    public Task AddCalendarAsync(WorkingCalendar calendar, CancellationToken cancellationToken) => dbContext.WorkingCalendars.AddAsync(calendar, cancellationToken).AsTask();
    public Task AddExceptionAsync(WorkingCalendarException exception, CancellationToken cancellationToken) => dbContext.WorkingCalendarExceptions.AddAsync(exception, cancellationToken).AsTask();
    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
