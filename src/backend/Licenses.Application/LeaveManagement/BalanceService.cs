using Licenses.Application.Authorization;
using Licenses.Domain.LeaveManagement;

namespace Licenses.Application.LeaveManagement;

public sealed class BalanceService(IBalanceRepository repository, AuthorizationService authorization, ICurrentActor currentActor, TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<BalanceSnapshotDto>> GetMyBalancesAsync(CancellationToken cancellationToken)
    {
        var actorId = RequireActor();
        if (!await authorization.CanUserPerformGlobalAsync(actorId, PermissionCodes.LeaveBalancesReadSelf, cancellationToken)) throw new UnauthorizedAccessException("Actor cannot read own balances.");
        return (await repository.ListSnapshotsAsync(actorId, cancellationToken)).Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<BalanceSnapshotDto>?> GetUserBalancesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var actorId = RequireActor();
        if (!await authorization.CanAccessUserAsync(actorId, PermissionCodes.LeaveBalancesRead, userId, cancellationToken)) return null;
        return (await repository.ListSnapshotsAsync(userId, cancellationToken)).Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<BalanceLedgerEntryDto>?> GetAccountLedgerAsync(Guid userId, Guid balanceBucketId, CancellationToken cancellationToken)
    {
        var actorId = RequireActor();
        if (!await authorization.CanAccessUserAsync(actorId, PermissionCodes.LeaveBalancesRead, userId, cancellationToken)) return null;
        return (await repository.ListLedgerAsync(userId, balanceBucketId, cancellationToken))?.Select(ToDto).ToList();
    }

    public Task<BalanceMutationResultDto?> GrantAsync(BalanceMutationCommand command, CancellationToken cancellationToken) => ManageAsync(command, BalanceLedgerEntryType.Grant, cancellationToken);
    public Task<BalanceMutationResultDto?> AdjustAsync(BalanceMutationCommand command, CancellationToken cancellationToken) => ManageAsync(command, BalanceLedgerEntryType.Adjustment, cancellationToken);
    public Task<BalanceMutationResultDto?> ExpireAsync(BalanceMutationCommand command, CancellationToken cancellationToken) => ManageAsync(command, BalanceLedgerEntryType.Expire, cancellationToken);

    public Task<BalanceMutationResultDto> ReserveAsync(BalanceMutationCommand command, CancellationToken cancellationToken) => InternalMutationAsync(command, BalanceLedgerEntryType.Reserve, cancellationToken);
    public Task<BalanceMutationResultDto> ReleaseAsync(BalanceMutationCommand command, CancellationToken cancellationToken) => InternalMutationAsync(command, BalanceLedgerEntryType.Release, cancellationToken);
    public Task<BalanceMutationResultDto> ConsumeAsync(BalanceMutationCommand command, CancellationToken cancellationToken) => InternalMutationAsync(command, BalanceLedgerEntryType.Consume, cancellationToken);
    public Task<BalanceMutationResultDto> RefundAsync(BalanceMutationCommand command, CancellationToken cancellationToken) => InternalMutationAsync(command, BalanceLedgerEntryType.Refund, cancellationToken);

    private async Task<BalanceMutationResultDto?> ManageAsync(BalanceMutationCommand command, BalanceLedgerEntryType type, CancellationToken cancellationToken)
    {
        var actorId = RequireActor();
        if (!await authorization.CanAccessUserAsync(actorId, PermissionCodes.LeaveBalancesManage, command.UserId, cancellationToken)) return null;
        return ToDto(await repository.MutateAsync(command.UserId, command.BalanceBucketId, command.OperationId, type, command.Amount, command.Reason, actorId, UtcNow(), cancellationToken));
    }

    private async Task<BalanceMutationResultDto> InternalMutationAsync(BalanceMutationCommand command, BalanceLedgerEntryType type, CancellationToken cancellationToken) =>
        ToDto(await repository.MutateAsync(command.UserId, command.BalanceBucketId, command.OperationId, type, command.Amount, command.Reason, currentActor.UserId, UtcNow(), cancellationToken));

    private Guid RequireActor() => currentActor.UserId ?? throw new UnauthorizedAccessException("Actor is required.");
    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private static BalanceMutationResultDto ToDto(BalanceMutationRecord record) => new(record.LedgerEntryId, record.OperationId, ToDto(record.Snapshot), record.WasAlreadyApplied);
    private static BalanceSnapshotDto ToDto(BalanceSnapshotRecord record) => new(record.UserId, record.BalanceBucketId, record.BalanceBucketCode, record.BalanceBucketName, ToUnitCode(record.Unit), record.Available, record.Reserved);
    private static BalanceLedgerEntryDto ToDto(BalanceLedgerEntryRecord record) => new(record.Id, record.OperationId, ToTypeCode(record.Type), record.AvailableDelta, record.ReservedDelta, record.Reason, record.CreatedByUserId, record.CreatedByUserName, record.CreatedAtUtc);
    private static string ToUnitCode(BalanceBucketUnit unit) => unit switch { BalanceBucketUnit.Day => "DAY", _ => throw new ArgumentOutOfRangeException(nameof(unit)) };
    private static string ToTypeCode(BalanceLedgerEntryType type) => type switch
    {
        BalanceLedgerEntryType.Grant => "GRANT",
        BalanceLedgerEntryType.Reserve => "RESERVE",
        BalanceLedgerEntryType.Release => "RELEASE",
        BalanceLedgerEntryType.Consume => "CONSUME",
        BalanceLedgerEntryType.Refund => "REFUND",
        BalanceLedgerEntryType.Adjustment => "ADJUSTMENT",
        BalanceLedgerEntryType.Expire => "EXPIRE",
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}
