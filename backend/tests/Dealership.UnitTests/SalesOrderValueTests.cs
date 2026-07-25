using Dealership.Domain.Sales;
using Dealership.Domain.Vehicles;

namespace Dealership.UnitTests;

public sealed class SalesOrderValueTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(15000)]
    [InlineData(61230)]
    public void ProjectedSubtotalRetainsAgreedValueRegardlessOfCredits(int tradeInAllowance)
    {
        var reservation = ReservationTests.Create();
        var order = SalesOrder.Open(reservation, Guid.NewGuid(), out _);
        var agent = Guid.NewGuid();
        var now = reservation.CreatedAt.AddHours(1);
        order.Claim(Guid.NewGuid(), agent, agent, now);
        var settings = new PurchaseSettings(500m, 0.08m, Guid.NewGuid(), now);
        var tradeIn = tradeInAllowance == 0 ? null :
            new SaleTradeIn(Guid.NewGuid(), "WXY 1234 · 2018 Perodua Myvi", tradeInAllowance);
        order.SaveTerms(Guid.NewGuid(),
            new(new SaleWarranty(Guid.NewGuid(), "ApexShield Gold", 2400m), 300m, tradeIn),
            settings, agent, now.AddMinutes(1));
        var price = SalePricing.Calculate(new SaleTerms(order.BasePrice, order.RoadTax, order.ProcessingFee,
            order.SstRate, order.PromoDiscount, order.NegotiatedDiscount, order.WarrantyPrice,
            order.DepositCredit, order.TradeInAllowance));

        var projected = SalesOrderValue.Projection.Compile()(order);

        Assert.Equal(61730m, price.Subtotal);
        Assert.Equal(price.Subtotal, projected.Subtotal);
        Assert.Equal(price.BalancePayable, order.BalancePayable);
    }
}
