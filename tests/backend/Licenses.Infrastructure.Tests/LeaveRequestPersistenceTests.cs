using Licenses.Domain.Identity;
using Licenses.Domain.LeaveManagement;
using Licenses.Domain.Organization;
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

    private static LeaveRequest ApprovedRequest(Guid userId, Guid unitId, Guid typeId, Guid versionId, DateTime now)
    {
        var request = LeaveRequest.CreateDraft(userId, unitId, typeId, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1), LeaveRequestDayPortion.FullDay, null, userId, now);
        request.Submit(versionId, 1m, null, null, now);
        request.Approve(now);
        return request;
    }
}

