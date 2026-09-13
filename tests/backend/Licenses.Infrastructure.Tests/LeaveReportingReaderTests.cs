using Licenses.Application.LeaveManagement;
using Licenses.Domain.Identity;
using Licenses.Domain.LeaveManagement;
using Licenses.Domain.Organization;
using Licenses.Infrastructure.LeaveManagement;

namespace Licenses.Infrastructure.Tests;

public sealed class LeaveReportingReaderTests
{
    [Fact]
    public async Task QuerySummaryUsesStoredOrgScopeOverlapStatusesCurrentWorkloadUniqueEmployeesAndBreakdown()
    {
        await PostgreSqlTestDatabase.WithFreshDatabaseAsync(async db =>
        {
            var now = DateTime.UtcNow;
            var company = OrgUnit.Create("Company", "COMP" + Guid.NewGuid().ToString("N")[..6], null, now);
            var it = OrgUnit.Create("IT", "IT" + Guid.NewGuid().ToString("N")[..8], company.Id, now);
            var support = OrgUnit.Create("Support", "SUP" + Guid.NewGuid().ToString("N")[..7], it.Id, now);
            var development = OrgUnit.Create("Development", "DEV" + Guid.NewGuid().ToString("N")[..7], it.Id, now);
            var cybersecurity = OrgUnit.Create("Cybersecurity", "CYB" + Guid.NewGuid().ToString("N")[..7], it.Id, now);
            var hr = OrgUnit.Create("HR", "HR" + Guid.NewGuid().ToString("N")[..8], company.Id, now);
            var supportUser = User.Create("Support User", $"support.{Guid.NewGuid():N}@example.test", null, now);
            var developmentUser = User.Create("Development User", $"development.{Guid.NewGuid():N}@example.test", null, now);
            var cyberUser = User.Create("Cyber User", $"cyber.{Guid.NewGuid():N}@example.test", null, now);
            var hrUser = User.Create("HR User", $"hr.{Guid.NewGuid():N}@example.test", null, now);
            var type = LeaveType.Create("VAC" + Guid.NewGuid().ToString("N")[..8], "Vacation", null, 1, true, now);
            await db.OrgUnits.AddRangeAsync(company, it, support, development, cybersecurity, hr);
            await db.Users.AddRangeAsync(supportUser, developmentUser, cyberUser, hrUser);
            await db.LeaveTypes.AddAsync(type);
            await db.SaveChangesAsync();
            await db.UserOrgAssignments.AddAsync(UserOrgAssignment.Create(supportUser.Id, development.Id, true, now, null));
            var version = await SeedVersionAsync(db, type.Id, now);

            var inside = Request(supportUser.Id, support.Id, type.Id, version.Id, now, new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 10), LeaveRequestStatus.Approved);
            var startsBefore = Request(supportUser.Id, support.Id, type.Id, version.Id, now, new DateOnly(2026, 8, 30), new DateOnly(2026, 9, 2), LeaveRequestStatus.Approved);
            var endsAfter = Request(supportUser.Id, support.Id, type.Id, version.Id, now, new DateOnly(2026, 9, 29), new DateOnly(2026, 10, 2), LeaveRequestStatus.Completed);
            var cancelling = Request(supportUser.Id, support.Id, type.Id, version.Id, now, new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 12), LeaveRequestStatus.CancellationRequested);
            var pending = Request(supportUser.Id, support.Id, type.Id, version.Id, now, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 1), LeaveRequestStatus.PendingApproval);
            var cancellingOutside = Request(supportUser.Id, support.Id, type.Id, version.Id, now, new DateOnly(2026, 10, 10), new DateOnly(2026, 10, 10), LeaveRequestStatus.CancellationRequested);
            var developmentApproved = Request(developmentUser.Id, development.Id, type.Id, version.Id, now, new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 10), LeaveRequestStatus.Approved);
            var cyberApproved = Request(cyberUser.Id, cybersecurity.Id, type.Id, version.Id, now, new DateOnly(2026, 9, 11), new DateOnly(2026, 9, 11), LeaveRequestStatus.Approved);
            var hrApproved = Request(hrUser.Id, hr.Id, type.Id, version.Id, now, new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 12), LeaveRequestStatus.Approved);
            var draft = Request(supportUser.Id, support.Id, type.Id, version.Id, now, new DateOnly(2026, 9, 13), new DateOnly(2026, 9, 13), LeaveRequestStatus.Draft);
            var rejected = Request(supportUser.Id, support.Id, type.Id, version.Id, now, new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 14), LeaveRequestStatus.Rejected);
            var cancelled = Request(supportUser.Id, support.Id, type.Id, version.Id, now, new DateOnly(2026, 9, 15), new DateOnly(2026, 9, 15), LeaveRequestStatus.Cancelled);
            var revoked = Request(supportUser.Id, support.Id, type.Id, version.Id, now, new DateOnly(2026, 9, 16), new DateOnly(2026, 9, 16), LeaveRequestStatus.Revoked);
            var nonOverlapping = Request(supportUser.Id, support.Id, type.Id, version.Id, now, new DateOnly(2026, 10, 3), new DateOnly(2026, 10, 3), LeaveRequestStatus.Approved);
            await db.LeaveRequests.AddRangeAsync(inside, startsBefore, endsAfter, cancelling, pending, cancellingOutside, developmentApproved, cyberApproved, hrApproved, draft, rejected, cancelled, revoked, nonOverlapping);
            await db.SaveChangesAsync();

            var reader = new EfLeaveReportingReader(db);
            var supportResult = await reader.QuerySummaryAsync(new LeaveSummaryReportCriteria(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), support.Id, [support.Id]), CancellationToken.None);

            Assert.Equal(1, supportResult.CurrentWorkload.PendingApprovalCount);
            Assert.Equal(2, supportResult.CurrentWorkload.CancellationRequestedCount);
            Assert.Equal(2, supportResult.Period.ApprovedAbsenceCount);
            Assert.Equal(1, supportResult.Period.CompletedAbsenceCount);
            Assert.Equal(1, supportResult.Period.CancellationRequestedAbsenceCount);
            Assert.Equal(4, supportResult.Period.ApprovedOrEffectiveAbsenceCount);
            Assert.Equal(1, supportResult.Period.UniqueEmployeesWithApprovedOrCompletedAbsence);
            Assert.Single(supportResult.OrgUnitBreakdown);
            Assert.Equal(support.Id, supportResult.OrgUnitBreakdown[0].OrgUnitId);
            Assert.Equal(4, supportResult.OrgUnitBreakdown[0].ApprovedOrEffectiveAbsenceCount);
            Assert.Equal(1, supportResult.OrgUnitBreakdown[0].UniqueEmployeeCount);

            var expanded = await reader.ExpandOrgUnitScopeAsync(it.Id, CancellationToken.None);
            Assert.NotNull(expanded);
            Assert.Contains(support.Id, expanded);
            Assert.Contains(development.Id, expanded);
            Assert.Contains(cybersecurity.Id, expanded);

            var itResult = await reader.QuerySummaryAsync(new LeaveSummaryReportCriteria(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), it.Id, expanded!), CancellationToken.None);
            Assert.Equal(6, itResult.Period.ApprovedOrEffectiveAbsenceCount);
            Assert.Contains(itResult.OrgUnitBreakdown, x => x.OrgUnitId == support.Id);
            Assert.Contains(itResult.OrgUnitBreakdown, x => x.OrgUnitId == development.Id);
            Assert.Contains(itResult.OrgUnitBreakdown, x => x.OrgUnitId == cybersecurity.Id);
            Assert.DoesNotContain(itResult.OrgUnitBreakdown, x => x.OrgUnitId == hr.Id);
            Assert.DoesNotContain(supportResult.OrgUnitBreakdown, x => x.OrgUnitId == development.Id);
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

    private static LeaveRequest Request(Guid userId, Guid orgUnitId, Guid typeId, Guid versionId, DateTime now, DateOnly start, DateOnly end, LeaveRequestStatus status)
    {
        var request = LeaveRequest.CreateDraft(userId, orgUnitId, typeId, start, end, LeaveRequestDayPortion.FullDay, "Private comment", userId, now);
        if (status == LeaveRequestStatus.Draft) return request;
        request.Submit(versionId, 1m, null, null, now);
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
