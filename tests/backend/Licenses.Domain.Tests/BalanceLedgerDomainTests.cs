using Licenses.Domain.LeaveManagement;

namespace Licenses.Domain.Tests;

public sealed class BalanceLedgerDomainTests
{
    [Theory]
    [InlineData(BalanceLedgerEntryType.Grant, 2.5, 2.5, 0)]
    [InlineData(BalanceLedgerEntryType.Reserve, 2.5, -2.5, 2.5)]
    [InlineData(BalanceLedgerEntryType.Release, 2.5, 2.5, -2.5)]
    [InlineData(BalanceLedgerEntryType.Consume, 2.5, 0, -2.5)]
    [InlineData(BalanceLedgerEntryType.Refund, 2.5, 2.5, 0)]
    [InlineData(BalanceLedgerEntryType.Expire, 2.5, -2.5, 0)]
    [InlineData(BalanceLedgerEntryType.Adjustment, -1.5, -1.5, 0)]
    public void CalculatesSemanticDeltas(BalanceLedgerEntryType type, double amount, double available, double reserved)
    {
        var deltas = BalanceLedgerEntry.GetDeltas(type, (decimal)amount);
        Assert.Equal((decimal)available, deltas.AvailableDelta);
        Assert.Equal((decimal)reserved, deltas.ReservedDelta);
    }

    [Fact]
    public void RejectsZeroEffectAndInvalidAmounts()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BalanceLedgerEntry.GetDeltas(BalanceLedgerEntryType.Grant, 0m));
        Assert.Throws<ArgumentOutOfRangeException>(() => BalanceLedgerEntry.GetDeltas(BalanceLedgerEntryType.Reserve, -1m));
        Assert.Throws<ArgumentOutOfRangeException>(() => BalanceLedgerEntry.GetDeltas(BalanceLedgerEntryType.Adjustment, 0m));
        Assert.Throws<ArgumentException>(() => BalanceLedgerEntry.Create(Guid.NewGuid(), Guid.NewGuid(), BalanceLedgerEntryType.Adjustment, 0m, 0m, "Reason", null, DateTime.UtcNow));
    }

    [Fact]
    public void BalanceAccountRequiresUserAndBucket()
    {
        Assert.Throws<ArgumentException>(() => BalanceAccount.Create(Guid.Empty, Guid.NewGuid(), DateTime.UtcNow));
        Assert.Throws<ArgumentException>(() => BalanceAccount.Create(Guid.NewGuid(), Guid.Empty, DateTime.UtcNow));
    }
}
