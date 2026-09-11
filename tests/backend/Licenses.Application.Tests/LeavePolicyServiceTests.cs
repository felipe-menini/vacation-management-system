using Licenses.Application.Audit;
using Licenses.Application.Authorization;
using Licenses.Application.LeaveManagement;
using Licenses.Domain.LeaveManagement;
using Licenses.Domain.Organization;

namespace Licenses.Application.Tests;

public sealed class LeavePolicyServiceTests
{
    private readonly FakeLeavePolicyRepository _repository = new();
    private readonly FakeAuditWriter _audit = new();
    private readonly Guid _actorId = Guid.NewGuid();
    private static readonly Guid TestWorkingCalendarId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private readonly LeavePolicyService _service;

    public LeavePolicyServiceTests() => _service = new LeavePolicyService(_repository, TimeProvider.System, new FixedCurrentActor(_actorId), _audit);

    [Fact]
    public async Task CreatesCompanyPolicyAndRejectsDuplicateExactScope()
    {
        var leaveType = _repository.AddLeaveType("VACATION", true);
        var created = await _service.CreatePolicyAsync(new(leaveType.Id, null, true, null), CancellationToken.None);

        Assert.False(created.AppliesToDescendants);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreatePolicyAsync(new(leaveType.Id, null, false, null), CancellationToken.None));
        Assert.Contains("already exists", ex.Message);
    }

    [Fact]
    public async Task AdministrativePolicyMutationsProduceFocusedAuditEvents()
    {
        var leaveType = _repository.AddLeaveType("VACATION", true);
        var bucket = _repository.AddBucket("VACATION_DAYS", true);

        var policy = await _service.CreatePolicyAsync(new(leaveType.Id, null, false, null), CancellationToken.None);
        await _service.UpdatePolicyAsync(policy.Id, new(null, false, true), CancellationToken.None);
        var version = await _service.CreateVersionAsync(policy.Id, Command(consumesBalance: true, balanceBucketId: bucket.Id), CancellationToken.None);
        await _service.UpdateVersionAsync(version.Id, new(version.EffectiveFrom, null, "BUSINESS_DAYS", true, 7, "CALENDAR_DAYS", 15m, "BLOCK", true, bucket.Id, TestWorkingCalendarId), CancellationToken.None);
        await _service.PublishVersionAsync(version.Id, CancellationToken.None);

        Assert.Equal([
            "leave.policy.create",
            "leave.policy.update",
            "leave.policy.version.create",
            "leave.policy.version.update",
            "leave.policy.version.publish"
        ], _audit.Events.Select(x => x.Action));
        Assert.All(_audit.Events, x => Assert.Equal(_actorId, x.ActorUserId));
        Assert.DoesNotContain(_audit.Events, x => x.MetadataJson?.Contains("Versions", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task FailedPolicyPublishDoesNotProduceAuditEvent()
    {
        var leaveType = _repository.AddLeaveType("VACATION", true);
        var bucket = _repository.AddBucket("VACATION_DAYS", true);
        var policy = await _service.CreatePolicyAsync(new(leaveType.Id, null, false, null), CancellationToken.None);
        var published = await _service.CreateVersionAsync(policy.Id, Command(consumesBalance: true, balanceBucketId: bucket.Id), CancellationToken.None);
        await _service.PublishVersionAsync(published.Id, CancellationToken.None);
        var overlapping = await _service.CreateVersionAsync(policy.Id, Command(effectiveFrom: new DateOnly(2026, 6, 1), consumesBalance: true, balanceBucketId: bucket.Id), CancellationToken.None);
        _audit.Events.Clear();

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.PublishVersionAsync(overlapping.Id, CancellationToken.None));

        Assert.Empty(_audit.Events);
    }

    [Fact]
    public async Task VersionNumbersIncrementAndPublishedVersionsAreImmutable()
    {
        var leaveType = _repository.AddLeaveType("VACATION", true);
        var bucket = _repository.AddBucket("VACATION_DAYS", true);
        var policy = await _service.CreatePolicyAsync(new(leaveType.Id, null, false, null), CancellationToken.None);

        var v1 = await _service.CreateVersionAsync(policy.Id, Command(consumesBalance: true, balanceBucketId: bucket.Id), CancellationToken.None);
        var v2 = await _service.CreateVersionAsync(policy.Id, Command(effectiveFrom: new DateOnly(2027, 1, 1), consumesBalance: true, balanceBucketId: bucket.Id), CancellationToken.None);
        await _service.PublishVersionAsync(v1.Id, CancellationToken.None);

        Assert.Equal(1, v1.VersionNumber);
        Assert.Equal(2, v2.VersionNumber);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.UpdateVersionAsync(v1.Id, UpdateCommand(), CancellationToken.None));
        Assert.Contains("immutable", ex.Message);
    }

    [Theory]
    [InlineData("BUSINESS_DAYS")]
    [InlineData("CALENDAR_DAYS")]
    public async Task ControlledDayCountModesPersist(string mode)
    {
        var leaveType = _repository.AddLeaveType("VACATION", true);
        var policy = await _service.CreatePolicyAsync(new(leaveType.Id, null, false, null), CancellationToken.None);

        var version = await _service.CreateVersionAsync(policy.Id, Command(dayCountMode: mode, noticeMode: mode), CancellationToken.None);

        Assert.Equal(mode, version.DayCountMode);
        Assert.Equal(mode, version.NoticeDayCountMode);
    }

    [Theory]
    [InlineData("BLOCK")]
    [InlineData("WARN")]
    [InlineData("ALLOW")]
    public async Task ControlledOverlapBehaviorPersists(string behavior)
    {
        var leaveType = _repository.AddLeaveType("VACATION", true);
        var policy = await _service.CreatePolicyAsync(new(leaveType.Id, null, false, null), CancellationToken.None);

        var version = await _service.CreateVersionAsync(policy.Id, Command(overlap: behavior), CancellationToken.None);

        Assert.Equal(behavior, version.OverlapBehavior);
    }

    [Fact]
    public async Task CreateVersionRejectsMissingBalanceBucketWithClearMessage()
    {
        var leaveType = _repository.AddLeaveType("VACATION", true);
        var policy = await _service.CreatePolicyAsync(new(leaveType.Id, null, false, null), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateVersionAsync(policy.Id, Command(consumesBalance: true, balanceBucketId: null), CancellationToken.None));

        Assert.Equal("BalanceBucketId is required when ConsumesBalance is true.", ex.Message);
    }

    [Fact]
    public async Task UpdateVersionRejectsInvalidBalanceConfigurationWithClearMessages()
    {
        var leaveType = _repository.AddLeaveType("VACATION", true);
        var bucket = _repository.AddBucket("VACATION_DAYS", true);
        var policy = await _service.CreatePolicyAsync(new(leaveType.Id, null, false, null), CancellationToken.None);
        var version = await _service.CreateVersionAsync(policy.Id, Command(), CancellationToken.None);

        var missingBucket = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.UpdateVersionAsync(version.Id, new(new DateOnly(2026, 1, 1), null, "BUSINESS_DAYS", true, 7, "CALENDAR_DAYS", 15m, "BLOCK", true, null, TestWorkingCalendarId), CancellationToken.None));
        var unexpectedBucket = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.UpdateVersionAsync(version.Id, new(new DateOnly(2026, 1, 1), null, "BUSINESS_DAYS", true, 7, "CALENDAR_DAYS", 15m, "BLOCK", false, bucket.Id, TestWorkingCalendarId), CancellationToken.None));

        Assert.Equal("BalanceBucketId is required when ConsumesBalance is true.", missingBucket.Message);
        Assert.Equal("BalanceBucketId must be empty when ConsumesBalance is false.", unexpectedBucket.Message);
    }

    [Fact]
    public async Task PublishRejectsOverlappingPublishedPeriodAndInactiveReferences()
    {
        var leaveType = _repository.AddLeaveType("VACATION", true);
        var bucket = _repository.AddBucket("VACATION_DAYS", true);
        var policy = await _service.CreatePolicyAsync(new(leaveType.Id, null, false, null), CancellationToken.None);
        var v1 = await _service.CreateVersionAsync(policy.Id, Command(consumesBalance: true, balanceBucketId: bucket.Id), CancellationToken.None);
        await _service.PublishVersionAsync(v1.Id, CancellationToken.None);
        var overlapping = await _service.CreateVersionAsync(policy.Id, Command(effectiveFrom: new DateOnly(2026, 6, 1), consumesBalance: true, balanceBucketId: bucket.Id), CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.PublishVersionAsync(overlapping.Id, CancellationToken.None));

        bucket.UpdateDetails(bucket.Name, bucket.Description, bucket.Unit, false, DateTime.UtcNow);
        var future = await _service.CreateVersionAsync(policy.Id, Command(effectiveFrom: new DateOnly(2027, 1, 1), consumesBalance: true, balanceBucketId: bucket.Id), CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.PublishVersionAsync(future.Id, CancellationToken.None));

        var historical = await _service.GetVersionAsync(v1.Id, CancellationToken.None);
        Assert.Equal("PUBLISHED", historical!.Status);
    }

    [Fact]
    public async Task PublishRejectsInactiveLeaveType()
    {
        var leaveType = _repository.AddLeaveType("VACATION", false);
        var policy = await _service.CreatePolicyAsync(new(leaveType.Id, null, false, null), CancellationToken.None);
        var version = await _service.CreateVersionAsync(policy.Id, Command(), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.PublishVersionAsync(version.Id, CancellationToken.None));

        Assert.Contains("Inactive leave types", ex.Message);
    }

    [Fact]
    public async Task PolicyVersionBusinessDaysRequiresWorkingCalendarButCalendarDaysOnlyMayOmitIt()
    {
        var leaveType = _repository.AddLeaveType("VACATION", true);
        var policy = await _service.CreatePolicyAsync(new(leaveType.Id, null, false, null), CancellationToken.None);

        var calendarDays = await _service.CreateVersionAsync(policy.Id, Command(dayCountMode: "CALENDAR_DAYS", noticeMode: "CALENDAR_DAYS", omitWorkingCalendar: true), CancellationToken.None);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateVersionAsync(policy.Id, Command(dayCountMode: "BUSINESS_DAYS", noticeMode: "CALENDAR_DAYS", omitWorkingCalendar: true), CancellationToken.None));

        Assert.Null(calendarDays.WorkingCalendarId);
        Assert.Contains("WorkingCalendarId is required", ex.Message);
    }

    [Fact]
    public async Task PublishRejectsInactiveWorkingCalendarButHistoricalVersionRemainsReadable()
    {
        var leaveType = _repository.AddLeaveType("VACATION", true);
        var inactiveCalendar = _repository.AddCalendar("INACTIVE", false);
        var policy = await _service.CreatePolicyAsync(new(leaveType.Id, null, false, null), CancellationToken.None);
        var historical = await _service.CreateVersionAsync(policy.Id, Command(), CancellationToken.None);
        await _service.PublishVersionAsync(historical.Id, CancellationToken.None);
        var future = await _service.CreateVersionAsync(policy.Id, Command(effectiveFrom: new DateOnly(2027, 1, 1), workingCalendarId: inactiveCalendar.Id), CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.PublishVersionAsync(future.Id, CancellationToken.None));

        Assert.Equal("PUBLISHED", (await _service.GetVersionAsync(historical.Id, CancellationToken.None))!.Status);
    }

    [Fact]
    public async Task ResolverPrefersDeepestApplicablePublishedOverrideAndIgnoresDrafts()
    {
        var leaveType = _repository.AddLeaveType("VACATION", true);
        var company = _repository.AddOrg("COMPANY", null);
        var it = _repository.AddOrg("IT", company.Id);
        var support = _repository.AddOrg("SUPPORT", it.Id);
        var companyPolicy = await _service.CreatePolicyAsync(new(leaveType.Id, null, false, null), CancellationToken.None);
        var itPolicy = await _service.CreatePolicyAsync(new(leaveType.Id, it.Id, true, null), CancellationToken.None);
        var supportPolicy = await _service.CreatePolicyAsync(new(leaveType.Id, support.Id, false, null), CancellationToken.None);
        await _service.PublishVersionAsync((await _service.CreateVersionAsync(companyPolicy.Id, Command(), CancellationToken.None)).Id, CancellationToken.None);
        await _service.PublishVersionAsync((await _service.CreateVersionAsync(itPolicy.Id, Command(maximum: 10), CancellationToken.None)).Id, CancellationToken.None);
        await _service.CreateVersionAsync(supportPolicy.Id, Command(maximum: 5), CancellationToken.None);

        var resolvedBeforePublish = await _service.ResolvePolicyAsync(leaveType.Id, support.Id, new DateOnly(2026, 1, 1), CancellationToken.None);
        await _service.PublishVersionAsync((await _service.ListVersionsAsync(supportPolicy.Id, CancellationToken.None)).Single().Id, CancellationToken.None);
        var resolvedAfterPublish = await _service.ResolvePolicyAsync(leaveType.Id, support.Id, new DateOnly(2026, 1, 1), CancellationToken.None);

        Assert.True(resolvedBeforePublish.Found);
        Assert.Equal(itPolicy.Id, resolvedBeforePublish.Policy!.Id);
        Assert.Equal(supportPolicy.Id, resolvedAfterPublish.Policy!.Id);
    }

    [Fact]
    public async Task ResolverIgnoresAncestorOverrideWhenDescendantsAreDisabled()
    {
        var leaveType = _repository.AddLeaveType("VACATION", true);
        var company = _repository.AddOrg("COMPANY", null);
        var it = _repository.AddOrg("IT", company.Id);
        var support = _repository.AddOrg("SUPPORT", it.Id);
        var companyPolicy = await _service.CreatePolicyAsync(new(leaveType.Id, null, false, null), CancellationToken.None);
        var itPolicy = await _service.CreatePolicyAsync(new(leaveType.Id, it.Id, false, null), CancellationToken.None);
        await _service.PublishVersionAsync((await _service.CreateVersionAsync(companyPolicy.Id, Command(), CancellationToken.None)).Id, CancellationToken.None);
        await _service.PublishVersionAsync((await _service.CreateVersionAsync(itPolicy.Id, Command(maximum: 10), CancellationToken.None)).Id, CancellationToken.None);

        var resolved = await _service.ResolvePolicyAsync(leaveType.Id, support.Id, new DateOnly(2026, 1, 1), CancellationToken.None);

        Assert.True(resolved.Found);
        Assert.Equal(companyPolicy.Id, resolved.Policy!.Id);
    }

    [Fact]
    public async Task ResolverFallsBackToCompanyPolicyWhenNoOverrideApplies()
    {
        var leaveType = _repository.AddLeaveType("VACATION", true);
        var company = _repository.AddOrg("COMPANY", null);
        var it = _repository.AddOrg("IT", company.Id);
        var companyPolicy = await _service.CreatePolicyAsync(new(leaveType.Id, null, false, null), CancellationToken.None);
        await _service.PublishVersionAsync((await _service.CreateVersionAsync(companyPolicy.Id, Command(), CancellationToken.None)).Id, CancellationToken.None);

        var resolved = await _service.ResolvePolicyAsync(leaveType.Id, it.Id, new DateOnly(2026, 1, 1), CancellationToken.None);

        Assert.True(resolved.Found);
        Assert.Equal(companyPolicy.Id, resolved.Policy!.Id);
    }

    [Fact]
    public async Task ResolverPrefersExactOrgUnitOverrideOverCompanyPolicy()
    {
        var leaveType = _repository.AddLeaveType("VACATION", true);
        var company = _repository.AddOrg("COMPANY", null);
        var it = _repository.AddOrg("IT", company.Id);
        var companyPolicy = await _service.CreatePolicyAsync(new(leaveType.Id, null, false, null), CancellationToken.None);
        var itPolicy = await _service.CreatePolicyAsync(new(leaveType.Id, it.Id, false, null), CancellationToken.None);
        await _service.PublishVersionAsync((await _service.CreateVersionAsync(companyPolicy.Id, Command(maximum: 15), CancellationToken.None)).Id, CancellationToken.None);
        await _service.PublishVersionAsync((await _service.CreateVersionAsync(itPolicy.Id, Command(maximum: 10), CancellationToken.None)).Id, CancellationToken.None);

        var resolved = await _service.ResolvePolicyAsync(leaveType.Id, it.Id, new DateOnly(2026, 1, 1), CancellationToken.None);

        Assert.True(resolved.Found);
        Assert.Equal(itPolicy.Id, resolved.Policy!.Id);
    }

    [Fact]
    public async Task ResolverDoesNotMatchSiblingOrgUnitAndReturnsClearNoPolicyResult()
    {
        var leaveType = _repository.AddLeaveType("VACATION", true);
        var company = _repository.AddOrg("COMPANY", null);
        var it = _repository.AddOrg("IT", company.Id);
        var hr = _repository.AddOrg("HR", company.Id);
        var itPolicy = await _service.CreatePolicyAsync(new(leaveType.Id, it.Id, true, null), CancellationToken.None);
        await _service.PublishVersionAsync((await _service.CreateVersionAsync(itPolicy.Id, Command(), CancellationToken.None)).Id, CancellationToken.None);

        var resolved = await _service.ResolvePolicyAsync(leaveType.Id, hr.Id, new DateOnly(2026, 1, 1), CancellationToken.None);

        Assert.False(resolved.Found);
        Assert.Null(resolved.Policy);
        Assert.Null(resolved.Version);
        Assert.False(string.IsNullOrWhiteSpace(resolved.Reason));
    }

    [Fact]
    public async Task ResolverIgnoresInactivePolicyDefinitions()
    {
        var leaveType = _repository.AddLeaveType("VACATION", true);
        var policy = await _service.CreatePolicyAsync(new(leaveType.Id, null, false, null), CancellationToken.None);
        await _service.PublishVersionAsync((await _service.CreateVersionAsync(policy.Id, Command(), CancellationToken.None)).Id, CancellationToken.None);
        await _service.UpdatePolicyAsync(policy.Id, new(null, false, false), CancellationToken.None);

        var resolved = await _service.ResolvePolicyAsync(leaveType.Id, null, new DateOnly(2026, 1, 1), CancellationToken.None);

        Assert.False(resolved.Found);
        Assert.Null(resolved.Policy);
        Assert.Null(resolved.Version);
        Assert.False(string.IsNullOrWhiteSpace(resolved.Reason));
    }

    [Fact]
    public async Task ResolverRespectsEffectiveDatesAndReturnsNoPolicy()
    {
        var leaveType = _repository.AddLeaveType("VACATION", true);
        var policy = await _service.CreatePolicyAsync(new(leaveType.Id, null, false, null), CancellationToken.None);
        var version = await _service.CreateVersionAsync(policy.Id, Command(effectiveFrom: new DateOnly(2026, 1, 1), effectiveTo: new DateOnly(2026, 12, 31)), CancellationToken.None);
        await _service.PublishVersionAsync(version.Id, CancellationToken.None);

        Assert.False((await _service.ResolvePolicyAsync(leaveType.Id, null, new DateOnly(2025, 12, 31), CancellationToken.None)).Found);
        Assert.True((await _service.ResolvePolicyAsync(leaveType.Id, null, new DateOnly(2026, 12, 31), CancellationToken.None)).Found);
        Assert.False((await _service.ResolvePolicyAsync(leaveType.Id, null, new DateOnly(2027, 1, 1), CancellationToken.None)).Found);
    }

    private static CreateLeavePolicyVersionCommand Command(DateOnly? effectiveFrom = null, DateOnly? effectiveTo = null, string dayCountMode = "BUSINESS_DAYS", string noticeMode = "CALENDAR_DAYS", string overlap = "BLOCK", decimal? maximum = 15m, bool consumesBalance = false, Guid? balanceBucketId = null, Guid? workingCalendarId = null, bool omitWorkingCalendar = false) =>
        new(effectiveFrom ?? new DateOnly(2026, 1, 1), effectiveTo, dayCountMode, true, 7, noticeMode, maximum, overlap, consumesBalance, balanceBucketId, omitWorkingCalendar ? null : workingCalendarId ?? TestWorkingCalendarId);

    private static UpdateLeavePolicyVersionCommand UpdateCommand() =>
        new(new DateOnly(2026, 1, 1), null, "CALENDAR_DAYS", false, null, "CALENDAR_DAYS", null, "ALLOW", false, null, null);

    private sealed class FakeLeavePolicyRepository : ILeavePolicyRepository
    {
        private readonly List<LeavePolicy> _policies = [];
        private readonly List<LeavePolicyVersion> _versions = [];
        private readonly List<LeaveType> _types = [];
        private readonly List<BalanceBucket> _buckets = [];
        private readonly List<OrgUnit> _orgs = [];
        private readonly List<WorkingCalendar> _calendars = [TestCalendar()];

        public LeaveType AddLeaveType(string code, bool active) { var x = LeaveType.Create(code, code, null, 0, active, DateTime.UtcNow); _types.Add(x); return x; }
        public BalanceBucket AddBucket(string code, bool active) { var x = BalanceBucket.Create(code, code, null, BalanceBucketUnit.Day, active, DateTime.UtcNow); _buckets.Add(x); return x; }
        public OrgUnit AddOrg(string code, Guid? parentId) { var x = OrgUnit.Create(code, code, parentId, DateTime.UtcNow); _orgs.Add(x); return x; }
        public WorkingCalendar AddCalendar(string code, bool active) { var x = WorkingCalendar.Create(code, code, null, active, StandardWeekdays(), DateTime.UtcNow); _calendars.Add(x); return x; }
        private static WorkingCalendar TestCalendar() { var x = WorkingCalendar.Create("TEST", "Test", null, true, StandardWeekdays(), DateTime.UtcNow); typeof(WorkingCalendar).GetProperty(nameof(WorkingCalendar.Id))!.SetValue(x, TestWorkingCalendarId); return x; }
        private static Dictionary<DayOfWeek, bool> StandardWeekdays() => new() { [DayOfWeek.Sunday] = false, [DayOfWeek.Monday] = true, [DayOfWeek.Tuesday] = true, [DayOfWeek.Wednesday] = true, [DayOfWeek.Thursday] = true, [DayOfWeek.Friday] = true, [DayOfWeek.Saturday] = false };

        public Task<List<LeavePolicy>> ListPoliciesAsync(CancellationToken cancellationToken) => Task.FromResult(_policies.ToList());
        public Task<LeavePolicy?> GetPolicyAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(_policies.SingleOrDefault(x => x.Id == id));
        public Task<LeavePolicyVersion?> GetVersionAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(_versions.SingleOrDefault(x => x.Id == id));
        public Task<List<LeavePolicyVersion>> ListVersionsAsync(Guid policyId, CancellationToken cancellationToken) => Task.FromResult(_versions.Where(x => x.LeavePolicyId == policyId).OrderBy(x => x.VersionNumber).ToList());
        public Task<bool> ExactPolicyScopeExistsAsync(Guid leaveTypeId, Guid? orgUnitId, Guid? excludingPolicyId, CancellationToken cancellationToken) => Task.FromResult(_policies.Any(x => x.LeaveTypeId == leaveTypeId && x.OrgUnitId == orgUnitId && (excludingPolicyId is null || x.Id != excludingPolicyId)));
        public Task<bool> HasPublishedVersionsAsync(Guid policyId, CancellationToken cancellationToken) => Task.FromResult(_versions.Any(x => x.LeavePolicyId == policyId && x.Status == LeavePolicyVersionStatus.Published));
        public Task<int> GetNextVersionNumberAsync(Guid policyId, CancellationToken cancellationToken) => Task.FromResult(_versions.Where(x => x.LeavePolicyId == policyId).Select(x => (int?)x.VersionNumber).Max() is { } max ? max + 1 : 1);
        public Task<bool> HasOverlappingPublishedVersionAsync(Guid policyId, DateOnly effectiveFrom, DateOnly? effectiveTo, Guid? excludingVersionId, CancellationToken cancellationToken) => Task.FromResult(_versions.Any(x => x.LeavePolicyId == policyId && x.Status == LeavePolicyVersionStatus.Published && (excludingVersionId is null || x.Id != excludingVersionId) && x.EffectiveFrom <= (effectiveTo ?? DateOnly.MaxValue) && (x.EffectiveTo ?? DateOnly.MaxValue) >= effectiveFrom));
        public Task<List<LeavePolicy>> ListPoliciesForLeaveTypeWithPublishedVersionsAsync(Guid leaveTypeId, DateOnly date, CancellationToken cancellationToken) { foreach (var p in _policies) { p.Versions.Clear(); p.Versions.AddRange(_versions.Where(v => v.LeavePolicyId == p.Id && v.Status == LeavePolicyVersionStatus.Published && v.EffectiveFrom <= date && (v.EffectiveTo is null || v.EffectiveTo >= date))); } return Task.FromResult(_policies.Where(x => x.LeaveTypeId == leaveTypeId && x.IsActive && x.Versions.Count > 0).ToList()); }
        public Task AddPolicyAsync(LeavePolicy policy, CancellationToken cancellationToken) { _policies.Add(policy); return Task.CompletedTask; }
        public Task AddVersionAsync(LeavePolicyVersion version, CancellationToken cancellationToken) { _versions.Add(version); return Task.CompletedTask; }
        public Task<LeaveType?> GetLeaveTypeAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(_types.SingleOrDefault(x => x.Id == id));
        public Task<BalanceBucket?> GetBalanceBucketAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(_buckets.SingleOrDefault(x => x.Id == id));
        public Task<WorkingCalendar?> GetWorkingCalendarAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(_calendars.SingleOrDefault(x => x.Id == id));
        public Task<OrgUnit?> GetOrgUnitAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(_orgs.SingleOrDefault(x => x.Id == id));
        public Task<List<OrgUnit>> ListOrgUnitsAsync(CancellationToken cancellationToken) => Task.FromResult(_orgs.ToList());
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeAuditWriter : IAuditWriter
    {
        public List<AuditEventData> Events { get; } = [];
        public Task WriteAsync(AuditEventData auditEvent, CancellationToken cancellationToken)
        {
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedCurrentActor(Guid userId) : ICurrentActor { public Guid? UserId => userId; }
}



