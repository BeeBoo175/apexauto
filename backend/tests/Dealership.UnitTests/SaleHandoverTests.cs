using Dealership.Domain.Sales;
using Dealership.Domain.Vehicles;

namespace Dealership.UnitTests;

public sealed class SaleHandoverTests
{
    [Theory]
    [InlineData(999, 2, true, true, true)]
    [InlineData(1000, -1, true, true, true)]
    [InlineData(1000, 2, false, true, true)]
    [InlineData(1000, 2, true, false, true)]
    [InlineData(1000, 2, true, true, false)]
    public void InvalidPhysicalEvidenceLeavesSettledSaleUnchanged(int odometer, int keys,
        bool acknowledged, bool transferred, bool insured)
    {
        var (sale, agent, now) = Settled();
        var before = SaleSnapshot.Of(sale);
        Assert.Throws<ArgumentException>(() => sale.Deliver(Guid.NewGuid(),
            new(odometer, keys, acknowledged, transferred, insured), 1000, agent, now));
        Assert.Equal(before, SaleSnapshot.Of(sale));
    }

    [Fact]
    public void DeliveryAcceptsRecordedMileageAndCapturesActorTimeWithoutChangingSettlement()
    {
        var (sale, agent, now) = Settled();
        var settled = SaleSnapshot.Of(sale);
        var evidence = new SaleHandoverEvidence(1000, 0, true, true, true);
        Assert.Throws<InvalidOperationException>(() => sale.Deliver(Guid.NewGuid(), evidence, 1000, Guid.NewGuid(), now));
        var receipt = sale.Deliver(Guid.NewGuid(), evidence, 1000, agent, now);
        Assert.Equal(SalesOrderEventKind.Delivered, receipt.Kind);
        Assert.Equal(agent, receipt.ActorId);
        Assert.Equal(now, receipt.RecordedAt);
        Assert.Equal(now, sale.DeliveredAt);
        Assert.Equal(settled.PaymentEvidence, receipt.Snapshot.PaymentEvidence);
        Assert.Equal(settled.SettledAt, receipt.Snapshot.SettledAt);
        Assert.True(sale.IsClosed);
        Assert.Throws<InvalidOperationException>(() => sale.Deliver(Guid.NewGuid(), evidence, 1000, agent, now));
        Assert.Throws<InvalidOperationException>(() => sale.Reassign(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), now));
    }

    private static (SalesOrder Sale, Guid Agent, DateTimeOffset Now) Settled()
    {
        var reservation = ReservationTests.Create();
        var sale = SalesOrder.Open(reservation, Guid.NewGuid(), out _);
        var agent = Guid.NewGuid();
        var now = reservation.CreatedAt.AddHours(1);
        sale.Claim(Guid.NewGuid(), agent, agent, now);
        var settings = new PurchaseSettings(500m, 0.08m, Guid.NewGuid(), now);
        sale.Submit(Guid.NewGuid(), settings, "VPO-2026-123456", agent, now);
        var payment = new SalePaymentEvidence(SalePaymentMethod.Cash, sale.BalancePayable, "CASH 47893",
            new DateOnly(2026, 11, 2), null, 0m, null, false, false);
        sale.Settle(Guid.NewGuid(), payment, null, agent, now);
        return (sale, agent, now.AddMinutes(1));
    }
}
