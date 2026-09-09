using Licenses.Domain.LeaveManagement;

namespace Licenses.Domain.Tests;

public sealed class LeaveCatalogDomainTests
{
    [Fact]
    public void LeaveType_CanBeCreatedAndNormalizesCode()
    {
        var leaveType = LeaveType.Create(" vacation ", "Vacation", null, 10, true, DateTime.UtcNow);

        Assert.NotEqual(Guid.Empty, leaveType.Id);
        Assert.Equal("VACATION", leaveType.Code);
        Assert.True(leaveType.IsActive);
    }

    [Fact]
    public void LeaveType_RequiresCodeAndName()
    {
        Assert.Throws<ArgumentException>(() => LeaveType.Create(" ", "Vacation", null, 0, true, DateTime.UtcNow));
        Assert.Throws<ArgumentException>(() => LeaveType.Create("VACATION", " ", null, 0, true, DateTime.UtcNow));
    }

    [Fact]
    public void LeaveType_RejectsNegativeSortOrder()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LeaveType.Create("VACATION", "Vacation", null, -1, true, DateTime.UtcNow));
    }

    [Fact]
    public void BalanceBucket_CanBeCreatedAndNormalizesCode()
    {
        var bucket = BalanceBucket.Create(" vacation_days ", "Vacation Days", null, BalanceBucketUnit.Day, true, DateTime.UtcNow);

        Assert.NotEqual(Guid.Empty, bucket.Id);
        Assert.Equal("VACATION_DAYS", bucket.Code);
        Assert.Equal(BalanceBucketUnit.Day, bucket.Unit);
        Assert.True(bucket.IsActive);
    }

    [Fact]
    public void BalanceBucket_RequiresCodeAndName()
    {
        Assert.Throws<ArgumentException>(() => BalanceBucket.Create(" ", "Vacation Days", null, BalanceBucketUnit.Day, true, DateTime.UtcNow));
        Assert.Throws<ArgumentException>(() => BalanceBucket.Create("VACATION_DAYS", " ", null, BalanceBucketUnit.Day, true, DateTime.UtcNow));
    }

    [Fact]
    public void BalanceBucket_RejectsInvalidUnit()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BalanceBucket.Create("VACATION_DAYS", "Vacation Days", null, (BalanceBucketUnit)999, true, DateTime.UtcNow));
    }
}
