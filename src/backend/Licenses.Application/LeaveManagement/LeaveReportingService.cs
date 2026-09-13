using Licenses.Application.Authorization;

namespace Licenses.Application.LeaveManagement;

public sealed class LeaveReportingService(AuthorizationService authorization, ICurrentActor currentActor, ILeaveReportingReader reader)
{
    public const int MaxQueryDays = 366;

    public async Task<LeaveSummaryReportDto> GetLeaveSummaryAsync(LeaveSummaryReportQuery query, CancellationToken cancellationToken)
    {
        var actorId = currentActor.UserId ?? throw new UnauthorizedAccessException("Actor is required.");
        Validate(query);

        var allowed = await authorization.GetAuthorizedOrgUnitIdsAsync(actorId, PermissionCodes.LeaveReportsRead, cancellationToken);
        if (allowed.Count == 0) throw new UnauthorizedAccessException("Actor cannot read leave reports.");

        IReadOnlyCollection<Guid> effectiveOrgUnitIds = allowed.ToList();
        if (query.OrgUnitId is { } requestedOrgUnitId)
        {
            if (!allowed.Contains(requestedOrgUnitId)) throw new UnauthorizedAccessException("Actor cannot read the requested organizational scope.");
            var requestedScope = await reader.ExpandOrgUnitScopeAsync(requestedOrgUnitId, cancellationToken)
                ?? throw new ArgumentException("Organizational unit does not exist.", nameof(query));
            effectiveOrgUnitIds = requestedScope.Where(allowed.Contains).ToList();
        }

        if (effectiveOrgUnitIds.Count == 0)
        {
            return new(query.From, query.To, query.OrgUnitId, new(0, 0), new(0, 0, 0, 0, 0), []);
        }

        return await reader.QuerySummaryAsync(new(query.From, query.To, query.OrgUnitId, effectiveOrgUnitIds), cancellationToken);
    }

    private static void Validate(LeaveSummaryReportQuery query)
    {
        if (query.To < query.From) throw new ArgumentException("from must be less than or equal to to.");
        if (query.To.DayNumber - query.From.DayNumber + 1 > MaxQueryDays) throw new ArgumentException($"Query range cannot exceed {MaxQueryDays} days.");
    }
}
