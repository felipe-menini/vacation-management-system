using Licenses.Domain.Identity;
using Licenses.Domain.LeaveManagement;
using Licenses.Domain.Organization;
using Licenses.Application.Audit;
using Licenses.Application.Common;
using Licenses.Application.LeaveManagement;
using Licenses.Infrastructure.Audit;
using Licenses.Infrastructure.LeaveManagement;
using Licenses.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Licenses.Infrastructure.Tests;

public sealed class LeaveRequestPersistenceTests
{
    [Fact]
    public async Task PersistsRequestAndEnforcesBasicConstraints()
    {
        await PostgreSqlTestDatabase.WithFreshDatabaseAsync(async db =>
        {
            var now = DateTime.UtcNow;
            var user = User.Create("Request User", $"request.{Guid.NewGuid():N}@example.test", null, now);
            var unit = OrgUnit.Create("Engineering", "ENG" + Guid.NewGuid().ToString("N")[..8], null, now);
            var type = LeaveType.Create("VAC" + Guid.NewGuid().ToString("N")[..8], "Vacation", null, 1, true, now);
            await db.Users.AddAsync(user);
            await db.OrgUnits.AddAsync(unit);
            await db.LeaveTypes.AddAsync(type);
            await db.SaveChangesAsync();

            var request = LeaveRequest.CreateDraft(user.Id, unit.Id, type.Id, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 2), LeaveRequestDayPortion.FullDay, null, user.Id, now);
            await db.LeaveRequests.AddAsync(request);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var persisted = await db.LeaveRequests.SingleAsync();
            Assert.Equal(LeaveRequestStatus.Draft, persisted.Status);
            Assert.Null(persisted.LeavePolicyVersionId);
            Assert.Null(persisted.CalculatedDays);

            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO licenses.leave_requests (id, user_id, org_unit_id, leave_type_id, start_date, end_date, day_portion, status, created_by_user_id, created_at_utc, updated_at_utc) VALUES ({Guid.NewGuid()}, {user.Id}, {unit.Id}, {type.Id}, {new DateOnly(2026, 9, 3)}, {new DateOnly(2026, 9, 2)}, {"FULL_DAY"}, {"DRAFT"}, {user.Id}, {now}, {now})"));
            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO licenses.leave_requests (id, user_id, org_unit_id, leave_type_id, start_date, end_date, day_portion, status, created_by_user_id, created_at_utc, updated_at_utc) VALUES ({Guid.NewGuid()}, {user.Id}, {unit.Id}, {type.Id}, {new DateOnly(2026, 9, 1)}, {new DateOnly(2026, 9, 2)}, {"HALF_DAY"}, {"DRAFT"}, {user.Id}, {now}, {now})"));
            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO licenses.leave_requests (id, user_id, org_unit_id, leave_type_id, start_date, end_date, day_portion, status, created_by_user_id, created_at_utc, updated_at_utc) VALUES ({Guid.NewGuid()}, {user.Id}, {unit.Id}, {type.Id}, {new DateOnly(2026, 9, 1)}, {new DateOnly(2026, 9, 1)}, {"BAD"}, {"DRAFT"}, {user.Id}, {now}, {now})"));
        });
    }

    [Fact]
    public async Task DecisionConstraintsAndImmutabilityAreEnforced()
    {
        await PostgreSqlTestDatabase.WithFreshDatabaseAsync(async db =>
        {
            var now = DateTime.UtcNow;
            var user = User.Create("Request User", $"decision.user.{Guid.NewGuid():N}@example.test", null, now);
            var approver = User.Create("Approver", $"decision.approver.{Guid.NewGuid():N}@example.test", null, now);
            var unit = OrgUnit.Create("Engineering", "ENG" + Guid.NewGuid().ToString("N")[..8], null, now);
            var type = LeaveType.Create("VAC" + Guid.NewGuid().ToString("N")[..8], "Vacation", null, 1, true, now);
            await db.Users.AddRangeAsync(user, approver);
            await db.OrgUnits.AddAsync(unit);
            await db.LeaveTypes.AddAsync(type);
            await db.SaveChangesAsync();

            var policy = LeavePolicy.Create(type.Id, null, false, true, now);
            var version = LeavePolicyVersion.CreateDraft(policy.Id, 1, new DateOnly(2026, 1, 1), null, PolicyDayCountMode.CalendarDays, true, null, PolicyDayCountMode.CalendarDays, null, PolicyOverlapBehavior.Block, consumesBalance: false, balanceBucketId: null, workingCalendarId: null, now);
            version.Publish(now);
            await db.LeavePolicies.AddAsync(policy);
            await db.LeavePolicyVersions.AddAsync(version);
            await db.SaveChangesAsync();

            var request = LeaveRequest.CreateDraft(user.Id, unit.Id, type.Id, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1), LeaveRequestDayPortion.FullDay, null, user.Id, now);
            request.Submit(version.Id, 1m, null, null, now);
            await db.LeaveRequests.AddAsync(request);
            await db.SaveChangesAsync();

            var operationId = Guid.NewGuid();
            var decision = LeaveRequestDecision.Create(request.Id, LeaveRequestDecisionKind.Approve, approver.Id, null, operationId, null, now);
            await db.LeaveRequestDecisions.AddAsync(decision);
            await db.SaveChangesAsync();

            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO licenses.leave_request_decisions (id, leave_request_id, decision, decided_by_user_id, operation_id, created_at_utc) VALUES ({Guid.NewGuid()}, {request.Id}, {"APPROVE"}, {approver.Id}, {Guid.NewGuid()}, {now})"));
            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO licenses.leave_request_decisions (id, leave_request_id, decision, decided_by_user_id, operation_id, created_at_utc) VALUES ({Guid.NewGuid()}, {Guid.NewGuid()}, {"APPROVE"}, {approver.Id}, {Guid.NewGuid()}, {now})"));
            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO licenses.leave_request_decisions (id, leave_request_id, decision, decided_by_user_id, operation_id, created_at_utc) VALUES ({Guid.NewGuid()}, {request.Id}, {"BAD"}, {approver.Id}, {Guid.NewGuid()}, {now})"));
            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO licenses.leave_request_decisions (id, leave_request_id, decision, decided_by_user_id, operation_id, created_at_utc) VALUES ({Guid.NewGuid()}, {request.Id}, {"REJECT"}, {approver.Id}, {Guid.NewGuid()}, {now})"));
            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE licenses.leave_request_decisions SET comment = {"Changed"} WHERE id = {decision.Id}"));
            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM licenses.leave_request_decisions WHERE id = {decision.Id}"));
        });
    }

    [Fact]
    public async Task CancellationAndRevocationHistoryConstraintsAndImmutabilityAreEnforced()
    {
        await PostgreSqlTestDatabase.WithFreshDatabaseAsync(async db =>
        {
            var now = DateTime.UtcNow;
            var user = User.Create("Request User", $"lifecycle.user.{Guid.NewGuid():N}@example.test", null, now);
            var approver = User.Create("Approver", $"lifecycle.approver.{Guid.NewGuid():N}@example.test", null, now);
            var unit = OrgUnit.Create("Engineering", "ENG" + Guid.NewGuid().ToString("N")[..8], null, now);
            var type = LeaveType.Create("VAC" + Guid.NewGuid().ToString("N")[..8], "Vacation", null, 1, true, now);
            await db.Users.AddRangeAsync(user, approver);
            await db.OrgUnits.AddAsync(unit);
            await db.LeaveTypes.AddAsync(type);
            await db.SaveChangesAsync();

            var policy = LeavePolicy.Create(type.Id, null, false, true, now);
            var version = LeavePolicyVersion.CreateDraft(policy.Id, 1, new DateOnly(2026, 1, 1), null, PolicyDayCountMode.CalendarDays, true, null, PolicyDayCountMode.CalendarDays, null, PolicyOverlapBehavior.Block, consumesBalance: false, balanceBucketId: null, workingCalendarId: null, now);
            version.Publish(now);
            await db.LeavePolicies.AddAsync(policy);
            await db.LeavePolicyVersions.AddAsync(version);
            await db.SaveChangesAsync();

            var cancelledRequest = ApprovedRequest(user.Id, unit.Id, type.Id, version.Id, now);
            var revokedRequest = ApprovedRequest(user.Id, unit.Id, type.Id, version.Id, now.AddMinutes(1));
            await db.LeaveRequests.AddRangeAsync(cancelledRequest, revokedRequest);
            await db.SaveChangesAsync();

            var cancellation = LeaveRequestCancellation.Create(cancelledRequest.Id, user.Id, "Plans changed", Guid.NewGuid(), now);
            cancelledRequest.RequestCancellation(now);
            await db.LeaveRequestCancellations.AddAsync(cancellation);
            await db.SaveChangesAsync();

            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO licenses.leave_request_cancellations (id, leave_request_id, requested_by_user_id, reason, operation_id, requested_at_utc) VALUES ({Guid.NewGuid()}, {cancelledRequest.Id}, {user.Id}, {"Duplicate"}, {Guid.NewGuid()}, {now})"));
            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE licenses.leave_request_cancellations SET reason = {"Changed"} WHERE id = {cancellation.Id}"));
            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE licenses.leave_request_cancellations SET decision = {"REJECT"} WHERE id = {cancellation.Id}"));

            cancellation.Decide(LeaveRequestCancellationDecision.Approve, approver.Id, null, Guid.NewGuid(), null, now);
            cancelledRequest.ApproveCancellation(now);
            await db.SaveChangesAsync();

            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE licenses.leave_request_cancellations SET decision_comment = {"Changed"} WHERE id = {cancellation.Id}"));
            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM licenses.leave_request_cancellations WHERE id = {cancellation.Id}"));

            var revocation = LeaveRequestRevocation.Create(revokedRequest.Id, approver.Id, "Operational need", Guid.NewGuid(), null, now);
            revokedRequest.Revoke(now);
            await db.LeaveRequestRevocations.AddAsync(revocation);
            await db.SaveChangesAsync();

            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO licenses.leave_request_revocations (id, leave_request_id, revoked_by_user_id, reason, operation_id, created_at_utc) VALUES ({Guid.NewGuid()}, {revokedRequest.Id}, {approver.Id}, {"Duplicate"}, {Guid.NewGuid()}, {now})"));
            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE licenses.leave_request_revocations SET reason = {"Changed"} WHERE id = {revocation.Id}"));
            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM licenses.leave_request_revocations WHERE id = {revocation.Id}"));
        });
    }

    [Fact]
    public async Task EligibleCompletionQueryReturnsOnlyApprovedPastEndDateInDeterministicBatches()
    {
        await PostgreSqlTestDatabase.WithFreshDatabaseAsync(async db =>
        {
            var now = DateTime.UtcNow;
            var (user, unit, type, version) = await SeedRequestDependenciesAsync(db, now);
            var later = ApprovedRequest(user.Id, unit.Id, type.Id, version.Id, now, new DateOnly(2026, 9, 13));
            var first = ApprovedRequest(user.Id, unit.Id, type.Id, version.Id, now.AddMinutes(1), new DateOnly(2026, 9, 12));
            var second = ApprovedRequest(user.Id, unit.Id, type.Id, version.Id, now.AddMinutes(2), new DateOnly(2026, 9, 12));
            var sameDay = ApprovedRequest(user.Id, unit.Id, type.Id, version.Id, now.AddMinutes(3), new DateOnly(2026, 9, 14));
            var cancellationRequested = ApprovedRequest(user.Id, unit.Id, type.Id, version.Id, now.AddMinutes(4), new DateOnly(2026, 9, 12));
            cancellationRequested.RequestCancellation(now);
            await db.LeaveRequests.AddRangeAsync(later, second, first, sameDay, cancellationRequested);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var repository = new EfLeaveRequestRepository(db);
            var result = await repository.ListEligibleApprovedForCompletionAsync(new DateOnly(2026, 9, 14), 2, CancellationToken.None);

            Assert.Equal(2, result.Count);
            Assert.All(result, request => Assert.Equal(LeaveRequestStatus.Approved, request.Status));
            Assert.Equal([new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 12)], result.Select(x => x.EndDate));
            Assert.True(result[0].Id.CompareTo(result[1].Id) < 0);
        });
    }

    [Fact]
    public async Task CompletionServicePersistsCompletionWithoutBalanceLedgerMutation()
    {
        await PostgreSqlTestDatabase.WithFreshDatabaseAsync(async db =>
        {
            var now = new DateTime(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);
            var (user, unit, type, version) = await SeedRequestDependenciesAsync(db, now);
            var request = ApprovedRequest(user.Id, unit.Id, type.Id, version.Id, now, new DateOnly(2026, 9, 15));
            await db.LeaveRequests.AddAsync(request);
            await db.SaveChangesAsync();

            var service = new LeaveRequestCompletionService(
                new EfLeaveRequestRepository(db),
                new FixedBusinessDateProvider(new DateOnly(2026, 9, 16)),
                new FixedClock(now),
                new EfAuditWriter(db));
            var result = await service.CompleteEligibleAsync(cancellationToken: CancellationToken.None);
            db.ChangeTracker.Clear();

            var persisted = await db.LeaveRequests.SingleAsync(x => x.Id == request.Id);
            Assert.Equal(1, result.CompletedCount);
            Assert.Equal(LeaveRequestStatus.Completed, persisted.Status);
            Assert.Equal(now, persisted.CompletedAtUtc);
            Assert.Equal(version.Id, persisted.LeavePolicyVersionId);
            Assert.Equal(1m, persisted.CalculatedDays);
            Assert.Empty(await db.BalanceLedgerEntries.ToListAsync());
        });
    }

    [Fact]
    public async Task CompletionRollbackDoesNotLeavePartialState()
    {
        await PostgreSqlTestDatabase.WithFreshDatabaseAsync(async db =>
        {
            var now = new DateTime(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);
            var (user, unit, type, version) = await SeedRequestDependenciesAsync(db, now);
            var request = ApprovedRequest(user.Id, unit.Id, type.Id, version.Id, now, new DateOnly(2026, 9, 15));
            await db.LeaveRequests.AddAsync(request);
            await db.SaveChangesAsync();

            var repository = new EfLeaveRequestRepository(db);
            await using (await db.Database.BeginTransactionAsync())
            {
                var locked = await repository.GetForUpdateAsync(request.Id, CancellationToken.None);
                Assert.True(locked!.Complete(new DateOnly(2026, 9, 16), now));
                await repository.SaveChangesAsync(CancellationToken.None);
            }

            db.ChangeTracker.Clear();
            var persisted = await db.LeaveRequests.SingleAsync(x => x.Id == request.Id);
            Assert.Equal(LeaveRequestStatus.Approved, persisted.Status);
            Assert.Null(persisted.CompletedAtUtc);
        });
    }

    [Fact]
    public async Task CompletionServicePersistsSystemAuditAtomically()
    {
        await PostgreSqlTestDatabase.WithFreshDatabaseAsync(async db =>
        {
            var now = new DateTime(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);
            var (user, unit, type, version) = await SeedRequestDependenciesAsync(db, now);
            var request = ApprovedRequest(user.Id, unit.Id, type.Id, version.Id, now, new DateOnly(2026, 9, 15));
            await db.LeaveRequests.AddAsync(request);
            await db.SaveChangesAsync();

            var service = new LeaveRequestCompletionService(
                new EfLeaveRequestRepository(db),
                new FixedBusinessDateProvider(new DateOnly(2026, 9, 16)),
                new FixedClock(now),
                new EfAuditWriter(db));

            await service.CompleteEligibleAsync(cancellationToken: CancellationToken.None);
            await service.CompleteEligibleAsync(cancellationToken: CancellationToken.None);
            db.ChangeTracker.Clear();

            var auditEvent = await db.AuditEvents.SingleAsync(x => x.Action == "leave.request.complete");
            Assert.Null(auditEvent.ActorUserId);
            Assert.Equal(request.Id, auditEvent.ResourceId);
            Assert.Equal(user.Id, auditEvent.SubjectUserId);
            Assert.Equal(unit.Id, auditEvent.OrgUnitId);
            Assert.Equal(now, auditEvent.OccurredAtUtc);
            Assert.Contains("previousStatus", auditEvent.MetadataJson);
            Assert.DoesNotContain("document", auditEvent.MetadataJson!, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task AuditFailureRollsBackCompletion()
    {
        await PostgreSqlTestDatabase.WithFreshDatabaseAsync(async db =>
        {
            var now = new DateTime(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);
            var (user, unit, type, version) = await SeedRequestDependenciesAsync(db, now);
            var request = ApprovedRequest(user.Id, unit.Id, type.Id, version.Id, now, new DateOnly(2026, 9, 15));
            await db.LeaveRequests.AddAsync(request);
            await db.SaveChangesAsync();

            var service = new LeaveRequestCompletionService(
                new EfLeaveRequestRepository(db),
                new FixedBusinessDateProvider(new DateOnly(2026, 9, 16)),
                new FixedClock(now),
                new ThrowingAuditWriter());

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CompleteEligibleAsync(cancellationToken: CancellationToken.None));
            db.ChangeTracker.Clear();

            var persisted = await db.LeaveRequests.SingleAsync(x => x.Id == request.Id);
            Assert.Equal(LeaveRequestStatus.Approved, persisted.Status);
            Assert.Null(persisted.CompletedAtUtc);
            Assert.Empty(await db.AuditEvents.Where(x => x.Action == "leave.request.complete").ToListAsync());
        });
    }

    [Fact]
    public async Task ConcurrentCompletionProcessorsDoNotDuplicateCompletionAudit()
    {
        await PostgreSqlTestDatabase.WithFreshDatabaseAsync(async (db, connectionString) =>
        {
            var now = new DateTime(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);
            var (user, unit, type, version) = await SeedRequestDependenciesAsync(db, now);
            var request = ApprovedRequest(user.Id, unit.Id, type.Id, version.Id, now, new DateOnly(2026, 9, 15));
            await db.LeaveRequests.AddAsync(request);
            await db.SaveChangesAsync();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connectionString).Options;
            await using var db1 = new ApplicationDbContext(options);
            await using var db2 = new ApplicationDbContext(options);
            var service1 = new LeaveRequestCompletionService(new EfLeaveRequestRepository(db1), new FixedBusinessDateProvider(new DateOnly(2026, 9, 16)), new FixedClock(now), new EfAuditWriter(db1));
            var service2 = new LeaveRequestCompletionService(new EfLeaveRequestRepository(db2), new FixedBusinessDateProvider(new DateOnly(2026, 9, 16)), new FixedClock(now), new EfAuditWriter(db2));

            await Task.WhenAll(
                service1.CompleteEligibleAsync(cancellationToken: CancellationToken.None),
                service2.CompleteEligibleAsync(cancellationToken: CancellationToken.None));

            db.ChangeTracker.Clear();
            Assert.Equal(LeaveRequestStatus.Completed, (await db.LeaveRequests.SingleAsync(x => x.Id == request.Id)).Status);
            Assert.Equal(1, await db.AuditEvents.CountAsync(x => x.Action == "leave.request.complete" && x.ResourceId == request.Id));
        });
    }

    private static LeaveRequest ApprovedRequest(Guid userId, Guid unitId, Guid typeId, Guid versionId, DateTime now, DateOnly? endDate = null)
    {
        var effectiveEndDate = endDate ?? new DateOnly(2026, 9, 1);
        var request = LeaveRequest.CreateDraft(userId, unitId, typeId, effectiveEndDate, effectiveEndDate, LeaveRequestDayPortion.FullDay, null, userId, now);
        request.Submit(versionId, 1m, null, null, now);
        request.Approve(now);
        return request;
    }

    private static async Task<(User User, OrgUnit Unit, LeaveType Type, LeavePolicyVersion Version)> SeedRequestDependenciesAsync(ApplicationDbContext db, DateTime now)
    {
        var user = User.Create("Request User", $"completion.user.{Guid.NewGuid():N}@example.test", null, now);
        var unit = OrgUnit.Create("Engineering", "ENG" + Guid.NewGuid().ToString("N")[..8], null, now);
        var type = LeaveType.Create("VAC" + Guid.NewGuid().ToString("N")[..8], "Vacation", null, 1, true, now);
        await db.Users.AddAsync(user);
        await db.OrgUnits.AddAsync(unit);
        await db.LeaveTypes.AddAsync(type);
        await db.SaveChangesAsync();

        var policy = LeavePolicy.Create(type.Id, null, false, true, now);
        var version = LeavePolicyVersion.CreateDraft(policy.Id, 1, new DateOnly(2026, 1, 1), null, PolicyDayCountMode.CalendarDays, true, null, PolicyDayCountMode.CalendarDays, null, PolicyOverlapBehavior.Block, consumesBalance: false, balanceBucketId: null, workingCalendarId: null, now);
        version.Publish(now);
        await db.LeavePolicies.AddAsync(policy);
        await db.LeavePolicyVersions.AddAsync(version);
        await db.SaveChangesAsync();
        return (user, unit, type, version);
    }

    private sealed class FixedBusinessDateProvider(DateOnly today) : IBusinessDateProvider { public DateOnly Today { get; } = today; }
    private sealed class FixedClock(DateTimeOffset utcNow) : IClock { public DateTimeOffset UtcNow { get; } = utcNow; }
    private sealed class ThrowingAuditWriter : IAuditWriter
    {
        public Task WriteAsync(AuditEventData auditEvent, CancellationToken cancellationToken) => throw new InvalidOperationException("Audit failed.");
    }
}