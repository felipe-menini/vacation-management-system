using Licenses.Application.LeaveManagement;
using Licenses.Domain.Identity;
using Licenses.Domain.LeaveManagement;
using Licenses.Domain.Organization;
using Licenses.Infrastructure.LeaveManagement;
using Microsoft.EntityFrameworkCore;

namespace Licenses.Infrastructure.Tests;

public sealed class LeaveCalendarReaderTests
{
    [Fact]
    public async Task QueryUsesStoredOrgUnitScopeDateOverlapVisibleStatusesAndDeterministicPaging()
    {
        await PostgreSqlTestDatabase.WithFreshDatabaseAsync(async db =>
        {
            var now = DateTime.UtcNow;
            var company = OrgUnit.Create("Company", "COMP" + Guid.NewGuid().ToString("N")[..6], null, now);
            var it = OrgUnit.Create("IT", "IT" + Guid.NewGuid().ToString("N")[..8], company.Id, now);
            var support = OrgUnit.Create("Support", "SUP" + Guid.NewGuid().ToString("N")[..7], it.Id, now);
            var development = OrgUnit.Create("Development", "DEV" + Guid.NewGuid().ToString("N")[..7], it.Id, now);
            var hr = OrgUnit.Create("HR", "HR" + Guid.NewGuid().ToString("N")[..8], company.Id, now);
            var supportUser = User.Create("Support User", $"support.{Guid.NewGuid():N}@example.test", null, now);
            var developmentUser = User.Create("Development User", $"development.{Guid.NewGuid():N}@example.test", null, now);
            var type = LeaveType.Create("VAC" + Guid.NewGuid().ToString("N")[..8], "Vacation", null, 1, true, now);
            await db.OrgUnits.AddRangeAsync(company, it, support, development, hr);
            await db.Users.AddRangeAsync(supportUser, developmentUser);
            await db.LeaveTypes.AddAsync(type);
            await db.SaveChangesAsync();
            await db.UserOrgAssignments.AddAsync(UserOrgAssignment.Create(supportUser.Id, development.Id, true, now, null));
            var version = await SeedVersionAsync(db, type.Id, now);

            var inside = Request(supportUser.Id, support.Id, type.Id, version.Id, now, new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 10), LeaveRequestStatus.Approved);
            var startsBefore = Request(supportUser.Id, support.Id, type.Id, version.Id, now, new DateOnly(2026, 8, 30), new DateOnly(2026, 9, 2), LeaveRequestStatus.Approved);
            var endsAfter = Request(supportUser.Id, support.Id, type.Id, version.Id, now, new DateOnly(2026, 9, 29), new DateOnly(2026, 10, 2), LeaveRequestStatus.Completed);
            var cancelling = Request(supportUser.Id, support.Id, type.Id, version.Id, now, new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 12), LeaveRequestStatus.CancellationRequested);
            var halfDay = Request(supportUser.Id, support.Id, type.Id, version.Id, now, new DateOnly(2026, 9, 13), new DateOnly(2026, 9, 13), LeaveRequestStatus.Approved, LeaveRequestDayPortion.HalfDay);
            var otherScope = Request(developmentUser.Id, development.Id, type.Id, version.Id, now, new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 10), LeaveRequestStatus.Approved);
            var draft = Request(supportUser.Id, support.Id, type.Id, version.Id, now, new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 14), LeaveRequestStatus.Draft);
            var pending = Request(supportUser.Id, support.Id, type.Id, version.Id, now, new DateOnly(2026, 9, 15), new DateOnly(2026, 9, 15), LeaveRequestStatus.PendingApproval);
            var rejected = Request(supportUser.Id, support.Id, type.Id, version.Id, now, new DateOnly(2026, 9, 16), new DateOnly(2026, 9, 16), LeaveRequestStatus.Rejected);
            var cancelled = Request(supportUser.Id, support.Id, type.Id, version.Id, now, new DateOnly(2026, 9, 17), new DateOnly(2026, 9, 17), LeaveRequestStatus.Cancelled);
            var revoked = Request(supportUser.Id, support.Id, type.Id, version.Id, now, new DateOnly(2026, 9, 18), new DateOnly(2026, 9, 18), LeaveRequestStatus.Revoked);
            var nonOverlapping = Request(supportUser.Id, support.Id, type.Id, version.Id, now, new DateOnly(2026, 10, 3), new DateOnly(2026, 10, 3), LeaveRequestStatus.Approved);
            await db.LeaveRequests.AddRangeAsync(inside, startsBefore, endsAfter, cancelling, halfDay, otherScope, draft, pending, rejected, cancelled, revoked, nonOverlapping);
            await db.SaveChangesAsync();

            var reader = new EfLeaveCalendarReader(db);
            var result = await reader.QueryAsync(new LeaveCalendarQueryCriteria(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), [support.Id], 1, 20), CancellationToken.None);

            Assert.Equal(5, result.TotalCount);
            Assert.Equal([startsBefore.Id, inside.Id, cancelling.Id, halfDay.Id, endsAfter.Id], result.Items.Select(x => x.LeaveRequestId));
            Assert.DoesNotContain(result.Items, x => x.LeaveRequestId == otherScope.Id);
            Assert.Contains(result.Items, x => x.LeaveRequestId == halfDay.Id && x.DayPortion == "HALF_DAY");
            Assert.Contains(result.Items, x => x.LeaveRequestId == cancelling.Id && x.Status == "CANCELLATION_REQUESTED");
            Assert.DoesNotContain(result.Items, x => x.LeaveRequestId == draft.Id || x.LeaveRequestId == pending.Id || x.LeaveRequestId == rejected.Id || x.LeaveRequestId == cancelled.Id || x.LeaveRequestId == revoked.Id || x.LeaveRequestId == nonOverlapping.Id);
            Assert.Equal(support.Id, result.Items.Single(x => x.LeaveRequestId == inside.Id).OrgUnitId);

            var page = await reader.QueryAsync(new LeaveCalendarQueryCriteria(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), [support.Id], 2, 2), CancellationToken.None);
            Assert.Equal(5, page.TotalCount);
            Assert.Equal([cancelling.Id, halfDay.Id], page.Items.Select(x => x.LeaveRequestId));
        });
    }

    [Fact]
    public async Task ExpandsOrgUnitDescendants()
    {
        await PostgreSqlTestDatabase.WithFreshDatabaseAsync(async db =>
        {
            var now = DateTime.UtcNow;
            var it = OrgUnit.Create("IT", "IT" + Guid.NewGuid().ToString("N")[..8], null, now);
            var support = OrgUnit.Create("Support", "SUP" + Guid.NewGuid().ToString("N")[..7], it.Id, now);
            await db.OrgUnits.AddRangeAsync(it, support);
            await db.SaveChangesAsync();

            var expanded = await new EfLeaveCalendarReader(db).ExpandOrgUnitScopeAsync(it.Id, CancellationToken.None);

            Assert.NotNull(expanded);
            Assert.Contains(it.Id, expanded);
            Assert.Contains(support.Id, expanded);
        });
    }

    private static async Task<LeavePolicyVersion> SeedVersionAsync(Licenses.Infrastructure.Persistence.ApplicationDbContext db, Guid leaveTypeId, DateTime now)
    {
        var policy = LeavePolicy.Create(leaveTypeId, null, false, true, now);
        var version = LeavePolicyVersion.CreateDraft(policy.Id, 1, new DateOnly(2026, 1, 1), null, PolicyDayCountMode.CalendarDays, true, null, PolicyDayCountMode.CalendarDays, null, PolicyOverlapBehavior.Allow, false, null, null, now);
        version.Publish(now);
        await db.LeavePolicies.AddAsync(policy);
        await db.LeavePolicyVersions.AddAsync(version);
        await db.SaveChangesAsync();
        return version;
    }

    private static LeaveRequest Request(Guid userId, Guid orgUnitId, Guid typeId, Guid versionId, DateTime now, DateOnly start, DateOnly end, LeaveRequestStatus status, LeaveRequestDayPortion dayPortion = LeaveRequestDayPortion.FullDay)
    {
        var request = LeaveRequest.CreateDraft(userId, orgUnitId, typeId, start, end, dayPortion, "Private comment", userId, now);
        if (status == LeaveRequestStatus.Draft) return request;
        request.Submit(versionId, dayPortion == LeaveRequestDayPortion.HalfDay ? 0.5m : 1m, null, null, now);
        if (status == LeaveRequestStatus.PendingApproval) return request;
        if (status == LeaveRequestStatus.Approved) { request.Approve(now); return request; }
        if (status == LeaveRequestStatus.Rejected) { request.Reject(now); return request; }
        request.Approve(now);
        if (status == LeaveRequestStatus.CancellationRequested) { request.RequestCancellation(now); return request; }
        if (status == LeaveRequestStatus.Cancelled) { request.RequestCancellation(now); request.ApproveCancellation(now); return request; }
        if (status == LeaveRequestStatus.Revoked) { request.Revoke(now); return request; }
        if (status == LeaveRequestStatus.Completed) { request.Complete(end.AddDays(1), now); return request; }
        throw new ArgumentOutOfRangeException(nameof(status));
    }
}
