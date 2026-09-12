using System.Net;
using System.Net.Http.Json;
using Licenses.Application.Audit;
using Licenses.Application.Authorization;
using Licenses.Application.Common;
using Licenses.Application.LeaveManagement;
using Licenses.Application.Notifications;
using Licenses.Domain.Authorization;
using Licenses.Domain.Identity;
using Licenses.Domain.LeaveManagement;
using Licenses.Domain.Organization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Licenses.Api.Tests;

public sealed class LeaveRequestMinimumNoticeEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly DateTime Now = new(2026, 9, 11, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task SelfSubmitInsufficientNoticeReturnsBusinessValidationWithoutInternals()
    {
        var setup = TestSetup.Create(minimumNoticeDays: 2, startDate: new DateOnly(2026, 9, 12));
        using var client = CreateClient(setup, setup.Employee.Id, PermissionCodes.LeaveRequestsCreateSelf);

        using var response = await client.PostAsync($"/api/leave-requests/{setup.Request.Id}/submit", null);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("INSUFFICIENT_MINIMUM_NOTICE", body);
        Assert.DoesNotContain("stack", body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(LeaveRequestStatus.Draft, setup.Request.Status);
        Assert.Empty(setup.Outbox.Messages);
        Assert.Empty(setup.Audit.Events);
    }

    [Fact]
    public async Task SelfSubmitSufficientNoticeKeepsSuccessContract()
    {
        var setup = TestSetup.Create(minimumNoticeDays: 2, startDate: new DateOnly(2026, 9, 13));
        using var client = CreateClient(setup, setup.Employee.Id, PermissionCodes.LeaveRequestsCreateSelf);

        using var response = await client.PostAsync($"/api/leave-requests/{setup.Request.Id}/submit", null);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("PENDING_APPROVAL", body);
        Assert.Single(setup.Outbox.Messages);
        Assert.Single(setup.Audit.Events);
    }

    [Fact]
    public async Task ManualCreateForOtherFollowsMinimumNoticeApiRule()
    {
        var insufficient = TestSetup.Create(minimumNoticeDays: 2, startDate: new DateOnly(2026, 9, 12));
        using var rejectedClient = CreateClient(insufficient, insufficient.Approver.Id, PermissionCodes.LeaveRequestsCreateForOthers);
        var rejectedCommand = new CreateLeaveRequestForUserCommand(insufficient.Unit.Id, insufficient.Type.Id, new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 12), "FULL_DAY", "Manual", Guid.NewGuid());

        using var rejected = await rejectedClient.PostAsJsonAsync($"/api/users/{insufficient.Employee.Id}/leave-requests", rejectedCommand);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Empty(insufficient.Outbox.Messages);

        var sufficient = TestSetup.Create(minimumNoticeDays: 2, startDate: new DateOnly(2026, 9, 13));
        using var acceptedClient = CreateClient(sufficient, sufficient.Approver.Id, PermissionCodes.LeaveRequestsCreateForOthers);
        var acceptedCommand = new CreateLeaveRequestForUserCommand(sufficient.Unit.Id, sufficient.Type.Id, new DateOnly(2026, 9, 13), new DateOnly(2026, 9, 13), "FULL_DAY", "Manual", Guid.NewGuid());

        using var accepted = await acceptedClient.PostAsJsonAsync($"/api/users/{sufficient.Employee.Id}/leave-requests", acceptedCommand);
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        Assert.Single(sufficient.Outbox.Messages);
    }

    private HttpClient CreateClient(TestSetup setup, Guid actorId, params string[] permissions) => factory.WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.AddScoped<ICurrentActor>(_ => new FixedCurrentActor(actorId));
            services.AddScoped<IAuthorizationRepository>(_ => new FakeAuthorizationRepository(actorId, setup, permissions));
            services.AddScoped<ILeaveRequestRepository>(_ => setup.Requests);
            services.AddScoped<ILeavePolicyRepository>(_ => new FakePolicyRepository(setup));
            services.AddScoped<IBalanceRepository>(_ => new FakeBalanceRepository());
            services.AddScoped<IApplicationEventOutbox>(_ => setup.Outbox);
            services.AddScoped<IAuditWriter>(_ => setup.Audit);
            services.AddScoped<IWorkingCalendarRepository>(_ => new FakeWorkingCalendarRepository());
            services.AddScoped<IMinimumNoticeCalculator>(_ => new MinimumNoticeCalculator(new BusinessDateProvider(new FixedClock(Now), TimeZoneInfo.FindSystemTimeZoneById("America/Montevideo")), new FakeWorkingCalendarRepository()));
        });
    }).CreateClient();

    private sealed record TestSetup(User Employee, User Approver, OrgUnit Unit, LeaveType Type, LeavePolicy Policy, LeavePolicyVersion Version, LeaveRequest Request, FakeLeaveRequestRepository Requests, FakeApplicationEventOutbox Outbox, FakeAuditWriter Audit)
    {
        public static TestSetup Create(int minimumNoticeDays, DateOnly startDate)
        {
            var employee = User.Create("Employee", $"employee.{Guid.NewGuid():N}@example.test", null, Now);
            var approver = User.Create("Approver", $"approver.{Guid.NewGuid():N}@example.test", null, Now);
            var unit = OrgUnit.Create("Engineering", "ENG" + Guid.NewGuid().ToString("N")[..8], null, Now);
            var type = LeaveType.Create("VAC" + Guid.NewGuid().ToString("N")[..8], "Vacation", null, 1, true, Now);
            var policy = LeavePolicy.Create(type.Id, null, false, true, Now);
            var version = LeavePolicyVersion.CreateDraft(policy.Id, 1, new DateOnly(2026, 1, 1), null, PolicyDayCountMode.CalendarDays, true, minimumNoticeDays, PolicyDayCountMode.CalendarDays, null, PolicyOverlapBehavior.Block, false, null, null, Now);
            version.Publish(Now);
            var request = LeaveRequest.CreateDraft(employee.Id, unit.Id, type.Id, startDate, startDate, LeaveRequestDayPortion.FullDay, "Draft", employee.Id, Now);
            var setup = new TestSetup(employee, approver, unit, type, policy, version, request, null!, new FakeApplicationEventOutbox(), new FakeAuditWriter());
            return setup with { Requests = new FakeLeaveRequestRepository(setup) };
        }
    }

    private sealed class FakeLeaveRequestRepository(TestSetup setup) : ILeaveRequestRepository
    {
        private readonly List<LeaveRequest> _requests = [setup.Request];
        public Task<IReadOnlyList<LeaveRequest>> ListByUserAsync(Guid userId, CancellationToken ct) => Task.FromResult<IReadOnlyList<LeaveRequest>>(_requests.Where(x => x.UserId == userId).ToList());
        public Task<IReadOnlyList<LeaveRequest>> ListByUsersAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => Task.FromResult<IReadOnlyList<LeaveRequest>>(_requests.Where(x => ids.Contains(x.UserId)).ToList());
        public Task<IReadOnlyList<LeaveRequest>> ListPendingByOrgUnitsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => Task.FromResult<IReadOnlyList<LeaveRequest>>([]);
        public Task<IReadOnlyList<LeaveRequest>> ListPendingCancellationByOrgUnitsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => Task.FromResult<IReadOnlyList<LeaveRequest>>([]);
        public Task<LeaveRequest?> GetAsync(Guid id, bool tracking, CancellationToken ct) => Task.FromResult(_requests.SingleOrDefault(x => x.Id == id));
        public Task<LeaveRequest?> GetForUpdateAsync(Guid id, CancellationToken ct) => GetAsync(id, true, ct);
        public Task<LeaveRequest?> GetBySubmissionOperationIdAsync(Guid operationId, bool tracking, CancellationToken ct) => Task.FromResult(_requests.SingleOrDefault(x => x.SubmissionOperationId == operationId));
        public Task<LeaveRequestDocument?> GetDocumentAsync(Guid id, bool tracking, CancellationToken ct) => Task.FromResult<LeaveRequestDocument?>(null);
        public Task<IReadOnlyList<LeaveRequestDocument>> ListDocumentsByRequestIdAsync(Guid requestId, CancellationToken ct) => Task.FromResult<IReadOnlyList<LeaveRequestDocument>>([]);
        public Task<IReadOnlyList<LeaveRequestDocument>> ListDocumentsByRequestIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => Task.FromResult<IReadOnlyList<LeaveRequestDocument>>([]);
        public Task AddDocumentAsync(LeaveRequestDocument document, CancellationToken ct) => Task.CompletedTask;
        public Task<LeaveRequestDecision?> GetDecisionByOperationIdAsync(Guid operationId, CancellationToken ct) => Task.FromResult<LeaveRequestDecision?>(null);
        public Task<LeaveRequestDecision?> GetDecisionByRequestIdAsync(Guid requestId, CancellationToken ct) => Task.FromResult<LeaveRequestDecision?>(null);
        public Task<IReadOnlyList<LeaveRequestDecision>> ListDecisionsByRequestIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => Task.FromResult<IReadOnlyList<LeaveRequestDecision>>([]);
        public Task<LeaveRequestCancellation?> GetCancellationByOperationIdAsync(Guid operationId, CancellationToken ct) => Task.FromResult<LeaveRequestCancellation?>(null);
        public Task<LeaveRequestCancellation?> GetCancellationByDecisionOperationIdAsync(Guid operationId, CancellationToken ct) => Task.FromResult<LeaveRequestCancellation?>(null);
        public Task<LeaveRequestCancellation?> GetCancellationByRequestIdAsync(Guid requestId, CancellationToken ct) => Task.FromResult<LeaveRequestCancellation?>(null);
        public Task<IReadOnlyList<LeaveRequestCancellation>> ListCancellationsByRequestIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => Task.FromResult<IReadOnlyList<LeaveRequestCancellation>>([]);
        public Task<LeaveRequestRevocation?> GetRevocationByOperationIdAsync(Guid operationId, CancellationToken ct) => Task.FromResult<LeaveRequestRevocation?>(null);
        public Task<LeaveRequestRevocation?> GetRevocationByRequestIdAsync(Guid requestId, CancellationToken ct) => Task.FromResult<LeaveRequestRevocation?>(null);
        public Task<IReadOnlyList<LeaveRequestRevocation>> ListRevocationsByRequestIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => Task.FromResult<IReadOnlyList<LeaveRequestRevocation>>([]);
        public Task AddAsync(LeaveRequest request, CancellationToken ct) { _requests.Add(request); return Task.CompletedTask; }
        public Task AddDecisionAsync(LeaveRequestDecision decision, CancellationToken ct) => Task.CompletedTask;
        public Task AddCancellationAsync(LeaveRequestCancellation cancellation, CancellationToken ct) => Task.CompletedTask;
        public Task AddRevocationAsync(LeaveRequestRevocation revocation, CancellationToken ct) => Task.CompletedTask;
        public Task<User?> GetUserAsync(Guid id, CancellationToken ct) => Task.FromResult<User?>(id == setup.Employee.Id ? setup.Employee : id == setup.Approver.Id ? setup.Approver : null);
        public Task<OrgUnit?> GetOrgUnitAsync(Guid id, CancellationToken ct) => Task.FromResult<OrgUnit?>(id == setup.Unit.Id ? setup.Unit : null);
        public Task<LeaveType?> GetLeaveTypeAsync(Guid id, CancellationToken ct) => Task.FromResult<LeaveType?>(id == setup.Type.Id ? setup.Type : null);
        public Task<LeavePolicy?> GetPolicyAsync(Guid id, CancellationToken ct) => Task.FromResult<LeavePolicy?>(setup.Policy);
        public Task<LeavePolicyVersion?> GetPolicyVersionAsync(Guid id, CancellationToken ct) => Task.FromResult<LeavePolicyVersion?>(id == setup.Version.Id ? setup.Version : null);
        public Task<WorkingCalendar?> GetWorkingCalendarAsync(Guid id, CancellationToken ct) => Task.FromResult<WorkingCalendar?>(null);
        public Task<Guid?> GetBalanceAccountIdAsync(Guid userId, Guid balanceBucketId, CancellationToken ct) => Task.FromResult<Guid?>(Guid.NewGuid());
        public Task<bool> HasEffectiveAssignmentAsync(Guid userId, Guid orgUnitId, DateOnly date, CancellationToken ct) => Task.FromResult(true);
        public Task<IReadOnlyList<LeaveRequest>> ListOverlappingAsync(Guid userId, DateOnly start, DateOnly end, Guid excluding, CancellationToken ct) => Task.FromResult<IReadOnlyList<LeaveRequest>>([]);
        public Task<IReadOnlyList<User>> ListUsersInOrgUnitsAsync(IReadOnlyCollection<Guid> ids, DateTime now, CancellationToken ct) => Task.FromResult<IReadOnlyList<User>>([setup.Employee]);
        public Task<IDisposable> BeginTransactionAsync(CancellationToken ct) => Task.FromResult<IDisposable>(new NoopTransaction());
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
        public Task CommitTransactionAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakePolicyRepository(TestSetup setup) : ILeavePolicyRepository
    {
        public Task<List<LeavePolicy>> ListPoliciesAsync(CancellationToken ct) => Task.FromResult(new List<LeavePolicy>());
        public Task<LeavePolicy?> GetPolicyAsync(Guid id, CancellationToken ct) => Task.FromResult<LeavePolicy?>(setup.Policy);
        public Task<LeavePolicyVersion?> GetVersionAsync(Guid id, CancellationToken ct) => Task.FromResult<LeavePolicyVersion?>(setup.Version);
        public Task<List<LeavePolicyVersion>> ListVersionsAsync(Guid policyId, CancellationToken ct) => Task.FromResult(new List<LeavePolicyVersion> { setup.Version });
        public Task<bool> ExactPolicyScopeExistsAsync(Guid leaveTypeId, Guid? orgUnitId, Guid? excludingPolicyId, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> HasPublishedVersionsAsync(Guid policyId, CancellationToken ct) => Task.FromResult(true);
        public Task<int> GetNextVersionNumberAsync(Guid policyId, CancellationToken ct) => Task.FromResult(1);
        public Task<bool> HasOverlappingPublishedVersionAsync(Guid policyId, DateOnly effectiveFrom, DateOnly? effectiveTo, Guid? excludingVersionId, CancellationToken ct) => Task.FromResult(false);
        public Task<List<LeavePolicy>> ListPoliciesForLeaveTypeWithPublishedVersionsAsync(Guid leaveTypeId, DateOnly date, CancellationToken ct) { setup.Policy.Versions.Clear(); setup.Policy.Versions.Add(setup.Version); return Task.FromResult(new List<LeavePolicy> { setup.Policy }); }
        public Task AddPolicyAsync(LeavePolicy policy, CancellationToken ct) => Task.CompletedTask;
        public Task AddVersionAsync(LeavePolicyVersion version, CancellationToken ct) => Task.CompletedTask;
        public Task<LeaveType?> GetLeaveTypeAsync(Guid id, CancellationToken ct) => Task.FromResult<LeaveType?>(setup.Type);
        public Task<BalanceBucket?> GetBalanceBucketAsync(Guid id, CancellationToken ct) => Task.FromResult<BalanceBucket?>(null);
        public Task<WorkingCalendar?> GetWorkingCalendarAsync(Guid id, CancellationToken ct) => Task.FromResult<WorkingCalendar?>(null);
        public Task<OrgUnit?> GetOrgUnitAsync(Guid id, CancellationToken ct) => Task.FromResult<OrgUnit?>(setup.Unit);
        public Task<List<OrgUnit>> ListOrgUnitsAsync(CancellationToken ct) => Task.FromResult(new List<OrgUnit> { setup.Unit });
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeAuthorizationRepository(Guid actorId, TestSetup setup, string[] permissions) : IAuthorizationRepository
    {
        private readonly Guid _roleId = Guid.NewGuid();
        public Task<User?> GetUserAsync(Guid id, CancellationToken ct) => Task.FromResult<User?>(id == setup.Employee.Id ? setup.Employee : id == setup.Approver.Id ? setup.Approver : null);
        public Task<OrgUnit?> GetOrgUnitAsync(Guid id, CancellationToken ct) => Task.FromResult<OrgUnit?>(setup.Unit);
        public Task<List<OrgUnit>> ListOrgUnitsAsync(CancellationToken ct) => Task.FromResult(new List<OrgUnit> { setup.Unit });
        public Task<List<UserOrgAssignment>> ListActiveUserOrgAssignmentsAsync(Guid id, DateTime now, CancellationToken ct) => Task.FromResult(new List<UserOrgAssignment> { UserOrgAssignment.Create(id, setup.Unit.Id, true, Now.AddDays(-1), null) });
        public Task<List<RoleScopeAssignment>> ListActiveRoleScopeAssignmentsAsync(Guid id, DateTime now, CancellationToken ct) => Task.FromResult(id == actorId ? new List<RoleScopeAssignment> { RoleScopeAssignment.Create(id, _roleId, setup.Unit.Id, false, Now.AddDays(-1), null) } : []);
        public Task<bool> RoleHasPermissionAsync(Guid roleId, string code, CancellationToken ct) => Task.FromResult(roleId == _roleId && permissions.Contains(code));
        public Task<bool> IsRoleActiveAsync(Guid roleId, CancellationToken ct) => Task.FromResult(roleId == _roleId);
        public Task<List<User>> ListUsersInOrgUnitsAsync(IReadOnlyCollection<Guid> ids, DateTime now, CancellationToken ct) => Task.FromResult(new List<User> { setup.Employee, setup.Approver });
        public Task<List<DevelopmentActorDto>> ListDevelopmentActorsAsync(DateTime now, CancellationToken ct) => Task.FromResult(new List<DevelopmentActorDto>());
    }

    private sealed class FakeBalanceRepository : IBalanceRepository
    {
        public Task<User?> GetUserAsync(Guid id, CancellationToken ct) => Task.FromResult<User?>(null);
        public Task<IReadOnlyList<BalanceSnapshotRecord>> ListSnapshotsAsync(Guid id, CancellationToken ct) => Task.FromResult<IReadOnlyList<BalanceSnapshotRecord>>([]);
        public Task<IReadOnlyList<BalanceLedgerEntryRecord>?> ListLedgerAsync(Guid id, Guid b, CancellationToken ct) => Task.FromResult<IReadOnlyList<BalanceLedgerEntryRecord>?>([]);
        public Task<BalanceMutationRecord> MutateAsync(Guid userId, Guid b, Guid op, BalanceLedgerEntryType type, decimal amount, string reason, Guid? by, DateTime at, BalanceMutationAuditContext? auditContext, CancellationToken ct) => Task.FromResult(new BalanceMutationRecord(Guid.NewGuid(), op, new BalanceSnapshotRecord(userId, b, "VAC", "Vacation", BalanceBucketUnit.Day, 0m, 0m), false));
    }

    private sealed class FakeApplicationEventOutbox : IApplicationEventOutbox { public List<(IApplicationEvent Event, Guid CorrelationId)> Messages { get; } = []; public Task EnqueueAsync(IApplicationEvent applicationEvent, Guid correlationId, CancellationToken cancellationToken) { Messages.Add((applicationEvent, correlationId)); return Task.CompletedTask; } }
    private sealed class FakeAuditWriter : IAuditWriter { public List<AuditEventData> Events { get; } = []; public Task WriteAsync(AuditEventData auditEvent, CancellationToken cancellationToken) { Events.Add(auditEvent); return Task.CompletedTask; } }
    private sealed class FakeWorkingCalendarRepository : IWorkingCalendarRepository { public Task<List<WorkingCalendar>> ListCalendarsAsync(CancellationToken ct) => Task.FromResult(new List<WorkingCalendar>()); public Task<WorkingCalendar?> GetCalendarAsync(Guid id, CancellationToken ct) => Task.FromResult<WorkingCalendar?>(null); public Task<WorkingCalendarException?> GetExceptionAsync(Guid id, CancellationToken ct) => Task.FromResult<WorkingCalendarException?>(null); public Task<bool> CodeExistsAsync(string normalizedCode, Guid? excludingCalendarId, CancellationToken ct) => Task.FromResult(false); public Task<bool> ExceptionDateExistsAsync(Guid calendarId, DateOnly date, Guid? excludingExceptionId, CancellationToken ct) => Task.FromResult(false); public Task AddCalendarAsync(WorkingCalendar calendar, CancellationToken ct) => Task.CompletedTask; public Task AddExceptionAsync(WorkingCalendarException exception, CancellationToken ct) => Task.CompletedTask; public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask; }
    private sealed class FixedCurrentActor(Guid userId) : ICurrentActor { public Guid? UserId => userId; }
    private sealed class FixedClock(DateTime utcNow) : IClock { public DateTimeOffset UtcNow { get; } = new(utcNow); }
    private sealed class NoopTransaction : IDisposable { public void Dispose() { } }
}
