using Licenses.Application.LeaveManagement;
using Licenses.Domain.LeaveManagement;
using Licenses.Domain.Organization;
using Licenses.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Licenses.Infrastructure.LeaveManagement;

public sealed class EfLeavePolicyRepository(ApplicationDbContext dbContext) : ILeavePolicyRepository
{
    public Task<List<LeavePolicy>> ListPoliciesAsync(CancellationToken cancellationToken) =>
        dbContext.LeavePolicies.AsNoTracking().OrderBy(x => x.LeaveTypeId).ThenBy(x => x.OrgUnitId).ToListAsync(cancellationToken);

    public Task<LeavePolicy?> GetPolicyAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.LeavePolicies.Include(x => x.Versions).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<LeavePolicyVersion?> GetVersionAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.LeavePolicyVersions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<List<LeavePolicyVersion>> ListVersionsAsync(Guid policyId, CancellationToken cancellationToken) =>
        dbContext.LeavePolicyVersions.AsNoTracking().Where(x => x.LeavePolicyId == policyId).OrderBy(x => x.VersionNumber).ToListAsync(cancellationToken);

    public Task<bool> ExactPolicyScopeExistsAsync(Guid leaveTypeId, Guid? orgUnitId, Guid? excludingPolicyId, CancellationToken cancellationToken) =>
        dbContext.LeavePolicies.AnyAsync(x => x.LeaveTypeId == leaveTypeId && x.OrgUnitId == orgUnitId && (excludingPolicyId == null || x.Id != excludingPolicyId), cancellationToken);

    public Task<bool> HasPublishedVersionsAsync(Guid policyId, CancellationToken cancellationToken) =>
        dbContext.LeavePolicyVersions.AnyAsync(x => x.LeavePolicyId == policyId && x.Status == LeavePolicyVersionStatus.Published, cancellationToken);

    public async Task<int> GetNextVersionNumberAsync(Guid policyId, CancellationToken cancellationToken)
    {
        var current = await dbContext.LeavePolicyVersions.Where(x => x.LeavePolicyId == policyId).Select(x => (int?)x.VersionNumber).MaxAsync(cancellationToken);
        return (current ?? 0) + 1;
    }

    public Task<bool> HasOverlappingPublishedVersionAsync(Guid policyId, DateOnly effectiveFrom, DateOnly? effectiveTo, Guid? excludingVersionId, CancellationToken cancellationToken) =>
        dbContext.LeavePolicyVersions.AnyAsync(x =>
            x.LeavePolicyId == policyId &&
            x.Status == LeavePolicyVersionStatus.Published &&
            (excludingVersionId == null || x.Id != excludingVersionId) &&
            x.EffectiveFrom <= (effectiveTo ?? DateOnly.MaxValue) &&
            (x.EffectiveTo ?? DateOnly.MaxValue) >= effectiveFrom, cancellationToken);

    public Task<List<LeavePolicy>> ListPoliciesForLeaveTypeWithPublishedVersionsAsync(Guid leaveTypeId, DateOnly date, CancellationToken cancellationToken) =>
        dbContext.LeavePolicies
            .Include(x => x.Versions.Where(v => v.Status == LeavePolicyVersionStatus.Published && v.EffectiveFrom <= date && (v.EffectiveTo == null || v.EffectiveTo >= date)))
            .Where(x => x.LeaveTypeId == leaveTypeId && x.IsActive && x.Versions.Any(v => v.Status == LeavePolicyVersionStatus.Published && v.EffectiveFrom <= date && (v.EffectiveTo == null || v.EffectiveTo >= date)))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public Task AddPolicyAsync(LeavePolicy policy, CancellationToken cancellationToken) => dbContext.LeavePolicies.AddAsync(policy, cancellationToken).AsTask();
    public Task AddVersionAsync(LeavePolicyVersion version, CancellationToken cancellationToken) => dbContext.LeavePolicyVersions.AddAsync(version, cancellationToken).AsTask();
    public Task<LeaveType?> GetLeaveTypeAsync(Guid id, CancellationToken cancellationToken) => dbContext.LeaveTypes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<BalanceBucket?> GetBalanceBucketAsync(Guid id, CancellationToken cancellationToken) => dbContext.BalanceBuckets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<WorkingCalendar?> GetWorkingCalendarAsync(Guid id, CancellationToken cancellationToken) => dbContext.WorkingCalendars.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<OrgUnit?> GetOrgUnitAsync(Guid id, CancellationToken cancellationToken) => dbContext.OrgUnits.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<List<OrgUnit>> ListOrgUnitsAsync(CancellationToken cancellationToken) => dbContext.OrgUnits.AsNoTracking().ToListAsync(cancellationToken);
    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
