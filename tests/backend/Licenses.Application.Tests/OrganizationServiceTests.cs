using Licenses.Application.Audit;
using Licenses.Application.Organization;
using Licenses.Domain.Identity;
using Licenses.Domain.Organization;

namespace Licenses.Application.Tests;

public sealed class OrganizationServiceTests
{
    private readonly OrganizationService _service;
    private readonly FakeOrganizationRepository _repository = new();
    private readonly FakeAuditWriter _auditWriter = new();

    public OrganizationServiceTests()
    {
        _service = new OrganizationService(_repository, TimeProvider.System, _auditWriter);
    }

    [Fact]
    public async Task CreatesParentChildHierarchyAndTree()
    {
        var company = await _service.CreateOrgUnitAsync(new("Company", "COMPANY", null), CancellationToken.None);
        var it = await _service.CreateOrgUnitAsync(new("IT", "IT", company.Id), CancellationToken.None);

        var tree = await _service.GetOrgUnitTreeAsync(CancellationToken.None);

        Assert.Single(tree);
        Assert.Equal(company.Id, tree[0].Id);
        Assert.Equal(it.Id, tree[0].Children[0].Id);
    }

    [Fact]
    public async Task PreventsHierarchyCycles()
    {
        var company = await _service.CreateOrgUnitAsync(new("Company", "COMPANY", null), CancellationToken.None);
        var it = await _service.CreateOrgUnitAsync(new("IT", "IT", company.Id), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UpdateOrgUnitAsync(company.Id, new("Company", "COMPANY", it.Id, true), CancellationToken.None));

        Assert.Contains("cycles", ex.Message);
    }

    [Fact]
    public async Task PreventsDuplicateOrgUnitCode()
    {
        await _service.CreateOrgUnitAsync(new("Company", "COMPANY", null), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreateOrgUnitAsync(new("Other", "company", null), CancellationToken.None));

        Assert.Contains("code", ex.Message);
    }

    [Fact]
    public async Task CreatesUsersAndPreventsDuplicateExternalIdentityId()
    {
        await _service.CreateUserAsync(new("Felipe", "felipe@example.test", "entra-1"), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreateUserAsync(new("Other", "other@example.test", "entra-1"), CancellationToken.None));

        Assert.Contains("External identity", ex.Message);
    }

    [Fact]
    public async Task CreatesUpdatesDeactivatesAndReactivatesUserWithAudit()
    {
        var actorId = Guid.NewGuid();

        var user = await _service.CreateUserAsync(new("Felipe", "felipe@example.test", null), CancellationToken.None, actorId);
        var updated = await _service.UpdateUserAsync(user.Id, new("Felipe Updated", "felipe.updated@example.test", null, false), CancellationToken.None, actorId);
        var reactivated = await _service.UpdateUserAsync(user.Id, new("Felipe Updated", "felipe.updated@example.test", null, true), CancellationToken.None, actorId);

        Assert.False(updated!.IsActive);
        Assert.True(reactivated!.IsActive);
        Assert.Equal(["user.create", "user.deactivate", "user.activate"], _auditWriter.Events.Select(x => x.Action).ToArray());
        Assert.All(_auditWriter.Events, auditEvent =>
        {
            Assert.Equal(actorId, auditEvent.ActorUserId);
            Assert.Equal(user.Id, auditEvent.SubjectUserId);
            Assert.Equal("User", auditEvent.ResourceType);
            Assert.Equal(user.Id, auditEvent.ResourceId);
        });
    }

    [Fact]
    public async Task PreventsDuplicateUserEmail()
    {
        await _service.CreateUserAsync(new("Felipe", "felipe@example.test", null), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreateUserAsync(new("Other", "FELIPE@example.test", null), CancellationToken.None));

        Assert.Contains("email", ex.Message);
    }

    [Fact]
    public async Task AllowsMultipleAssignmentsButOnlyOneActivePrimary()
    {
        var it = await _service.CreateOrgUnitAsync(new("IT", "IT", null), CancellationToken.None);
        var support = await _service.CreateOrgUnitAsync(new("Support", "SUPPORT", it.Id), CancellationToken.None);
        var user = await _service.CreateUserAsync(new("Felipe", "felipe@example.test", null), CancellationToken.None);

        await _service.CreateAssignmentAsync(user.Id, new(it.Id, true, DateTime.UtcNow.AddDays(-1), null), CancellationToken.None);
        await _service.CreateAssignmentAsync(user.Id, new(support.Id, false, DateTime.UtcNow.AddDays(-1), null), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreateAssignmentAsync(user.Id, new(support.Id, true, DateTime.UtcNow.AddDays(-1), null), CancellationToken.None));

        var assignments = await _service.ListAssignmentsAsync(user.Id, CancellationToken.None);
        Assert.Equal(2, assignments!.Count);
        Assert.Contains("primary", ex.Message);
    }

    [Fact]
    public async Task UpdatesAndEndsUserOrgAssignmentWithAudit()
    {
        var actorId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var it = await _service.CreateOrgUnitAsync(new("IT", "IT", null), CancellationToken.None);
        var support = await _service.CreateOrgUnitAsync(new("Support", "SUPPORT", it.Id), CancellationToken.None);
        var user = await _service.CreateUserAsync(new("Felipe", "felipe@example.test", null), CancellationToken.None);
        var assignment = await _service.CreateAssignmentAsync(user.Id, new(it.Id, false, now.AddDays(-2), null), CancellationToken.None);
        _auditWriter.Events.Clear();

        var updated = await _service.UpdateAssignmentAsync(user.Id, assignment!.Id, new(support.Id, true, now.AddDays(-1), null), CancellationToken.None, actorId);
        var ended = await _service.EndAssignmentAsync(user.Id, assignment.Id, new(now), CancellationToken.None, actorId);

        Assert.Equal(support.Id, updated!.OrgUnitId);
        Assert.True(updated.IsPrimary);
        Assert.Equal(now, ended!.EffectiveToUtc);
        Assert.Equal(["organization.assignment.update", "organization.assignment.end"], _auditWriter.Events.Select(x => x.Action).ToArray());
        Assert.All(_auditWriter.Events, auditEvent =>
        {
            Assert.Equal(actorId, auditEvent.ActorUserId);
            Assert.Equal(user.Id, auditEvent.SubjectUserId);
            Assert.Equal("UserOrgAssignment", auditEvent.ResourceType);
        });
    }

    [Fact]
    public async Task OrgUnitCreateAndUpdateProduceFocusedAuditEvents()
    {
        var actorId = Guid.NewGuid();

        var company = await _service.CreateOrgUnitAsync(new("Company", "COMPANY", null), CancellationToken.None, actorId);
        var updated = await _service.UpdateOrgUnitAsync(company.Id, new("Company Updated", "COMPANY2", null, false), CancellationToken.None, actorId);

        Assert.NotNull(updated);
        Assert.Collection(_auditWriter.Events,
            created =>
            {
                Assert.Equal("organization.unit.create", created.Action);
                Assert.Equal("OrgUnit", created.ResourceType);
                Assert.Equal(company.Id, created.ResourceId);
                Assert.Equal(company.Id, created.OrgUnitId);
                Assert.Equal(actorId, created.ActorUserId);
                Assert.Null(created.SubjectUserId);
                Assert.Contains("COMPANY", created.MetadataJson);
            },
            deactivated =>
            {
                Assert.Equal("organization.unit.deactivate", deactivated.Action);
                Assert.Equal("OrgUnit", deactivated.ResourceType);
                Assert.Equal(company.Id, deactivated.ResourceId);
                Assert.Equal(company.Id, deactivated.OrgUnitId);
                Assert.Equal(actorId, deactivated.ActorUserId);
                Assert.Contains("COMPANY2", deactivated.MetadataJson);
                Assert.Contains("previousIsActive", deactivated.MetadataJson);
            });
    }

    [Fact]
    public async Task UserOrgAssignmentCreateProducesFocusedAuditEvent()
    {
        var actorId = Guid.NewGuid();
        var effectiveFrom = DateTime.UtcNow.AddDays(-1);
        var orgUnit = await _service.CreateOrgUnitAsync(new("IT", "IT", null), CancellationToken.None, actorId);
        var user = await _service.CreateUserAsync(new("Subject", "subject@example.test", null), CancellationToken.None);
        _auditWriter.Events.Clear();

        var assignment = await _service.CreateAssignmentAsync(user.Id, new(orgUnit.Id, true, effectiveFrom, null), CancellationToken.None, actorId);

        Assert.NotNull(assignment);
        var auditEvent = Assert.Single(_auditWriter.Events);
        Assert.Equal("organization.assignment.create", auditEvent.Action);
        Assert.Equal("UserOrgAssignment", auditEvent.ResourceType);
        Assert.Equal(assignment!.Id, auditEvent.ResourceId);
        Assert.Equal(actorId, auditEvent.ActorUserId);
        Assert.Equal(user.Id, auditEvent.SubjectUserId);
        Assert.Equal(orgUnit.Id, auditEvent.OrgUnitId);
        Assert.Contains("isPrimary", auditEvent.MetadataJson);
    }

    [Fact]
    public async Task FailedOrganizationMutationDoesNotProduceAuditEvent()
    {
        await _service.CreateOrgUnitAsync(new("Company", "COMPANY", null), CancellationToken.None, Guid.NewGuid());
        _auditWriter.Events.Clear();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreateOrgUnitAsync(new("Other", "company", null), CancellationToken.None, Guid.NewGuid()));

        Assert.Empty(_auditWriter.Events);
    }

    private sealed class FakeOrganizationRepository : IOrganizationRepository
    {
        private readonly List<OrgUnit> _orgUnits = [];
        private readonly List<User> _users = [];
        private readonly List<UserOrgAssignment> _assignments = [];
        public Task<List<OrgUnit>> ListOrgUnitsAsync(CancellationToken cancellationToken) => Task.FromResult(_orgUnits.OrderBy(x => x.Name).ToList());
        public Task<OrgUnit?> GetOrgUnitAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(_orgUnits.SingleOrDefault(x => x.Id == id));
        public Task<bool> OrgUnitCodeExistsAsync(string code, Guid? excludingId, CancellationToken cancellationToken) => Task.FromResult(_orgUnits.Any(x => x.Code == code && (excludingId == null || x.Id != excludingId)));
        public Task<bool> OrgUnitExistsAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(_orgUnits.Any(x => x.Id == id));
        public Task<List<Guid>> GetOrgUnitAncestorIdsAsync(Guid orgUnitId, CancellationToken cancellationToken)
        {
            var ancestors = new List<Guid>();
            var byId = _orgUnits.ToDictionary(x => x.Id);
            var current = orgUnitId;
            while (byId.TryGetValue(current, out var unit) && unit.ParentId is { } parentId)
            {
                ancestors.Add(parentId);
                current = parentId;
            }
            return Task.FromResult(ancestors);
        }
        public Task AddOrgUnitAsync(OrgUnit orgUnit, CancellationToken cancellationToken) { _orgUnits.Add(orgUnit); return Task.CompletedTask; }
        public Task<List<User>> ListUsersAsync(CancellationToken cancellationToken) => Task.FromResult(_users.OrderBy(x => x.DisplayName).ToList());
        public Task<List<User>> ListUsersInOrgUnitsAsync(IReadOnlyCollection<Guid> orgUnitIds, DateTime utcNow, CancellationToken cancellationToken) => Task.FromResult(_users.Where(user => user.IsActive && _assignments.Any(assignment => assignment.UserId == user.Id && orgUnitIds.Contains(assignment.OrgUnitId) && assignment.IsActiveAt(utcNow))).OrderBy(x => x.DisplayName).ToList());
        public Task<User?> GetUserAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(_users.SingleOrDefault(x => x.Id == id));
        public Task<bool> EmailExistsAsync(string email, Guid? excludingId, CancellationToken cancellationToken) => Task.FromResult(_users.Any(x => x.Email == email && (excludingId == null || x.Id != excludingId)));
        public Task<bool> ExternalIdentityIdExistsAsync(string externalIdentityId, Guid? excludingId, CancellationToken cancellationToken) => Task.FromResult(_users.Any(x => x.ExternalIdentityId == externalIdentityId && (excludingId == null || x.Id != excludingId)));
        public Task AddUserAsync(User user, CancellationToken cancellationToken) { _users.Add(user); return Task.CompletedTask; }
        public Task<List<UserOrgAssignment>> ListAssignmentsAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(_assignments.Where(x => x.UserId == userId).ToList());
        public Task<UserOrgAssignment?> GetAssignmentAsync(Guid userId, Guid assignmentId, CancellationToken cancellationToken) => Task.FromResult(_assignments.SingleOrDefault(x => x.UserId == userId && x.Id == assignmentId));
        public Task<bool> HasOverlappingPrimaryAssignmentAsync(Guid userId, DateTime effectiveFromUtc, DateTime? effectiveToUtc, Guid? excludingAssignmentId, CancellationToken cancellationToken) =>
            Task.FromResult(_assignments.Any(x =>
                x.UserId == userId
                && (excludingAssignmentId == null || x.Id != excludingAssignmentId)
                && x.IsPrimary
                && x.EffectiveFromUtc < (effectiveToUtc ?? DateTime.MaxValue)
                && (x.EffectiveToUtc ?? DateTime.MaxValue) > effectiveFromUtc));
        public Task AddAssignmentAsync(UserOrgAssignment assignment, CancellationToken cancellationToken) { _assignments.Add(assignment); return Task.CompletedTask; }
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
}
