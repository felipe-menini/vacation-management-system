using Licenses.Application.Organization;
using Licenses.Domain.Identity;
using Licenses.Domain.Organization;

namespace Licenses.Application.Tests;

public sealed class OrganizationServiceTests
{
    private readonly OrganizationService _service;
    private readonly FakeOrganizationRepository _repository = new();

    public OrganizationServiceTests()
    {
        _service = new OrganizationService(_repository, TimeProvider.System);
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
        public Task<User?> GetUserAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(_users.SingleOrDefault(x => x.Id == id));
        public Task<bool> ExternalIdentityIdExistsAsync(string externalIdentityId, Guid? excludingId, CancellationToken cancellationToken) => Task.FromResult(_users.Any(x => x.ExternalIdentityId == externalIdentityId && (excludingId == null || x.Id != excludingId)));
        public Task AddUserAsync(User user, CancellationToken cancellationToken) { _users.Add(user); return Task.CompletedTask; }
        public Task<List<UserOrgAssignment>> ListAssignmentsAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(_assignments.Where(x => x.UserId == userId).ToList());
        public Task<bool> HasOverlappingPrimaryAssignmentAsync(Guid userId, DateTime effectiveFromUtc, DateTime? effectiveToUtc, CancellationToken cancellationToken) =>
            Task.FromResult(_assignments.Any(x =>
                x.UserId == userId
                && x.IsPrimary
                && x.EffectiveFromUtc < (effectiveToUtc ?? DateTime.MaxValue)
                && (x.EffectiveToUtc ?? DateTime.MaxValue) > effectiveFromUtc));
        public Task AddAssignmentAsync(UserOrgAssignment assignment, CancellationToken cancellationToken) { _assignments.Add(assignment); return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
