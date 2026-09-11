using Licenses.Domain.Identity;
using Licenses.Domain.LeaveManagement;
using Licenses.Infrastructure.LeaveManagement;
using Licenses.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Licenses.Infrastructure.Tests;

public sealed class BalancePersistenceTests
{
    [Fact]
    public async Task EnforcesAccountOperationTypeAndImmutabilityConstraints()
    {
        await PostgreSqlTestDatabase.WithFreshDatabaseAsync(async db =>
        {
            var (user, bucket) = await SeedUserAndBucketAsync(db);
            var repo = new EfBalanceRepository(db, new Licenses.Infrastructure.Audit.EfAuditWriter(db));
            await repo.MutateAsync(user.Id, bucket.Id, Guid.NewGuid(), BalanceLedgerEntryType.Grant, 10m, "Initial grant", user.Id, DateTime.UtcNow, null, CancellationToken.None);
            var account = await db.BalanceAccounts.SingleAsync();
            var entry = await db.BalanceLedgerEntries.SingleAsync();

            await db.BalanceAccounts.AddAsync(BalanceAccount.Create(user.Id, bucket.Id, DateTime.UtcNow));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            db.ChangeTracker.Clear();
            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO licenses.balance_ledger_entries (id, balance_account_id, operation_id, type, available_delta, reserved_delta, reason, created_at_utc) VALUES ({Guid.NewGuid()}, {account.Id}, {Guid.NewGuid()}, {"BAD"}, {1m}, {0m}, {"Bad type"}, {DateTime.UtcNow})"));
            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO licenses.balance_ledger_entries (id, balance_account_id, operation_id, type, available_delta, reserved_delta, reason, created_at_utc) VALUES ({Guid.NewGuid()}, {account.Id}, {Guid.NewGuid()}, {"GRANT"}, {0m}, {0m}, {"Zero"}, {DateTime.UtcNow})"));
            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO licenses.balance_ledger_entries (id, balance_account_id, operation_id, type, available_delta, reserved_delta, reason, created_at_utc) VALUES ({Guid.NewGuid()}, {account.Id}, {entry.OperationId}, {"GRANT"}, {1m}, {0m}, {"Duplicate"}, {DateTime.UtcNow})"));
            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE licenses.balance_ledger_entries SET reason = {"changed"} WHERE id = {entry.Id}"));
            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM licenses.balance_ledger_entries WHERE id = {entry.Id}"));
        });
    }

    [Fact]
    public async Task ConcurrentReservationsCannotOverdrawAccount()
    {
        await PostgreSqlTestDatabase.WithFreshDatabaseAsync(async (db, connectionString) =>
        {
            var (user, bucket) = await SeedUserAndBucketAsync(db);
            var repo = new EfBalanceRepository(db, new Licenses.Infrastructure.Audit.EfAuditWriter(db));
            await repo.MutateAsync(user.Id, bucket.Id, Guid.NewGuid(), BalanceLedgerEntryType.Grant, 10m, "Initial grant", user.Id, DateTime.UtcNow, null, CancellationToken.None);

            async Task<bool> ReserveAsync()
            {
                var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connectionString).Options;
                await using var context = new ApplicationDbContext(options);
                var localRepo = new EfBalanceRepository(context, new Licenses.Infrastructure.Audit.EfAuditWriter(context));
                try
                {
                    await localRepo.MutateAsync(user.Id, bucket.Id, Guid.NewGuid(), BalanceLedgerEntryType.Reserve, 8m, "Concurrent reserve", user.Id, DateTime.UtcNow, null, CancellationToken.None);
                    return true;
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
            }

            var results = await Task.WhenAll(ReserveAsync(), ReserveAsync());
            var snapshot = (await repo.ListSnapshotsAsync(user.Id, CancellationToken.None)).Single();

            Assert.Single(results, x => x);
            Assert.Equal(2m, snapshot.Available);
            Assert.Equal(8m, snapshot.Reserved);
        });
    }

    private static async Task<(User User, BalanceBucket Bucket)> SeedUserAndBucketAsync(ApplicationDbContext db)
    {
        var user = User.Create("Balance User", $"balance.{Guid.NewGuid():N}@example.test", null, DateTime.UtcNow);
        var bucket = BalanceBucket.Create("VACATION_DAYS_" + Guid.NewGuid().ToString("N")[..8], "Vacation Days", null, BalanceBucketUnit.Day, true, DateTime.UtcNow);
        await db.Users.AddAsync(user);
        await db.BalanceBuckets.AddAsync(bucket);
        await db.SaveChangesAsync();
        return (user, bucket);
    }
}

