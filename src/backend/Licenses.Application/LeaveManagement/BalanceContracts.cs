using Licenses.Domain.Identity;
using Licenses.Domain.LeaveManagement;

namespace Licenses.Application.LeaveManagement;

public sealed record BalanceSnapshotDto(Guid UserId, Guid BalanceBucketId, string BalanceBucketCode, string BalanceBucketName, string Unit, decimal Available, decimal Reserved);
public sealed record BalanceLedgerEntryDto(Guid Id, Guid OperationId, string Type, decimal AvailableDelta, decimal ReservedDelta, string Reason, Guid? CreatedByUserId, string? CreatedByUserName, DateTime CreatedAtUtc);
public sealed record BalanceMutationCommand(Guid UserId, Guid BalanceBucketId, Guid OperationId, decimal Amount, string Reason);
public sealed record BalanceMutationResultDto(Guid LedgerEntryId, Guid OperationId, BalanceSnapshotDto Snapshot, bool WasAlreadyApplied);

public interface IBalanceRepository
{
    Task<User?> GetUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<BalanceSnapshotRecord>> ListSnapshotsAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<BalanceLedgerEntryRecord>?> ListLedgerAsync(Guid userId, Guid balanceBucketId, CancellationToken cancellationToken);
    Task<BalanceMutationRecord> MutateAsync(Guid userId, Guid balanceBucketId, Guid operationId, BalanceLedgerEntryType type, decimal amount, string reason, Guid? createdByUserId, DateTime createdAtUtc, CancellationToken cancellationToken);
}

public sealed record BalanceSnapshotRecord(Guid UserId, Guid BalanceBucketId, string BalanceBucketCode, string BalanceBucketName, BalanceBucketUnit Unit, decimal Available, decimal Reserved);
public sealed record BalanceLedgerEntryRecord(Guid Id, Guid OperationId, BalanceLedgerEntryType Type, decimal AvailableDelta, decimal ReservedDelta, string Reason, Guid? CreatedByUserId, string? CreatedByUserName, DateTime CreatedAtUtc);
public sealed record BalanceMutationRecord(Guid LedgerEntryId, Guid OperationId, BalanceSnapshotRecord Snapshot, bool WasAlreadyApplied);
