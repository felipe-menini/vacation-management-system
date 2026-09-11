using Licenses.Application.Authorization;
using Licenses.Domain.LeaveManagement;

namespace Licenses.Application.LeaveManagement;

public sealed class LeaveRequestService(ILeaveRequestRepository repository, ILeavePolicyRepository policyRepository, BalanceService balanceService, AuthorizationService authorization, ICurrentActor currentActor, TimeProvider timeProvider)
{
    private readonly DayCalculator _calculator = new();

    public async Task<IReadOnlyList<LeaveRequestDto>> ListMyRequestsAsync(CancellationToken cancellationToken)
    {
        var actorId = RequireActor();
        if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeaveRequestsReadSelf, cancellationToken)) throw new UnauthorizedAccessException("Actor cannot read own leave requests.");
        return await ToDtosAsync(await repository.ListByUserAsync(actorId, cancellationToken), cancellationToken);
    }

    public async Task<IReadOnlyList<LeaveRequestDto>?> ListUserRequestsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var actorId = RequireActor();
        if (actorId == userId)
        {
            if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeaveRequestsReadSelf, cancellationToken)) return null;
        }
        else if (!await authorization.CanAccessUserAsync(actorId, PermissionCodes.LeaveRequestsRead, userId, cancellationToken)) return null;
        return await ToDtosAsync(await repository.ListByUserAsync(userId, cancellationToken), cancellationToken);
    }

    public async Task<LeaveRequestDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var actorId = RequireActor();
        var request = await repository.GetAsync(id, tracking: false, cancellationToken);
        if (request is null) return null;
        if (request.UserId == actorId)
        {
            if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeaveRequestsReadSelf, cancellationToken)) return null;
        }
        else if (!await authorization.CanUserPerformAsync(actorId, PermissionCodes.LeaveRequestsRead, request.OrgUnitId, cancellationToken)) return null;
        return (await ToDtosAsync([request], cancellationToken)).Single();
    }

    public async Task<IReadOnlyList<LeaveRequestDto>> ListScopedAsync(CancellationToken cancellationToken)
    {
        var actorId = RequireActor();
        var allowed = await authorization.GetAuthorizedOrgUnitIdsAsync(actorId, PermissionCodes.LeaveRequestsRead, cancellationToken);
        if (allowed.Count == 0) return [];
        var users = await repository.ListUsersInOrgUnitsAsync(allowed.ToList(), UtcNow(), cancellationToken);
        var requests = await repository.ListByUsersAsync(users.Select(x => x.Id).ToList(), cancellationToken);
        return await ToDtosAsync(requests.Where(x => allowed.Contains(x.OrgUnitId)).ToList(), cancellationToken);
    }

    public async Task<LeaveRequestDto> CreateDraftAsync(CreateLeaveRequestCommand command, CancellationToken cancellationToken)
    {
        var actorId = RequireActor();
        if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeaveRequestsCreateSelf, cancellationToken)) throw new UnauthorizedAccessException("Actor cannot create own leave requests.");
        await ValidateDraftBasicsAsync(actorId, command.OrgUnitId, command.LeaveTypeId, command.StartDate, command.EndDate, cancellationToken);
        var request = LeaveRequest.CreateDraft(actorId, command.OrgUnitId, command.LeaveTypeId, command.StartDate, command.EndDate, ParseDayPortion(command.DayPortion), command.Comment, actorId, UtcNow());
        await repository.AddAsync(request, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return (await ToDtosAsync([request], cancellationToken)).Single();
    }

    public async Task<LeaveRequestDto?> UpdateDraftAsync(Guid id, UpdateLeaveRequestCommand command, CancellationToken cancellationToken)
    {
        var actorId = RequireActor();
        var request = await repository.GetAsync(id, tracking: true, cancellationToken);
        if (request is null || request.UserId != actorId) return null;
        if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeaveRequestsCreateSelf, cancellationToken)) throw new UnauthorizedAccessException("Actor cannot edit own leave requests.");
        await ValidateDraftBasicsAsync(actorId, command.OrgUnitId, command.LeaveTypeId, command.StartDate, command.EndDate, cancellationToken);
        request.UpdateDraft(command.OrgUnitId, command.LeaveTypeId, command.StartDate, command.EndDate, ParseDayPortion(command.DayPortion), command.Comment, UtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return (await ToDtosAsync([request], cancellationToken)).Single();
    }

    public async Task<SubmitLeaveRequestResultDto?> SubmitAsync(Guid id, CancellationToken cancellationToken)
    {
        var actorId = RequireActor();
        if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeaveRequestsCreateSelf, cancellationToken)) throw new UnauthorizedAccessException("Actor cannot submit own leave requests.");
        using var _ = await repository.BeginTransactionAsync(cancellationToken);
        var request = await repository.GetForUpdateAsync(id, cancellationToken);
        if (request is null || request.UserId != actorId) return null;
        if (request.Status == LeaveRequestStatus.PendingApproval)
        {
            await repository.CommitTransactionAsync(cancellationToken);
            return new((await ToDtosAsync([request], cancellationToken)).Single(), [], WasAlreadySubmitted: true);
        }
        if (request.Status != LeaveRequestStatus.Draft) throw new InvalidOperationException("Only DRAFT leave requests can be submitted.");

        await ValidateDraftBasicsAsync(request.UserId, request.OrgUnitId, request.LeaveTypeId, request.StartDate, request.EndDate, cancellationToken);
        var resolved = await new LeavePolicyService(policyRepository, timeProvider).ResolvePolicyAsync(request.LeaveTypeId, request.OrgUnitId, request.StartDate, cancellationToken);
        if (!resolved.Found || resolved.Version is null) throw new InvalidOperationException(resolved.Reason ?? "No published applicable policy exists.");
        var version = await repository.GetPolicyVersionAsync(resolved.Version.Id, cancellationToken) ?? throw new InvalidOperationException("Resolved policy version does not exist.");

        var calculatedDays = await CalculateAsync(request, version, cancellationToken);
        if (version.MaximumRequestDays is not null && calculatedDays > version.MaximumRequestDays.Value) throw new InvalidOperationException("Requested days exceed the policy maximum.");

        var overlaps = await repository.ListOverlappingAsync(request.UserId, request.StartDate, request.EndDate, request.Id, cancellationToken);
        var activeOverlaps = overlaps.Where(x => LeaveRequest.IsActiveOverlapStatus(x.Status)).ToList();
        var warnings = new List<string>();
        if (activeOverlaps.Count > 0)
        {
            if (version.OverlapBehavior == PolicyOverlapBehavior.Block) throw new InvalidOperationException("An active overlapping leave request already exists.");
            if (version.OverlapBehavior == PolicyOverlapBehavior.Warn) warnings.Add("An active overlapping leave request exists.");
        }

        Guid? balanceAccountId = null;
        Guid? operationId = null;
        if (version.ConsumesBalance)
        {
            if (version.BalanceBucketId is not { } bucketId) throw new InvalidOperationException("Resolved consuming policy has no balance bucket.");
            operationId = request.EnsureReservationOperationId();
            await repository.SaveChangesAsync(cancellationToken);
            var reservation = await balanceService.ReserveAsync(new BalanceMutationCommand(request.UserId, bucketId, operationId.Value, calculatedDays, $"Reserve leave request {request.Id}"), cancellationToken);
            balanceAccountId = await FindBalanceAccountIdAsync(request.UserId, bucketId, cancellationToken);
            if (balanceAccountId is null) throw new InvalidOperationException("Balance reservation account was not persisted.");
        }

        request.Submit(version.Id, calculatedDays, balanceAccountId, operationId, UtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        await repository.CommitTransactionAsync(cancellationToken);
        return new((await ToDtosAsync([request], cancellationToken)).Single(), warnings, WasAlreadySubmitted: false);
    }

    private Task<Guid?> FindBalanceAccountIdAsync(Guid userId, Guid bucketId, CancellationToken cancellationToken) =>
        repository.GetBalanceAccountIdAsync(userId, bucketId, cancellationToken);

    private async Task<decimal> CalculateAsync(LeaveRequest request, LeavePolicyVersion version, CancellationToken cancellationToken)
    {
        if (request.DayPortion == LeaveRequestDayPortion.HalfDay)
        {
            if (!version.AllowHalfDay) throw new InvalidOperationException("Policy does not allow HALF_DAY requests.");
            if (version.DayCountMode == PolicyDayCountMode.BusinessDays)
            {
                var calendar = await repository.GetWorkingCalendarAsync(version.WorkingCalendarId!.Value, cancellationToken) ?? throw new InvalidOperationException("Working calendar does not exist.");
                if (!calendar.IsWorkingDay(request.StartDate)) throw new InvalidOperationException("HALF_DAY business-day requests require a working day.");
            }
            return 0.5m;
        }

        WorkingCalendar? workingCalendar = null;
        if (version.DayCountMode == PolicyDayCountMode.BusinessDays)
            workingCalendar = await repository.GetWorkingCalendarAsync(version.WorkingCalendarId!.Value, cancellationToken) ?? throw new InvalidOperationException("Working calendar does not exist.");
        return _calculator.Calculate(request.StartDate, request.EndDate, version.DayCountMode, workingCalendar).CalculatedDays;
    }

    private async Task ValidateDraftBasicsAsync(Guid userId, Guid orgUnitId, Guid leaveTypeId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken)
    {
        if (endDate < startDate) throw new ArgumentException("EndDate must be greater than or equal to StartDate.");
        var leaveType = await repository.GetLeaveTypeAsync(leaveTypeId, cancellationToken) ?? throw new InvalidOperationException("Leave type does not exist.");
        if (!leaveType.IsActive) throw new InvalidOperationException("Inactive leave types cannot be used for new requests.");
        if (await repository.GetOrgUnitAsync(orgUnitId, cancellationToken) is null) throw new InvalidOperationException("Organizational unit does not exist.");
        if (!await repository.HasEffectiveAssignmentAsync(userId, orgUnitId, startDate, cancellationToken)) throw new InvalidOperationException("Employee does not have an effective assignment to the selected organizational unit for the request start date.");
    }

    private async Task<List<LeaveRequestDto>> ToDtosAsync(IEnumerable<LeaveRequest> requests, CancellationToken cancellationToken)
    {
        var list = requests.ToList();
        var result = new List<LeaveRequestDto>();
        foreach (var request in list)
        {
            var user = await repository.GetUserAsync(request.UserId, cancellationToken);
            var unit = await repository.GetOrgUnitAsync(request.OrgUnitId, cancellationToken);
            var type = await repository.GetLeaveTypeAsync(request.LeaveTypeId, cancellationToken);
            result.Add(new(request.Id, request.UserId, user?.DisplayName, request.OrgUnitId, unit?.Code, unit?.Name, request.LeaveTypeId, type?.Code, type?.Name, request.LeavePolicyVersionId, request.StartDate, request.EndDate, ToDayPortion(request.DayPortion), request.CalculatedDays, ToStatus(request.Status), request.Comment, request.BalanceAccountId, request.BalanceReservationOperationId, request.CreatedByUserId, request.CreatedAtUtc, request.UpdatedAtUtc, request.SubmittedAtUtc));
        }
        return result;
    }

    private Guid RequireActor() => currentActor.UserId ?? throw new UnauthorizedAccessException("Actor is required.");
    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;
    private static DateTime StartDateUtc(DateOnly date) => date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
    public static LeaveRequestDayPortion ParseDayPortion(string value) => value?.Trim().ToUpperInvariant() switch { "FULL_DAY" => LeaveRequestDayPortion.FullDay, "HALF_DAY" => LeaveRequestDayPortion.HalfDay, _ => throw new ArgumentException("Day portion is invalid.") };
    private static string ToDayPortion(LeaveRequestDayPortion value) => value switch { LeaveRequestDayPortion.FullDay => "FULL_DAY", LeaveRequestDayPortion.HalfDay => "HALF_DAY", _ => throw new ArgumentOutOfRangeException(nameof(value)) };
    private static string ToStatus(LeaveRequestStatus value) => value switch { LeaveRequestStatus.Draft => "DRAFT", LeaveRequestStatus.PendingApproval => "PENDING_APPROVAL", LeaveRequestStatus.Approved => "APPROVED", LeaveRequestStatus.Rejected => "REJECTED", LeaveRequestStatus.CancellationRequested => "CANCELLATION_REQUESTED", LeaveRequestStatus.Cancelled => "CANCELLED", LeaveRequestStatus.Revoked => "REVOKED", LeaveRequestStatus.Completed => "COMPLETED", _ => throw new ArgumentOutOfRangeException(nameof(value)) };
}


