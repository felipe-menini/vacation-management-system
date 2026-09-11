using Licenses.Application.Audit;
using Licenses.Application.Authorization;
using Licenses.Domain.Authorization;
using Licenses.Domain.Identity;
using Licenses.Domain.Organization;

namespace Licenses.Application.Tests;

public sealed class AuthorizationAdminServiceTests
{
    private readonly FakeAuthorizationAdminRepository _repository = new();
    private readonly FakeAuditWriter _auditWriter = new();
    private readonly AuthorizationAdminService _service;

    public AuthorizationAdminServiceTests()
    {
        _service = new AuthorizationAdminService(_repository, TimeProvider.System, _auditWriter);
    }

    [Fact]
    public async Task AssignsExistingRoleAtOrgScopeAndPersistsIncludeDescendants()
    {
        var now = DateTime.UtcNow.AddDays(-1);
        var (user, role, orgUnit) = _repository.SeedActiveReferences();
        var actorId = Guid.NewGuid();

        var assignment = await _service.AssignRoleScopeAsync(new(user.Id, role.Id, orgUnit.Id, true, now, null), CancellationToken.None, actorId);

        Assert.Equal(user.Id, assignment.UserId);
        Assert.Equal(role.Id, assignment.RoleId);
        Assert.Equal(role.Code, assignment.RoleCode);
        Assert.Equal(orgUnit.Id, assignment.OrgUnitId);
        Assert.True(assignment.IncludeDescendants);
        var auditEvent = Assert.Single(_auditWriter.Events);
        Assert.Equal("authorization.role_scope.assign", auditEvent.Action);
        Assert.Equal(actorId, auditEvent.ActorUserId);
        Assert.Equal(user.Id, auditEvent.SubjectUserId);
        Assert.Equal(orgUnit.Id, auditEvent.OrgUnitId);
        Assert.Contains(role.Code, auditEvent.MetadataJson);
    }

    [Fact]
    public async Task RejectsDuplicateActiveRoleScopeAssignment()
    {
        var now = DateTime.UtcNow.AddDays(-1);
        var (user, role, orgUnit) = _repository.SeedActiveReferences();
        await _service.AssignRoleScopeAsync(new(user.Id, role.Id, orgUnit.Id, false, now, null), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.AssignRoleScopeAsync(new(user.Id, role.Id, orgUnit.Id, true, now.AddHours(1), null), CancellationToken.None));

        Assert.Contains("already exists", ex.Message);
    }

    [Fact]
    public async Task UpdatesRoleScopeAndWritesAudit()
    {
        var now = DateTime.UtcNow.AddDays(-2);
        var (user, role, orgUnit) = _repository.SeedActiveReferences();
        var otherOrgUnit = _repository.SeedOrgUnit("Other", "OTHER");
        var assignment = await _service.AssignRoleScopeAsync(new(user.Id, role.Id, orgUnit.Id, false, now, null), CancellationToken.None);
        _auditWriter.Events.Clear();

        var updated = await _service.UpdateRoleScopeAsync(assignment.Id, new(otherOrgUnit.Id, true, now.AddDays(1), null), CancellationToken.None, Guid.NewGuid());

        Assert.NotNull(updated);
        Assert.Equal(otherOrgUnit.Id, updated!.OrgUnitId);
        Assert.True(updated.IncludeDescendants);
        var auditEvent = Assert.Single(_auditWriter.Events);
        Assert.Equal("authorization.role_scope.update", auditEvent.Action);
        Assert.Equal(user.Id, auditEvent.SubjectUserId);
        Assert.Equal(otherOrgUnit.Id, auditEvent.OrgUnitId);
        Assert.Contains("previousOrgUnitId", auditEvent.MetadataJson);
    }

    [Fact]
    public async Task RevokesRoleScopeByEndingEffectivePeriodAndWritesAudit()
    {
        var now = DateTime.UtcNow;
        var (user, role, orgUnit) = _repository.SeedActiveReferences();
        var assignment = await _service.AssignRoleScopeAsync(new(user.Id, role.Id, orgUnit.Id, false, now.AddDays(-1), null), CancellationToken.None);
        _auditWriter.Events.Clear();

        var revoked = await _service.RevokeRoleScopeAsync(assignment.Id, new(now), CancellationToken.None, Guid.NewGuid());

        Assert.NotNull(revoked);
        Assert.Equal(now, revoked!.EffectiveToUtc);
        var auditEvent = Assert.Single(_auditWriter.Events);
        Assert.Equal("authorization.role_scope.revoke", auditEvent.Action);
        Assert.Equal(user.Id, auditEvent.SubjectUserId);
        Assert.Equal(orgUnit.Id, auditEvent.OrgUnitId);
        Assert.Contains(role.Code, auditEvent.MetadataJson);
    }

    [Fact]
    public async Task RejectsInvalidUserRoleAndOrgReferences()
    {
        var (user, role, orgUnit) = _repository.SeedActiveReferences();
        var now = DateTime.UtcNow.AddDays(-1);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.AssignRoleScopeAsync(new(Guid.NewGuid(), role.Id, orgUnit.Id, false, now, null), CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.AssignRoleScopeAsync(new(user.Id, Guid.NewGuid(), orgUnit.Id, false, now, null), CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.AssignRoleScopeAsync(new(user.Id, role.Id, Guid.NewGuid(), false, now, null), CancellationToken.None));

        Assert.Empty(_auditWriter.Events);
    }

    [Fact]
    public async Task FailedRoleScopeMutationDoesNotProduceAudit()
    {
        var (user, role, orgUnit) = _repository.SeedActiveReferences();
        user.Update(user.DisplayName, user.Email, user.ExternalIdentityId, false, DateTime.UtcNow);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.AssignRoleScopeAsync(new(user.Id, role.Id, orgUnit.Id, false, DateTime.UtcNow.AddDays(-1), null), CancellationToken.None, Guid.NewGuid()));

        Assert.Empty(_auditWriter.Events);
    }

    private sealed class FakeAuthorizationAdminRepository : IAuthorizationAdminRepository
    {
        private readonly List<User> _users = [];
        private readonly List<Role> _roles = [];
        private readonly List<OrgUnit> _orgUnits = [];
        private readonly List<RoleScopeAssignment> _assignments = [];

        public (User User, Role Role, OrgUnit OrgUnit) SeedActiveReferences()
        {
            var now = DateTime.UtcNow;
            var user = User.Create("Subject", $"subject.{Guid.NewGuid():N}@example.test", null, now);
            var role = Role.Create("ROLE_" + Guid.NewGuid().ToString("N")[..8], "Role", "Role description", true, now);
            var orgUnit = OrgUnit.Create("Org Unit", "ORG_" + Guid.NewGuid().ToString("N")[..8], null, now);
            _users.Add(user);
            _roles.Add(role);
            _orgUnits.Add(orgUnit);
            return (user, role, orgUnit);
        }

        public OrgUnit SeedOrgUnit(string name, string code)
        {
            var orgUnit = OrgUnit.Create(name, code + "_" + Guid.NewGuid().ToString("N")[..8], null, DateTime.UtcNow);
            _orgUnits.Add(orgUnit);
            return orgUnit;
        }

        public Task<User?> GetUserAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(_users.SingleOrDefault(x => x.Id == userId));
        public Task<Role?> GetRoleAsync(Guid roleId, CancellationToken cancellationToken) => Task.FromResult(_roles.SingleOrDefault(x => x.Id == roleId));
        public Task<List<Role>> ListRolesAsync(CancellationToken cancellationToken) => Task.FromResult(_roles.ToList());
        public Task<OrgUnit?> GetOrgUnitAsync(Guid orgUnitId, CancellationToken cancellationToken) => Task.FromResult(_orgUnits.SingleOrDefault(x => x.Id == orgUnitId));
        public Task<RoleScopeAssignment?> GetRoleScopeAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken) => Task.FromResult(_assignments.SingleOrDefault(x => x.Id == assignmentId));
        public Task<List<RoleScopeAssignment>> ListRoleScopeAssignmentsAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(_assignments.Where(x => x.UserId == userId).ToList());
        public Task<bool> HasOverlappingActiveRoleScopeAssignmentAsync(Guid userId, Guid roleId, Guid orgUnitId, DateTime effectiveFromUtc, DateTime? effectiveToUtc, Guid? excludingAssignmentId, CancellationToken cancellationToken) =>
            Task.FromResult(_assignments.Any(x =>
                x.UserId == userId
                && x.RoleId == roleId
                && x.OrgUnitId == orgUnitId
                && (excludingAssignmentId == null || x.Id != excludingAssignmentId)
                && x.EffectiveFromUtc < (effectiveToUtc ?? DateTime.MaxValue)
                && (x.EffectiveToUtc ?? DateTime.MaxValue) > effectiveFromUtc));
        public Task AddRoleScopeAssignmentAsync(RoleScopeAssignment assignment, CancellationToken cancellationToken) { _assignments.Add(assignment); return Task.CompletedTask; }
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
