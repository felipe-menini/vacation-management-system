using Licenses.Application.LeaveManagement;
using Licenses.Domain.Identity;
using Licenses.Domain.LeaveManagement;
using Licenses.Domain.Organization;
using Licenses.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Licenses.Infrastructure.LeaveManagement;

public sealed class EfLeaveRequestRepository(ApplicationDbContext dbContext) : ILeaveRequestRepository
{
    public async Task<IDisposable> BeginTransactionAsync(CancellationToken cancellationToken) =>
        await dbContext.Database.BeginTransactionAsync(cancellationToken);

    public async Task CommitTransactionAsync(CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is not null) await dbContext.Database.CurrentTransaction.CommitAsync(cancellationToken);
    }

    public Task<IReadOnlyList<LeaveRequest>> ListByUserAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.LeaveRequests.AsNoTracking().Where(x => x.UserId == userId).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken).ContinueWith(t => (IReadOnlyList<LeaveRequest>)t.Result, cancellationToken);

    public Task<IReadOnlyList<LeaveRequest>> ListByUsersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
        dbContext.LeaveRequests.AsNoTracking().Where(x => userIds.Contains(x.UserId)).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken).ContinueWith(t => (IReadOnlyList<LeaveRequest>)t.Result, cancellationToken);

    public Task<LeaveRequest?> GetAsync(Guid id, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? dbContext.LeaveRequests : dbContext.LeaveRequests.AsNoTracking();
        return query.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<LeaveRequest?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM licenses.leave_requests WHERE id = {id} FOR UPDATE", cancellationToken);
        return await dbContext.LeaveRequests.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task AddAsync(LeaveRequest request, CancellationToken cancellationToken) => dbContext.LeaveRequests.AddAsync(request, cancellationToken).AsTask();
    public Task<User?> GetUserAsync(Guid id, CancellationToken cancellationToken) => dbContext.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<OrgUnit?> GetOrgUnitAsync(Guid id, CancellationToken cancellationToken) => dbContext.OrgUnits.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<LeaveType?> GetLeaveTypeAsync(Guid id, CancellationToken cancellationToken) => dbContext.LeaveTypes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<LeavePolicy?> GetPolicyAsync(Guid id, CancellationToken cancellationToken) => dbContext.LeavePolicies.AsNoTracking().Include(x => x.Versions).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<LeavePolicyVersion?> GetPolicyVersionAsync(Guid id, CancellationToken cancellationToken) => dbContext.LeavePolicyVersions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<WorkingCalendar?> GetWorkingCalendarAsync(Guid id, CancellationToken cancellationToken) => dbContext.WorkingCalendars.AsNoTracking().Include(x => x.Weekdays).Include(x => x.Exceptions).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public async Task<Guid?> GetBalanceAccountIdAsync(Guid userId, Guid balanceBucketId, CancellationToken cancellationToken) =>
        (await dbContext.BalanceAccounts.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId && x.BalanceBucketId == balanceBucketId, cancellationToken))?.Id;

    public Task<bool> HasEffectiveAssignmentAsync(Guid userId, Guid orgUnitId, DateOnly date, CancellationToken cancellationToken)
    {
        var at = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        return dbContext.UserOrgAssignments.AsNoTracking().AnyAsync(x => x.UserId == userId && x.OrgUnitId == orgUnitId && x.EffectiveFromUtc <= at && (x.EffectiveToUtc == null || x.EffectiveToUtc > at), cancellationToken);
    }

    public Task<IReadOnlyList<LeaveRequest>> ListOverlappingAsync(Guid userId, DateOnly startDate, DateOnly endDate, Guid excludingRequestId, CancellationToken cancellationToken) =>
        dbContext.LeaveRequests.AsNoTracking()
            .Where(x => x.UserId == userId && x.Id != excludingRequestId && x.StartDate <= endDate && x.EndDate >= startDate && x.Status != LeaveRequestStatus.Draft)
            .ToListAsync(cancellationToken).ContinueWith(t => (IReadOnlyList<LeaveRequest>)t.Result, cancellationToken);

    public Task<IReadOnlyList<User>> ListUsersInOrgUnitsAsync(IReadOnlyCollection<Guid> orgUnitIds, DateTime utcNow, CancellationToken cancellationToken) =>
        dbContext.Users.AsNoTracking()
            .Where(user => user.IsActive && dbContext.UserOrgAssignments.Any(assignment => assignment.UserId == user.Id && orgUnitIds.Contains(assignment.OrgUnitId) && assignment.EffectiveFromUtc <= utcNow && (assignment.EffectiveToUtc == null || assignment.EffectiveToUtc > utcNow)))
            .ToListAsync(cancellationToken).ContinueWith(t => (IReadOnlyList<User>)t.Result, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
