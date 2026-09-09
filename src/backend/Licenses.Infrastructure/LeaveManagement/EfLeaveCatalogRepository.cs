using Licenses.Application.LeaveManagement;
using Licenses.Domain.LeaveManagement;
using Licenses.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Licenses.Infrastructure.LeaveManagement;

public sealed class EfLeaveCatalogRepository(ApplicationDbContext dbContext) : ILeaveCatalogRepository
{
    public Task<List<LeaveType>> ListLeaveTypesAsync(bool? isActive, CancellationToken cancellationToken)
    {
        var query = dbContext.LeaveTypes.AsNoTracking();
        if (isActive is not null) query = query.Where(x => x.IsActive == isActive);
        return query.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public Task<LeaveType?> GetLeaveTypeAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.LeaveTypes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> LeaveTypeCodeExistsAsync(string code, Guid? excludingId, CancellationToken cancellationToken) =>
        dbContext.LeaveTypes.AnyAsync(x => x.Code == code && (excludingId == null || x.Id != excludingId), cancellationToken);

    public Task AddLeaveTypeAsync(LeaveType leaveType, CancellationToken cancellationToken) => dbContext.LeaveTypes.AddAsync(leaveType, cancellationToken).AsTask();

    public Task<List<BalanceBucket>> ListBalanceBucketsAsync(bool? isActive, CancellationToken cancellationToken)
    {
        var query = dbContext.BalanceBuckets.AsNoTracking();
        if (isActive is not null) query = query.Where(x => x.IsActive == isActive);
        return query.OrderBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public Task<BalanceBucket?> GetBalanceBucketAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.BalanceBuckets.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> BalanceBucketCodeExistsAsync(string code, Guid? excludingId, CancellationToken cancellationToken) =>
        dbContext.BalanceBuckets.AnyAsync(x => x.Code == code && (excludingId == null || x.Id != excludingId), cancellationToken);

    public Task AddBalanceBucketAsync(BalanceBucket balanceBucket, CancellationToken cancellationToken) => dbContext.BalanceBuckets.AddAsync(balanceBucket, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
