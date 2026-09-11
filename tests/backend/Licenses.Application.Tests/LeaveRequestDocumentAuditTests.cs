using Licenses.Application.Audit;
using Licenses.Application.Authorization;
using Licenses.Application.LeaveManagement;
using Licenses.Domain.Authorization;
using Licenses.Domain.Identity;
using Licenses.Domain.LeaveManagement;
using Licenses.Domain.Organization;

namespace Licenses.Application.Tests;

public sealed class LeaveRequestDocumentAuditTests
{
    private static readonly DateTime Now = new(2026, 9, 11, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task UploadAndAuthorizedReadProducePrivacySafeAuditEvents()
    {
        var setup = DocumentSetup.Create();
        var service = setup.CreateService(setup.Employee.Id, PermissionCodes.LeaveDocumentsUploadSelf, PermissionCodes.LeaveDocumentsReadSelf);

        var uploaded = await service.UploadAsync(setup.Request.Id, "medical.pdf", "application/pdf", PdfStream(), null, 1024, CancellationToken.None);
        var opened = await service.OpenContentAsync(uploaded!.Id, CancellationToken.None);

        Assert.NotNull(opened);
        Assert.Equal(["leave.document.upload", "leave.document.read"], setup.Audit.Events.Select(x => x.Action));
        Assert.All(setup.Audit.Events, audit =>
        {
            Assert.Equal(setup.Employee.Id, audit.ActorUserId);
            Assert.Equal(setup.Employee.Id, audit.SubjectUserId);
            Assert.Equal(setup.Unit.Id, audit.OrgUnitId);
            Assert.Equal("LeaveRequestDocument", audit.ResourceType);
            Assert.DoesNotContain("StorageKey", audit.MetadataJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("storage", audit.MetadataJson, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task DeniedDocumentReadsDoNotProduceSuccessfulReadAudit()
    {
        var setup = DocumentSetup.Create();
        var ownerService = setup.CreateService(setup.Employee.Id, PermissionCodes.LeaveDocumentsUploadSelf);
        var uploaded = await ownerService.UploadAsync(setup.Request.Id, "medical.pdf", "application/pdf", PdfStream(), null, 1024, CancellationToken.None);
        setup.Audit.Events.Clear();

        Assert.Null(await setup.CreateService(setup.Employee.Id).OpenContentAsync(uploaded!.Id, CancellationToken.None));
        Assert.Null(await setup.CreateService(setup.OutOfScopeManager.Id, PermissionCodes.LeaveDocumentsRead).OpenContentAsync(uploaded.Id, CancellationToken.None));
        Assert.Null(await setup.CreateService(setup.TechAdmin.Id, "system.tech_admin").OpenContentAsync(uploaded.Id, CancellationToken.None));

        Assert.Empty(setup.Audit.Events);
    }

    private static MemoryStream PdfStream() => new("%PDF-1.7 test"u8.ToArray());

    private sealed record DocumentSetup(User Employee, User OutOfScopeManager, User TechAdmin, OrgUnit Unit, OrgUnit OtherUnit, LeaveType Type, LeaveRequest Request, FakeLeaveRequestRepository Requests, FakePrivateDocumentStorage Storage, FakeAuditWriter Audit)
    {
        public static DocumentSetup Create()
        {
            var employee = User.Create("Employee", $"employee.{Guid.NewGuid():N}@example.test", null, Now);
            var manager = User.Create("Manager", $"manager.{Guid.NewGuid():N}@example.test", null, Now);
            var techAdmin = User.Create("Tech Admin", $"tech.{Guid.NewGuid():N}@example.test", null, Now);
            var unit = OrgUnit.Create("Engineering", "ENG" + Guid.NewGuid().ToString("N")[..8], null, Now);
            var otherUnit = OrgUnit.Create("Finance", "FIN" + Guid.NewGuid().ToString("N")[..8], null, Now);
            var type = LeaveType.Create("SICK" + Guid.NewGuid().ToString("N")[..8], "Sick Leave", null, 1, true, Now);
            var request = LeaveRequest.CreateDraft(employee.Id, unit.Id, type.Id, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1), LeaveRequestDayPortion.FullDay, null, employee.Id, Now);
            var requests = new FakeLeaveRequestRepository(employee, manager, techAdmin, unit, otherUnit, type, request);
            var storage = new FakePrivateDocumentStorage();
            var audit = new FakeAuditWriter();
            return new(employee, manager, techAdmin, unit, otherUnit, type, request, requests, storage, audit);
        }

        public LeaveRequestDocumentService CreateService(Guid actorId, params string[] permissions)
        {
            var auth = new AuthorizationService(new FakeAuthorizationRepository(actorId, Employee, OutOfScopeManager, TechAdmin, Unit, OtherUnit, permissions), new FixedTimeProvider(Now));
            return new LeaveRequestDocumentService(Requests, Storage, auth, new FixedCurrentActor(actorId), new FixedTimeProvider(Now), Audit);
        }
    }

    private sealed class FakeLeaveRequestRepository(User employee, User manager, User techAdmin, OrgUnit unit, OrgUnit otherUnit, LeaveType type, LeaveRequest request) : ILeaveRequestRepository
    {
        private readonly List<LeaveRequest> _requests = [request];
        private readonly List<LeaveRequestDocument> _documents = [];
        public Task<IReadOnlyList<LeaveRequest>> ListByUserAsync(Guid userId, CancellationToken ct) => Task.FromResult<IReadOnlyList<LeaveRequest>>(_requests.Where(x => x.UserId == userId).ToList());
        public Task<IReadOnlyList<LeaveRequest>> ListByUsersAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => Task.FromResult<IReadOnlyList<LeaveRequest>>(_requests.Where(x => ids.Contains(x.UserId)).ToList());
        public Task<IReadOnlyList<LeaveRequest>> ListPendingByOrgUnitsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => Task.FromResult<IReadOnlyList<LeaveRequest>>([]);
        public Task<IReadOnlyList<LeaveRequest>> ListPendingCancellationByOrgUnitsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => Task.FromResult<IReadOnlyList<LeaveRequest>>([]);
        public Task<LeaveRequest?> GetAsync(Guid id, bool tracking, CancellationToken ct) => Task.FromResult(_requests.SingleOrDefault(x => x.Id == id));
        public Task<LeaveRequest?> GetForUpdateAsync(Guid id, CancellationToken ct) => GetAsync(id, true, ct);
        public Task<LeaveRequest?> GetBySubmissionOperationIdAsync(Guid operationId, bool tracking, CancellationToken ct) => Task.FromResult<LeaveRequest?>(null);
        public Task<LeaveRequestDocument?> GetDocumentAsync(Guid id, bool tracking, CancellationToken ct) => Task.FromResult(_documents.SingleOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<LeaveRequestDocument>> ListDocumentsByRequestIdAsync(Guid requestId, CancellationToken ct) => Task.FromResult<IReadOnlyList<LeaveRequestDocument>>(_documents.Where(x => x.LeaveRequestId == requestId).ToList());
        public Task<IReadOnlyList<LeaveRequestDocument>> ListDocumentsByRequestIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => Task.FromResult<IReadOnlyList<LeaveRequestDocument>>(_documents.Where(x => ids.Contains(x.LeaveRequestId)).ToList());
        public Task AddDocumentAsync(LeaveRequestDocument document, CancellationToken ct) { _documents.Add(document); return Task.CompletedTask; }
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
        public Task AddAsync(LeaveRequest r, CancellationToken ct) { _requests.Add(r); return Task.CompletedTask; }
        public Task AddDecisionAsync(LeaveRequestDecision decision, CancellationToken ct) => Task.CompletedTask;
        public Task AddCancellationAsync(LeaveRequestCancellation cancellation, CancellationToken ct) => Task.CompletedTask;
        public Task AddRevocationAsync(LeaveRequestRevocation revocation, CancellationToken ct) => Task.CompletedTask;
        public Task<User?> GetUserAsync(Guid id, CancellationToken ct) => Task.FromResult<User?>(id == employee.Id ? employee : id == manager.Id ? manager : id == techAdmin.Id ? techAdmin : null);
        public Task<OrgUnit?> GetOrgUnitAsync(Guid id, CancellationToken ct) => Task.FromResult<OrgUnit?>(id == unit.Id ? unit : id == otherUnit.Id ? otherUnit : null);
        public Task<LeaveType?> GetLeaveTypeAsync(Guid id, CancellationToken ct) => Task.FromResult<LeaveType?>(id == type.Id ? type : null);
        public Task<LeavePolicy?> GetPolicyAsync(Guid id, CancellationToken ct) => Task.FromResult<LeavePolicy?>(null);
        public Task<LeavePolicyVersion?> GetPolicyVersionAsync(Guid id, CancellationToken ct) => Task.FromResult<LeavePolicyVersion?>(null);
        public Task<WorkingCalendar?> GetWorkingCalendarAsync(Guid id, CancellationToken ct) => Task.FromResult<WorkingCalendar?>(null);
        public Task<Guid?> GetBalanceAccountIdAsync(Guid userId, Guid balanceBucketId, CancellationToken ct) => Task.FromResult<Guid?>(null);
        public Task<bool> HasEffectiveAssignmentAsync(Guid userId, Guid orgUnitId, DateOnly date, CancellationToken ct) => Task.FromResult(true);
        public Task<IReadOnlyList<LeaveRequest>> ListOverlappingAsync(Guid userId, DateOnly start, DateOnly end, Guid excluding, CancellationToken ct) => Task.FromResult<IReadOnlyList<LeaveRequest>>([]);
        public Task<IReadOnlyList<User>> ListUsersInOrgUnitsAsync(IReadOnlyCollection<Guid> ids, DateTime now, CancellationToken ct) => Task.FromResult<IReadOnlyList<User>>([employee, manager, techAdmin]);
        public Task<IDisposable> BeginTransactionAsync(CancellationToken ct) => Task.FromResult<IDisposable>(new NoopTransaction());
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
        public Task CommitTransactionAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeAuthorizationRepository(Guid actorId, User employee, User manager, User techAdmin, OrgUnit unit, OrgUnit otherUnit, string[] permissions) : IAuthorizationRepository
    {
        private readonly Guid _roleId = Guid.NewGuid();
        public Task<User?> GetUserAsync(Guid id, CancellationToken ct) => Task.FromResult<User?>(id == employee.Id ? employee : id == manager.Id ? manager : id == techAdmin.Id ? techAdmin : null);
        public Task<OrgUnit?> GetOrgUnitAsync(Guid id, CancellationToken ct) => Task.FromResult<OrgUnit?>(id == unit.Id ? unit : id == otherUnit.Id ? otherUnit : null);
        public Task<List<OrgUnit>> ListOrgUnitsAsync(CancellationToken ct) => Task.FromResult(new List<OrgUnit> { unit, otherUnit });
        public Task<List<UserOrgAssignment>> ListActiveUserOrgAssignmentsAsync(Guid id, DateTime now, CancellationToken ct) => Task.FromResult(new List<UserOrgAssignment> { UserOrgAssignment.Create(id, id == manager.Id ? otherUnit.Id : unit.Id, true, Now.AddDays(-1), null) });
        public Task<List<RoleScopeAssignment>> ListActiveRoleScopeAssignmentsAsync(Guid id, DateTime now, CancellationToken ct) => Task.FromResult(id == actorId ? new List<RoleScopeAssignment> { RoleScopeAssignment.Create(id, _roleId, id == manager.Id ? otherUnit.Id : unit.Id, false, Now.AddDays(-1), null) } : []);
        public Task<bool> RoleHasPermissionAsync(Guid roleId, string code, CancellationToken ct) => Task.FromResult(roleId == _roleId && permissions.Contains(code));
        public Task<bool> IsRoleActiveAsync(Guid roleId, CancellationToken ct) => Task.FromResult(roleId == _roleId);
        public Task<List<User>> ListUsersInOrgUnitsAsync(IReadOnlyCollection<Guid> ids, DateTime now, CancellationToken ct) => Task.FromResult(new List<User> { employee, manager, techAdmin }.Where(user => user.Id != manager.Id || ids.Contains(otherUnit.Id)).ToList());
        public Task<List<DevelopmentActorDto>> ListDevelopmentActorsAsync(DateTime now, CancellationToken ct) => Task.FromResult(new List<DevelopmentActorDto>());
    }

    private sealed class FakePrivateDocumentStorage : IPrivateDocumentStorage
    {
        private readonly Dictionary<string, byte[]> _content = [];
        public async Task<UploadedPrivateDocument> StoreAsync(string storageKey, Stream content, CancellationToken cancellationToken)
        {
            await using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            _content[storageKey] = buffer.ToArray();
            return new UploadedPrivateDocument(storageKey, new string('A', 64), buffer.Length);
        }

        public Task<PrivateDocumentReadStream> OpenReadAsync(string storageKey, string contentType, CancellationToken cancellationToken) =>
            Task.FromResult(new PrivateDocumentReadStream(new MemoryStream(_content[storageKey], writable: false), contentType, _content[storageKey].Length));
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
    private sealed class FixedTimeProvider(DateTime now) : TimeProvider { public override DateTimeOffset GetUtcNow() => new(now); }
    private sealed class NoopTransaction : IDisposable { public void Dispose() { } }
}
