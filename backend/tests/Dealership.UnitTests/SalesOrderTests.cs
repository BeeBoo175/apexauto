using Dealership.Domain.Sales;
using Dealership.Domain.Vehicles;

namespace Dealership.UnitTests;

public sealed class SalesOrderTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 30, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid Agent = Guid.NewGuid();
    private static readonly Guid Manager = Guid.NewGuid();

    private static PurchaseSettings Settings(decimal limit = 500m) =>
        new(500m, 0.08m, Guid.NewGuid(), Start.AddDays(-1), limit);

    private static SalesOrder Draft(DealReservation? reservation = null, decimal basePrice = 50000m) =>
        SalesOrder.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Agent, Guid.NewGuid(), basePrice, 90m,
            Settings(), reservation, Start, out _);

    [Fact]
    public void BalanceFollowsTheAgreedItemizationWithSstOnTheProcessingFeeOnly()
    {
        var price = DealPricing.Calculate(new DealTerms(50000m, 90m, 500m, 0.08m, 500m, 300m, 2400m, 500m, 1000m));

        Assert.Equal(40m, price.ServiceTax);
        Assert.Equal(52230m, price.Subtotal);
        Assert.Equal(50730m, price.BalancePayable);
    }

    [Fact]
    public void SstRoundsOnceToSenAndNeverTouchesWarrantyOrOtherLines()
    {
        var price = DealPricing.Calculate(new DealTerms(10000m, 0m, 123.45m, 0.06m, 0m, 0m, 1200m, 0m, 0m));

        Assert.Equal(7.41m, price.ServiceTax);
        Assert.Equal(11330.86m, price.Subtotal);
    }

    [Theory]
    [InlineData(50000, 49000, 1100)]
    [InlineData(1000, 0, 0)]
    public void DiscountsCannotExceedTheBasePriceAndCreditsCannotExceedTheTotal(double basePrice, double promo,
        double negotiated)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DealPricing.Calculate(new DealTerms((decimal)basePrice, 0m,
            0m, 0m, (decimal)promo, (decimal)negotiated, 0m, 0m, 5000m)));
    }

    [Fact]
    public void ZeroBalanceIsAllowedAndNegativeBalanceIsRejected()
    {
        Assert.Equal(0m, DealPricing.Calculate(new DealTerms(1000m, 0m, 0m, 0m, 0m, 0m, 0m, 400m, 600m)).BalancePayable);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DealPricing.Calculate(new DealTerms(1000m, 0m, 0m, 0m, 0m, 0m, 0m, 400m, 600.01m)));
    }

    [Fact]
    public void DraftCarriesTheReservationPromoAndDepositWithoutAnApprovalOrNumber()
    {
        var order = Draft(new DealReservation(Guid.NewGuid(), Guid.NewGuid(), "APEX500", 500m, 500m));

        Assert.Equal((SalesOrderStatus.Draft, "APEX500", 500m, 500m, null), (order.Status, order.PromoCode,
            order.PromoDiscount, order.DepositCredit, order.OrderNumber));
        Assert.Equal(49630m, order.BalancePayable);
    }

    [Fact]
    public void SubmittingWithinTheLimitApprovesAtOnceAndAssignsTheNumber()
    {
        var order = Draft();
        order.Revise(null, 500m, null, Settings(), Agent, Start.AddMinutes(1));

        var submitted = order.Submit(Settings(), "VPO-2026-000001", Agent, Start.AddMinutes(2));

        Assert.Equal((SalesOrderStatus.Approved, "VPO-2026-000001", 500m), (order.Status, order.OrderNumber,
            order.DiscountLimitAtSubmit));
        Assert.Equal((SalesOrderEventKind.Submitted, SalesOrderStatus.Draft, SalesOrderStatus.Approved),
            (submitted.Kind, submitted.FromStatus, submitted.ToStatus));
    }

    [Fact]
    public void ExcessDiscountWaitsForAnotherPersonAndApprovalIsBoundToTheCapturedLimit()
    {
        var order = Draft();
        order.Revise(null, 800m, null, Settings(), Agent, Start.AddMinutes(1));
        order.Submit(Settings(), "VPO-2026-000002", Agent, Start.AddMinutes(2));

        Assert.Equal(SalesOrderStatus.PendingManagerApproval, order.Status);
        Assert.Throws<InvalidOperationException>(() => order.Approve(Agent, Start.AddMinutes(3)));
        Assert.Throws<InvalidOperationException>(() => order.Revise(null, 100m, null, Settings(), Agent, Start.AddMinutes(3)));
        order.Approve(Manager, Start.AddMinutes(4));

        Assert.Equal((SalesOrderStatus.Approved, Manager, 500m), (order.Status, order.DecidedById, order.DiscountLimitAtSubmit));
    }

    [Fact]
    public void RejectionReturnsToDraftKeepingTheNumberAndRequiresAReason()
    {
        var order = Draft();
        order.Revise(null, 800m, null, Settings(), Agent, Start.AddMinutes(1));
        order.Submit(Settings(), "VPO-2026-000003", Agent, Start.AddMinutes(2));

        Assert.Throws<ArgumentException>(() => order.Reject(Manager, " ", Start.AddMinutes(3)));
        var rejected = order.Reject(Manager, "Margin too thin", Start.AddMinutes(3));

        Assert.Equal((SalesOrderStatus.Draft, "VPO-2026-000003", null, "Margin too thin"),
            (order.Status, order.OrderNumber, order.DiscountLimitAtSubmit, rejected.Reason));
        Assert.Throws<ArgumentException>(() => order.Submit(Settings(), "VPO-2026-000004", Agent, Start.AddMinutes(4)));
        order.Submit(Settings(), "VPO-2026-000003", Agent, Start.AddMinutes(4));
        Assert.Equal(SalesOrderStatus.PendingManagerApproval, order.Status);
    }

    [Fact]
    public void EditingAnApprovedDealVoidsTheApprovalAndLaterSettingsAreRecaptured()
    {
        var order = Draft();
        order.Submit(Settings(), "VPO-2026-000005", Agent, Start.AddMinutes(1));
        Assert.Equal(SalesOrderStatus.Approved, order.Status);

        var revised = order.Revise(new DealWarranty(Guid.NewGuid(), "Gold", 2400m), 100m, null,
            new PurchaseSettings(600m, 0.06m, Guid.NewGuid(), Start, 250m), Agent, Start.AddMinutes(2));

        Assert.Equal((SalesOrderStatus.Draft, null, 600m, 0.06m), (order.Status, order.DiscountLimitAtSubmit,
            order.ProcessingFee, order.SstRate));
        Assert.Equal((SalesOrderStatus.Approved, SalesOrderStatus.Draft), (revised.FromStatus!.Value, revised.ToStatus));
    }

    [Fact]
    public void RejectedEditsLeaveTheDealUnchanged()
    {
        var order = Draft(basePrice: 1000m);
        var version = order.Version;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            order.Revise(new DealWarranty(Guid.NewGuid(), "Gold", 100m), 2000m, null, Settings(), Agent, Start.AddMinutes(1)));

        Assert.Equal((version, 0m, null), (order.Version, order.NegotiatedDiscount, order.WarrantyName));
    }

    [Fact]
    public void CancellationIsFinalAndNeedsAReason()
    {
        var order = Draft();

        Assert.Throws<ArgumentException>(() => order.Cancel(Agent, "", Start.AddMinutes(1)));
        var cancelled = order.Cancel(Agent, "Customer withdrew", Start.AddMinutes(1));

        Assert.Equal(SalesOrderStatus.Cancelled, order.Status);
        Assert.Equal("Customer withdrew", cancelled.Reason);
        Assert.Throws<InvalidOperationException>(() => order.Cancel(Agent, "Again", Start.AddMinutes(2)));
        Assert.Throws<InvalidOperationException>(() => order.Submit(Settings(), "VPO-2026-000006", Agent, Start.AddMinutes(2)));
    }

    [Fact]
    public void ActionsCannotGoBackwardsInTime()
    {
        var order = Draft();

        Assert.Throws<ArgumentException>(() => order.Cancel(Agent, "Early", Start.AddTicks(-1)));
    }

    [Fact]
    public void NumbersAreSequentialPerYearAndValidated()
    {
        var counter = new DealNumberCounter(2026);

        Assert.Equal(("VPO-2026-000001", "VPO-2026-000002"), (counter.Next(), counter.Next()));
        Assert.True(SalesOrder.ValidNumber("VPO-2026-000042"));
        Assert.False(SalesOrder.ValidNumber("VPO-26-42"));
        Assert.False(SalesOrder.ValidNumber(null));
    }
}
