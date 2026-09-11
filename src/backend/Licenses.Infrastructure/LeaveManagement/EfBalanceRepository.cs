using System.Data;
using Licenses.Application.LeaveManagement;
using Licenses.Domain.Identity;
using Licenses.Domain.LeaveManagement;
using Licenses.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Licenses.Infrastructure.LeaveManagement;

public sealed class EfBalanceRepository(ApplicationDbContext dbContext) : IBalanceRepository
{
    public Task<User?> GetUserAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

    public async Task<IReadOnlyList<BalanceSnapshotRecord>> ListSnapshotsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var rows = await dbContext.BalanceAccounts.AsNoTracking()
            .Where(account => account.UserId == userId)
            .Join(dbContext.BalanceBuckets.AsNoTracking(), account => account.BalanceBucketId, bucket => bucket.Id, (account, bucket) => new { account, bucket })
            .Select(x => new BalanceSnapshotRecord(
                x.account.UserId,
                x.bucket.Id,
                x.bucket.Code,
                x.bucket.Name,
                x.bucket.Unit,
                dbContext.BalanceLedgerEntries.Where(e => e.BalanceAccountId == x.account.Id).Sum(e => (decimal?)e.AvailableDelta) ?? 0m,
                dbContext.BalanceLedgerEntries.Where(e => e.BalanceAccountId == x.account.Id).Sum(e => (decimal?)e.ReservedDelta) ?? 0m))
            .ToListAsync(cancellationToken);
        return rows.OrderBy(x => x.BalanceBucketCode).ToList();
    }

    public async Task<IReadOnlyList<BalanceLedgerEntryRecord>?> ListLedgerAsync(Guid userId, Guid balanceBucketId, CancellationToken cancellationToken)
    {
        var account = await dbContext.BalanceAccounts.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId && x.BalanceBucketId == balanceBucketId, cancellationToken);
        if (account is null) return null;

        return await dbContext.BalanceLedgerEntries.AsNoTracking()
            .Where(x => x.BalanceAccountId == account.Id)
            .GroupJoin(dbContext.Users.AsNoTracking(), entry => entry.CreatedByUserId, user => user.Id, (entry, users) => new { entry, users })
            .SelectMany(x => x.users.DefaultIfEmpty(), (x, user) => new BalanceLedgerEntryRecord(x.entry.Id, x.entry.OperationId, x.entry.Type, x.entry.AvailableDelta, x.entry.ReservedDelta, x.entry.Reason, x.entry.CreatedByUserId, user == null ? null : user.DisplayName, x.entry.CreatedAtUtc))
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<BalanceMutationRecord> MutateAsync(Guid userId, Guid balanceBucketId, Guid operationId, BalanceLedgerEntryType type, decimal amount, string reason, Guid? createdByUserId, DateTime createdAtUtc, CancellationToken cancellationToken)
    {
        var (availableDelta, reservedDelta) = BalanceLedgerEntry.GetDeltas(type, amount);
        reason = NormalizeReason(reason);

        var ownsTransaction = dbContext.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken) : null;

        var existing = await FindExistingOperationAsync(operationId, cancellationToken);
        if (existing is not null)
        {
            EnsureIdempotentMatch(existing.Value, userId, balanceBucketId, type, availableDelta, reservedDelta, reason);
            if (ownsTransaction) await transaction!.CommitAsync(cancellationToken);
            return new BalanceMutationRecord(existing.Value.Entry.Id, operationId, await BuildSnapshotAsync(existing.Value.Account, cancellationToken), WasAlreadyApplied: true);
        }

        var account = await GetOrCreateAccountAsync(userId, balanceBucketId, createdAtUtc, type, cancellationToken);
        await LockAccountAsync(account.Id, cancellationToken);

        var currentAvailable = await dbContext.BalanceLedgerEntries.Where(x => x.BalanceAccountId == account.Id).SumAsync(x => (decimal?)x.AvailableDelta, cancellationToken) ?? 0m;
        var currentReserved = await dbContext.BalanceLedgerEntries.Where(x => x.BalanceAccountId == account.Id).SumAsync(x => (decimal?)x.ReservedDelta, cancellationToken) ?? 0m;
        var nextAvailable = currentAvailable + availableDelta;
        var nextReserved = currentReserved + reservedDelta;
        if (nextAvailable < 0m) throw new InvalidOperationException("Available balance cannot become negative.");
        if (nextReserved < 0m) throw new InvalidOperationException("Reserved balance cannot become negative.");

        var entry = BalanceLedgerEntry.Create(account.Id, operationId, type, availableDelta, reservedDelta, reason, createdByUserId, createdAtUtc);
        await dbContext.BalanceLedgerEntries.AddAsync(entry, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        if (ownsTransaction) await transaction!.CommitAsync(cancellationToken);

        return new BalanceMutationRecord(entry.Id, operationId, await BuildSnapshotAsync(account, cancellationToken), WasAlreadyApplied: false);
    }

    private async Task<(BalanceLedgerEntry Entry, BalanceAccount Account)?> FindExistingOperationAsync(Guid operationId, CancellationToken cancellationToken)
    {
        var existing = await dbContext.BalanceLedgerEntries.AsNoTracking().FirstOrDefaultAsync(x => x.OperationId == operationId, cancellationToken);
        if (existing is null) return null;
        var account = await dbContext.BalanceAccounts.AsNoTracking().SingleAsync(x => x.Id == existing.BalanceAccountId, cancellationToken);
        return (existing, account);
    }

    private static void EnsureIdempotentMatch((BalanceLedgerEntry Entry, BalanceAccount Account) existing, Guid userId, Guid balanceBucketId, BalanceLedgerEntryType type, decimal availableDelta, decimal reservedDelta, string reason)
    {
        if (existing.Account.UserId != userId || existing.Account.BalanceBucketId != balanceBucketId || existing.Entry.Type != type || existing.Entry.AvailableDelta != availableDelta || existing.Entry.ReservedDelta != reservedDelta || existing.Entry.Reason != reason)
            throw new InvalidOperationException("OperationId was already used for a different balance mutation.");
    }

    private async Task<BalanceAccount> GetOrCreateAccountAsync(Guid userId, Guid balanceBucketId, DateTime createdAtUtc, BalanceLedgerEntryType type, CancellationToken cancellationToken)
    {
        var account = await dbContext.BalanceAccounts.FirstOrDefaultAsync(x => x.UserId == userId && x.BalanceBucketId == balanceBucketId, cancellationToken);
        var bucket = await dbContext.BalanceBuckets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == balanceBucketId, cancellationToken) ?? throw new InvalidOperationException("Balance bucket does not exist.");
        if (type is BalanceLedgerEntryType.Grant or BalanceLedgerEntryType.Reserve && !bucket.IsActive) throw new InvalidOperationException("Inactive balance buckets cannot receive new grant or reserve operations.");
        if (account is not null) return account;
        var user = await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, cancellationToken) ?? throw new InvalidOperationException("User does not exist.");
        if (!user.IsActive) throw new InvalidOperationException("Inactive users cannot receive new balance accounts.");
        if (!bucket.IsActive) throw new InvalidOperationException("Inactive balance buckets cannot receive new balance accounts.");

        account = BalanceAccount.Create(userId, balanceBucketId, createdAtUtc);
        await dbContext.BalanceAccounts.AddAsync(account, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return account;
    }

    private Task LockAccountAsync(Guid accountId, CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM licenses.balance_accounts WHERE id = {accountId} FOR UPDATE", cancellationToken);

    private async Task<BalanceSnapshotRecord> BuildSnapshotAsync(BalanceAccount account, CancellationToken cancellationToken)
    {
        var bucket = await dbContext.BalanceBuckets.AsNoTracking().SingleAsync(x => x.Id == account.BalanceBucketId, cancellationToken);
        var available = await dbContext.BalanceLedgerEntries.Where(x => x.BalanceAccountId == account.Id).SumAsync(x => (decimal?)x.AvailableDelta, cancellationToken) ?? 0m;
        var reserved = await dbContext.BalanceLedgerEntries.Where(x => x.BalanceAccountId == account.Id).SumAsync(x => (decimal?)x.ReservedDelta, cancellationToken) ?? 0m;
        return new BalanceSnapshotRecord(account.UserId, bucket.Id, bucket.Code, bucket.Name, bucket.Unit, available, reserved);
    }

    private static string NormalizeReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Reason is required.", nameof(reason));
        var normalized = reason.Trim();
        if (normalized.Length > BalanceLedgerEntry.ReasonMaxLength) throw new ArgumentException($"Reason cannot exceed {BalanceLedgerEntry.ReasonMaxLength} characters.", nameof(reason));
        return normalized;
    }
}