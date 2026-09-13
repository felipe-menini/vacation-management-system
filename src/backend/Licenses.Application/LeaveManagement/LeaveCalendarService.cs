using Licenses.Application.Authorization;

namespace Licenses.Application.LeaveManagement;

public sealed class LeaveCalendarService(AuthorizationService authorization, ICurrentActor currentActor, ILeaveCalendarReader reader)
{
    public const int MaxQueryDays = 366;
    public const int MaxPageSize = 100;

    public async Task<LeaveCalendarPageDto> QueryAsync(LeaveCalendarQuery query, CancellationToken cancellationToken)
    {
        var actorId = currentActor.UserId ?? throw new UnauthorizedAccessException("Actor is required.");
        Validate(query);

        var allowed = await authorization.GetAuthorizedOrgUnitIdsAsync(actorId, PermissionCodes.LeaveCalendarRead, cancellationToken);
        if (allowed.Count == 0) throw new UnauthorizedAccessException("Actor cannot read the leave calendar.");

        IReadOnlyCollection<Guid> effectiveOrgUnitIds = allowed.ToList();
        if (query.OrgUnitId is { } requestedOrgUnitId)
        {
            if (!allowed.Contains(requestedOrgUnitId)) throw new UnauthorizedAccessException("Actor cannot read the requested organizational scope.");
            var requestedScope = await reader.ExpandOrgUnitScopeAsync(requestedOrgUnitId, cancellationToken)
                ?? throw new ArgumentException("Organizational unit does not exist.", nameof(query));
            effectiveOrgUnitIds = requestedScope.Where(allowed.Contains).ToList();
        }

        if (effectiveOrgUnitIds.Count == 0) return new(query.Page, query.PageSize, 0, []);
        return await reader.QueryAsync(new(query.From, query.To, effectiveOrgUnitIds, query.Page, query.PageSize), cancellationToken);
    }

    private static void Validate(LeaveCalendarQuery query)
    {
        if (query.To < query.From) throw new ArgumentException("from must be less than or equal to to.");
        if (query.To.DayNumber - query.From.DayNumber + 1 > MaxQueryDays) throw new ArgumentException($"Query range cannot exceed {MaxQueryDays} days.");
        if (query.Page < 1) throw new ArgumentException("page must be greater than or equal to 1.");
        if (query.PageSize < 1 || query.PageSize > MaxPageSize) throw new ArgumentException($"pageSize must be between 1 and {MaxPageSize}.");
    }
}
