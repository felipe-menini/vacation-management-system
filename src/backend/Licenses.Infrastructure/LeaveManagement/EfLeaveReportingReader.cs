using Licenses.Application.LeaveManagement;
using Licenses.Domain.LeaveManagement;
using Licenses.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Licenses.Infrastructure.LeaveManagement;

public sealed class EfLeaveReportingReader(ApplicationDbContext dbContext) : ILeaveReportingReader
{
    public async Task<IReadOnlySet<Guid>?> ExpandOrgUnitScopeAsync(Guid rootOrgUnitId, CancellationToken cancellationToken)
    {
        var units = await dbContext.OrgUnits.AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => new { x.Id, x.ParentId })
            .ToListAsync(cancellationToken);
        if (units.All(x => x.Id != rootOrgUnitId)) return null;

        var result = new HashSet<Guid> { rootOrgUnitId };
        var childrenByParent = units.Where(x => x.ParentId is not null).GroupBy(x => x.ParentId!.Value).ToDictionary(x => x.Key, x => x.Select(y => y.Id).ToList());
        AddDescendants(rootOrgUnitId, childrenByParent, result);
        return result;
    }

    public async Task<LeaveSummaryReportDto> QuerySummaryAsync(LeaveSummaryReportCriteria criteria, CancellationToken cancellationToken)
    {
        var orgUnitIds = criteria.OrgUnitIds.ToList();
        var scopedRequests = dbContext.LeaveRequests.AsNoTracking()
            .Where(request => orgUnitIds.Contains(request.OrgUnitId));

        var currentWorkload = new LeaveReportCurrentWorkloadDto(
            await scopedRequests.CountAsync(request => request.Status == LeaveRequestStatus.PendingApproval, cancellationToken),
            await scopedRequests.CountAsync(request => request.Status == LeaveRequestStatus.CancellationRequested, cancellationToken));

        var periodRequests = scopedRequests
            .Where(request =>
                LeaveReportingStatusSets.EffectiveAbsenceStatuses.Contains(request.Status) &&
                request.StartDate <= criteria.To &&
                request.EndDate >= criteria.From);

        var period = new LeaveReportPeriodDto(
            await periodRequests.CountAsync(request => request.Status == LeaveRequestStatus.Approved, cancellationToken),
            await periodRequests.CountAsync(request => request.Status == LeaveRequestStatus.Completed, cancellationToken),
            await periodRequests.CountAsync(request => request.Status == LeaveRequestStatus.CancellationRequested, cancellationToken),
            await periodRequests.CountAsync(cancellationToken),
            await periodRequests
                .Where(request => LeaveReportingStatusSets.ApprovedOrCompletedStatuses.Contains(request.Status))
                .Select(request => request.UserId)
                .Distinct()
                .CountAsync(cancellationToken));

        var breakdown = await periodRequests
            .GroupBy(request => request.OrgUnitId)
            .Select(group => new
            {
                OrgUnitId = group.Key,
                ApprovedOrEffectiveAbsenceCount = group.Count(),
                UniqueEmployeeCount = group.Select(request => request.UserId).Distinct().Count()
            })
            .Join(dbContext.OrgUnits.AsNoTracking(), row => row.OrgUnitId, orgUnit => orgUnit.Id, (row, orgUnit) => new
            {
                row.OrgUnitId,
                OrgUnitName = orgUnit.Name,
                row.ApprovedOrEffectiveAbsenceCount,
                row.UniqueEmployeeCount
            })
            .OrderBy(row => row.OrgUnitName)
            .ThenBy(row => row.OrgUnitId)
            .Select(row => new LeaveReportOrgUnitBreakdownDto(
                row.OrgUnitId,
                row.OrgUnitName,
                row.ApprovedOrEffectiveAbsenceCount,
                row.UniqueEmployeeCount))
            .ToListAsync(cancellationToken);

        return new(criteria.From, criteria.To, criteria.OrgUnitId, currentWorkload, period, breakdown);
    }

    private static void AddDescendants(Guid orgUnitId, IReadOnlyDictionary<Guid, List<Guid>> childrenByParent, HashSet<Guid> result)
    {
        if (!childrenByParent.TryGetValue(orgUnitId, out var children)) return;
        foreach (var child in children)
        {
            if (result.Add(child)) AddDescendants(child, childrenByParent, result);
        }
    }
}
