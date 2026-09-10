using Licenses.Domain.LeaveManagement;
using Licenses.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Licenses.Infrastructure.Tests;

public sealed class LeavePolicyPersistenceTests
{
    [Fact]
    public async Task EnforcesPolicyScopeUniquenessAgainstPostgreSql()
    {
        await PostgreSqlTestDatabase.WithFreshDatabaseAsync(async db =>
        {
            var now = DateTime.UtcNow;
            var leaveType = LeaveType.Create("VACATION", "Vacation", null, 10, true, now);
            var orgUnit = OrgUnit.Create("IT", "IT", null, now);
            db.LeaveTypes.Add(leaveType);
            db.OrgUnits.Add(orgUnit);
            await db.SaveChangesAsync();

            await InsertPolicyAsync(db, leaveType.Id, null, false);
            await Assert.ThrowsAsync<PostgresException>(() => InsertPolicyAsync(db, leaveType.Id, null, false));

            await InsertPolicyAsync(db, leaveType.Id, orgUnit.Id, true);
            await Assert.ThrowsAsync<PostgresException>(() => InsertPolicyAsync(db, leaveType.Id, orgUnit.Id, false));
        });
    }

    [Fact]
    public async Task EnforcesVersionNumberAndRuleCheckConstraintsAgainstPostgreSql()
    {
        await PostgreSqlTestDatabase.WithFreshDatabaseAsync(async db =>
        {
            var (_, _, policy) = await SeedPolicyAsync(db);

            await InsertVersionAsync(db, policy.Id, 1, "DRAFT", new DateOnly(2026, 1, 1), null, false, null, null);

            await Assert.ThrowsAsync<PostgresException>(() => InsertVersionAsync(db, policy.Id, 1, "DRAFT", new DateOnly(2027, 1, 1), null, false, null, null));
            await Assert.ThrowsAsync<PostgresException>(() => InsertVersionAsync(db, policy.Id, 2, "DRAFT", new DateOnly(2026, 2, 1), new DateOnly(2026, 1, 1), false, null, null));
            await Assert.ThrowsAsync<PostgresException>(() => InsertVersionAsync(db, policy.Id, 3, "ACTIVE", new DateOnly(2026, 1, 1), null, false, null, null));
            await Assert.ThrowsAsync<PostgresException>(() => InsertVersionAsync(db, policy.Id, 4, "DRAFT", new DateOnly(2026, 1, 1), null, false, null, null, dayCountMode: "ARBITRARY"));
        });
    }

    [Fact]
    public async Task BlocksPublishedPeriodOverlapButAllowsDraftOverlapAgainstPostgreSql()
    {
        await PostgreSqlTestDatabase.WithFreshDatabaseAsync(async db =>
        {
            var (_, _, policy) = await SeedPolicyAsync(db);

            await InsertVersionAsync(db, policy.Id, 1, "PUBLISHED", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), false, null, DateTime.UtcNow);
            await InsertVersionAsync(db, policy.Id, 2, "DRAFT", new DateOnly(2026, 6, 1), null, false, null, null);

            await Assert.ThrowsAsync<PostgresException>(() => InsertVersionAsync(db, policy.Id, 3, "PUBLISHED", new DateOnly(2026, 6, 1), null, false, null, DateTime.UtcNow));
        });
    }

    [Fact]
    public async Task EnforcesConsumesBalanceConsistencyAgainstPostgreSql()
    {
        await PostgreSqlTestDatabase.WithFreshDatabaseAsync(async db =>
        {
            var (_, bucket, policy) = await SeedPolicyAsync(db);

            await InsertVersionAsync(db, policy.Id, 1, "DRAFT", new DateOnly(2026, 1, 1), null, true, bucket.Id, null);

            await Assert.ThrowsAsync<PostgresException>(() => InsertVersionAsync(db, policy.Id, 2, "DRAFT", new DateOnly(2027, 1, 1), null, true, null, null));
            await Assert.ThrowsAsync<PostgresException>(() => InsertVersionAsync(db, policy.Id, 3, "DRAFT", new DateOnly(2028, 1, 1), null, false, bucket.Id, null));
        });
    }

    [Fact]
    public async Task UsesRestrictDeletesForHistoricalPolicyRecordsAgainstPostgreSql()
    {
        await PostgreSqlTestDatabase.WithFreshDatabaseAsync(async db =>
        {
            var (leaveType, bucket, policy) = await SeedPolicyAsync(db);
            await InsertVersionAsync(db, policy.Id, 1, "PUBLISHED", new DateOnly(2026, 1, 1), null, true, bucket.Id, DateTime.UtcNow);

            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM licenses.balance_buckets WHERE id = {bucket.Id};"));
            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM licenses.leave_policies WHERE id = {policy.Id};"));
            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM licenses.leave_types WHERE id = {leaveType.Id};"));
        });
    }

    private static async Task<(LeaveType LeaveType, BalanceBucket Bucket, LeavePolicy Policy)> SeedPolicyAsync(DbContext db)
    {
        var now = DateTime.UtcNow;
        var leaveType = LeaveType.Create("VACATION", "Vacation", null, 10, true, now);
        var bucket = BalanceBucket.Create("VACATION_DAYS", "Vacation Days", null, BalanceBucketUnit.Day, true, now);
        db.Set<LeaveType>().Add(leaveType);
        db.Set<BalanceBucket>().Add(bucket);
        await db.SaveChangesAsync();

        var policy = LeavePolicy.Create(leaveType.Id, null, false, true, now);
        db.Set<LeavePolicy>().Add(policy);
        await db.SaveChangesAsync();

        return (leaveType, bucket, policy);
    }

    private static Task InsertPolicyAsync(DbContext db, Guid leaveTypeId, Guid? orgUnitId, bool appliesToDescendants) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO licenses.leave_policies
                (id, leave_type_id, org_unit_id, applies_to_descendants, is_active, created_at_utc, updated_at_utc)
            VALUES
                ({Guid.NewGuid()}, {leaveTypeId}, {orgUnitId}, {appliesToDescendants}, true, {DateTime.UtcNow}, {DateTime.UtcNow});
            """);

    private static Task InsertVersionAsync(
        DbContext db,
        Guid policyId,
        int versionNumber,
        string status,
        DateOnly effectiveFrom,
        DateOnly? effectiveTo,
        bool consumesBalance,
        Guid? balanceBucketId,
        DateTime? publishedAtUtc,
        string dayCountMode = "BUSINESS_DAYS") =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO licenses.leave_policy_versions
                (id, leave_policy_id, version_number, status, effective_from, effective_to, day_count_mode, allow_half_day, minimum_notice_days, notice_day_count_mode, maximum_request_days, overlap_behavior, consumes_balance, balance_bucket_id, created_at_utc, updated_at_utc, published_at_utc)
            VALUES
                ({Guid.NewGuid()}, {policyId}, {versionNumber}, {status}, {effectiveFrom}, {effectiveTo}, {dayCountMode}, true, 7, 'CALENDAR_DAYS', 15, 'BLOCK', {consumesBalance}, {balanceBucketId}, {DateTime.UtcNow}, {DateTime.UtcNow}, {publishedAtUtc});
            """);
}
