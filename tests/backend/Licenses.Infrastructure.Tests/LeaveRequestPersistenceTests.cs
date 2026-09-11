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
}

