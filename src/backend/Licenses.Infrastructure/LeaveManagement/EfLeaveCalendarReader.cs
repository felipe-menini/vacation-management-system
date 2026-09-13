using Licenses.Application.LeaveManagement;
using Licenses.Domain.LeaveManagement;
using Licenses.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Licenses.Infrastructure.LeaveManagement;

public sealed class EfLeaveCalendarReader(ApplicationDbContext dbContext) : ILeaveCalendarReader
{
    private static readonly LeaveRequestStatus[] VisibleStatuses =
    [
        LeaveRequestStatus.Approved,
        LeaveRequestStatus.CancellationRequested,
        LeaveRequestStatus.Completed
    ];

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

    public async Task<LeaveCalendarPageDto> QueryAsync(LeaveCalendarQueryCriteria criteria, CancellationToken cancellationToken)
    {
        var orgUnitIds = criteria.OrgUnitIds.ToList();
        var query = dbContext.LeaveRequests.AsNoTracking()
            .Where(request =>
                orgUnitIds.Contains(request.OrgUnitId) &&
                VisibleStatuses.Contains(request.Status) &&
                request.StartDate <= criteria.To &&
                request.EndDate >= criteria.From)
            .Join(dbContext.Users.AsNoTracking(), request => request.UserId, user => user.Id, (request, user) => new { request, user })
            .Join(dbContext.OrgUnits.AsNoTracking(), row => row.request.OrgUnitId, orgUnit => orgUnit.Id, (row, orgUnit) => new { row.request, row.user, orgUnit });

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.request.StartDate)
            .ThenBy(x => x.user.DisplayName)
            .ThenBy(x => x.request.Id)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .Select(x => new LeaveCalendarEntryDto(
                x.request.Id,
                x.request.UserId,
                x.user.DisplayName,
                x.request.OrgUnitId,
                x.orgUnit.Name,
                x.request.StartDate,
                x.request.EndDate,
                LeaveCalendarFormatting.ToDayPortion(x.request.DayPortion),
                LeaveCalendarFormatting.ToStatus(x.request.Status)))
            .ToListAsync(cancellationToken);

        return new(criteria.Page, criteria.PageSize, totalCount, items);
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
