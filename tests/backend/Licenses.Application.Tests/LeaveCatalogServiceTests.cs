using Licenses.Application.LeaveManagement;
using Licenses.Domain.LeaveManagement;

namespace Licenses.Application.Tests;

public sealed class LeaveCatalogServiceTests
{
    private readonly FakeLeaveCatalogRepository _repository = new();
    private readonly LeaveCatalogService _service;

    public LeaveCatalogServiceTests()
    {
        _service = new LeaveCatalogService(_repository, TimeProvider.System);
    }

    [Fact]
    public async Task LeaveTypeCanBeCreatedAndCodeIsNormalized()
    {
        var created = await _service.CreateLeaveTypeAsync(new(" vacation ", "Vacation", null, null, 10), CancellationToken.None);

        Assert.Equal("VACATION", created.Code);
        Assert.Equal("Vacation", created.Name);
        Assert.True(created.IsActive);
    }

    [Fact]
    public async Task DuplicateLeaveTypeCodeIsRejected()
    {
        await _service.CreateLeaveTypeAsync(new("VACATION", "Vacation", null, null, 0), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateLeaveTypeAsync(new("vacation", "Other", null, null, 0), CancellationToken.None));

        Assert.Contains("unique", ex.Message);
    }

    [Fact]
    public async Task LeaveTypeCodeCannotNormallyBeChangedButNameAndActivationCanChange()
    {
        var created = await _service.CreateLeaveTypeAsync(new("VACATION", "Vacation", null, null, 0), CancellationToken.None);

        var updated = await _service.UpdateLeaveTypeAsync(created.Id, new("Annual Vacation", "Updated", false, 5), CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal("VACATION", updated.Code);
        Assert.Equal("Annual Vacation", updated.Name);
        Assert.False(updated.IsActive);
        Assert.Equal(5, updated.SortOrder);
        Assert.Single(await _service.ListLeaveTypesAsync(null, CancellationToken.None));
    }

    [Fact]
    public async Task NegativeLeaveTypeSortOrderIsRejected()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _service.CreateLeaveTypeAsync(new("VACATION", "Vacation", null, null, -1), CancellationToken.None));
    }

    [Fact]
    public async Task BalanceBucketCanBeCreatedAndCodeIsNormalized()
    {
        var created = await _service.CreateBalanceBucketAsync(new(" vacation_days ", "Vacation Days", null, "day", null), CancellationToken.None);

        Assert.Equal("VACATION_DAYS", created.Code);
        Assert.Equal("DAY", created.Unit);
        Assert.True(created.IsActive);
    }

    [Fact]
    public async Task DuplicateBalanceBucketCodeIsRejected()
    {
        await _service.CreateBalanceBucketAsync(new("VACATION_DAYS", "Vacation Days", null, "DAY", null), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateBalanceBucketAsync(new("vacation_days", "Other", null, "DAY", null), CancellationToken.None));

        Assert.Contains("unique", ex.Message);
    }

    [Fact]
    public async Task BalanceBucketCodeCannotNormallyBeChangedButCanBeDeactivated()
    {
        var created = await _service.CreateBalanceBucketAsync(new("VACATION_DAYS", "Vacation Days", null, "DAY", null), CancellationToken.None);

        var updated = await _service.UpdateBalanceBucketAsync(created.Id, new("Annual Vacation Days", null, "DAY", false), CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal("VACATION_DAYS", updated.Code);
        Assert.Equal("Annual Vacation Days", updated.Name);
        Assert.False(updated.IsActive);
        Assert.Single(await _service.ListBalanceBucketsAsync(null, CancellationToken.None));
    }

    [Fact]
    public async Task BalanceBucketInvalidUnitIsRejected()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateBalanceBucketAsync(new("VACATION_DAYS", "Vacation Days", null, "HOUR", null), CancellationToken.None));
    }

    private sealed class FakeLeaveCatalogRepository : ILeaveCatalogRepository
    {
        private readonly List<LeaveType> _leaveTypes = [];
        private readonly List<BalanceBucket> _balanceBuckets = [];

        public Task<List<LeaveType>> ListLeaveTypesAsync(bool? isActive, CancellationToken cancellationToken) => Task.FromResult(_leaveTypes.Where(x => isActive is null || x.IsActive == isActive).OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToList());
        public Task<LeaveType?> GetLeaveTypeAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(_leaveTypes.SingleOrDefault(x => x.Id == id));
        public Task<bool> LeaveTypeCodeExistsAsync(string code, Guid? excludingId, CancellationToken cancellationToken) => Task.FromResult(_leaveTypes.Any(x => x.Code == code && (excludingId is null || x.Id != excludingId)));
        public Task AddLeaveTypeAsync(LeaveType leaveType, CancellationToken cancellationToken) { _leaveTypes.Add(leaveType); return Task.CompletedTask; }
        public Task<List<BalanceBucket>> ListBalanceBucketsAsync(bool? isActive, CancellationToken cancellationToken) => Task.FromResult(_balanceBuckets.Where(x => isActive is null || x.IsActive == isActive).OrderBy(x => x.Name).ToList());
        public Task<BalanceBucket?> GetBalanceBucketAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(_balanceBuckets.SingleOrDefault(x => x.Id == id));
        public Task<bool> BalanceBucketCodeExistsAsync(string code, Guid? excludingId, CancellationToken cancellationToken) => Task.FromResult(_balanceBuckets.Any(x => x.Code == code && (excludingId is null || x.Id != excludingId)));
        public Task AddBalanceBucketAsync(BalanceBucket balanceBucket, CancellationToken cancellationToken) { _balanceBuckets.Add(balanceBucket); return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

