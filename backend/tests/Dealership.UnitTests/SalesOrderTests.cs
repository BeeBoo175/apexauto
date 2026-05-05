using Dealership.Domain.Sales;
using Dealership.Domain.Vehicles;

namespace Dealership.UnitTests;

public sealed class SalesOrderTests
{
    private static readonly DateTimeOffset Reserved = new(2026, 10, 26, 7, 0, 0, TimeSpan.Zero);
    private static readonly Guid Agent = Guid.NewGuid();
    private static readonly Guid OtherAgent = Guid.NewGuid();
    private static readonly Guid Manager = Guid.NewGuid();

    private static PurchaseSettings Settings(decimal limit = 500m) =>
        new(500m, 0.08m, Guid.NewGuid(), Reserved.AddDays(-1), limit);

    private static SalesOrder Opened(out Reservation reservation)
    {
        reservation = ReservationTests.Create();
        return SalesOrder.Open(reservation, Guid.NewGuid(), out _);
    }

    private static SalesOrder Claimed(DateTimeOffset? at = null)
    {
        var order = Opened(out _);
        order.Claim(Guid.NewGuid(), Agent, Agent, at ?? Reserved.AddHours(1));
        return order;
    }

    private static DealTermsInput Terms(decimal discount, DealWarranty? warranty = null, DealTradeIn? tradeIn = null) =>
        new(warranty, discount, tradeIn);

    [Fact]
    public void BalanceFollowsTheAgreedItemizationWithSstOnTheProcessingFeeOnly()
    {
        var price = DealPricing.Calculate(new DealTerms(50000m, 90m, 500m, 0.08m, 500m, 300m, 2400m, 500m, 1000m));

        Assert.Equal(40m, price.ServiceTax);
        Assert.Equal(52230m, price.Subtotal);
        Assert.Equal(50730m, price.BalancePayable);
    }

    [Fact]
    public void ZeroBalanceIsAllowedAndNegativeBalanceIsRejected()
    {
        Assert.Equal(0m, DealPricing.Calculate(new DealTerms(1000m, 0m, 0m, 0m, 0m, 0m, 0m, 400m, 600m)).BalancePayable);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DealPricing.Calculate(new DealTerms(1000m, 0m, 0m, 0m, 0m, 0m, 0m, 400m, 600.01m)));
    }

    [Fact]
    public void OpeningCarriesTheReservationTermsDeadlineAndIdentity()
    {
        var reservation = ReservationTests.Create();

        var order = SalesOrder.Open(reservation, Guid.NewGuid(), out var created);

        Assert.Equal((reservation.Id, SalesOrderStatus.Open, reservation.ExpiresAt, 1L),
            (order.Id, order.Status, order.DueAt, order.Version));
        Assert.Equal((60000m, 90m, 1000m, 500m, "RAYA1000"), (order.BasePrice, order.RoadTax, order.PromoDiscount,
            order.DepositCredit, order.PromoCode));
        Assert.Equal(reservation.Price.Total - reservation.Deposit, order.BalancePayable);
        Assert.Equal((SalesOrderEventKind.Created, 1L, reservation.CustomerId), (created.Kind, created.Sequence,
            created.ActorId));
        Assert.Equal(SalesOrderStatus.Open, created.Snapshot.Status);
    }

    [Fact]
    public void ClaimingPausesTheTimerAndReleasingResumesItWithTheReservationTerms()
    {
        var order = Opened(out var reservation);
        var claimedAt = Reserved.AddHours(30);

        var claimed = order.Claim(Guid.NewGuid(), Agent, Agent, claimedAt);
        order.SaveTerms(Guid.NewGuid(), Terms(300m), Settings(), Agent, claimedAt.AddHours(1));

        Assert.Equal((SalesOrderEventKind.Claimed, Agent), (claimed.Kind, claimed.AgentId));
        Assert.Equal((null, 18L * 3600), (order.DueAt, order.PausedSeconds));
        Assert.False(order.HasExpired(Reserved.AddDays(30)));

        var releasedAt = claimedAt.AddDays(10);
        var released = order.Release(Guid.NewGuid(), Agent, releasedAt);

        Assert.Equal((SalesOrderStatus.Open, releasedAt.AddHours(18), null), (order.Status, order.DueAt, order.AgentId));
        Assert.Equal((SalesOrderEventKind.Released, Agent, 0m), (released.Kind, released.PreviousAgentId,
            order.NegotiatedDiscount));
        Assert.Contains(released.Changes!, change => change is { Field: DealChange.NegotiatedDiscount, Before: "300.00" });
        Assert.Equal(reservation.Price.Total - reservation.Deposit, order.BalancePayable);
        Assert.True(order.HasExpired(releasedAt.AddHours(18)));
    }

    [Fact]
    public void AManagerAssignsAndReassignsWithoutTouchingTermsOrPausedTime()
    {
        var order = Opened(out _);

        var assigned = order.Claim(Guid.NewGuid(), Agent, Manager, Reserved.AddHours(1));
        order.SaveTerms(Guid.NewGuid(), Terms(200m), Settings(), Agent, Reserved.AddHours(2));
        var moved = order.Reassign(Guid.NewGuid(), OtherAgent, Manager, Reserved.AddHours(5));

        Assert.Equal((SalesOrderEventKind.Assigned, Manager), (assigned.Kind, assigned.ActorId));
        Assert.Equal((SalesOrderEventKind.Reassigned, OtherAgent, Agent), (moved.Kind, moved.AgentId,
            moved.PreviousAgentId));
        Assert.Equal((SalesOrderStatus.Handling, 200m, 47L * 3600), (order.Status, order.NegotiatedDiscount,
            order.PausedSeconds));
        Assert.Throws<InvalidOperationException>(() => order.Reassign(Guid.NewGuid(), OtherAgent, Manager,
            Reserved.AddHours(6)));
    }

    [Fact]
    public void SubmittingWithinTheLimitApprovesAtOnceAndAboveItWaitsForAManager()
    {
        var within = Claimed();
        within.SaveTerms(Guid.NewGuid(), Terms(500m), Settings(), Agent, Reserved.AddHours(2));
        within.Submit(Guid.NewGuid(), Settings(), "VPO-2026-000001", Agent, Reserved.AddHours(3));

        var above = Claimed();
        above.SaveTerms(Guid.NewGuid(), Terms(500.01m), Settings(), Agent, Reserved.AddHours(2));
        var submitted = above.Submit(Guid.NewGuid(), Settings(), "VPO-2026-000002", Agent, Reserved.AddHours(3));

        Assert.Equal((SalesOrderStatus.Approved, 500m), (within.Status, within.DiscountLimitAtSubmit));
        Assert.Equal((SalesOrderStatus.PendingApproval, "VPO-2026-000002"), (above.Status, above.OrderNumber));
        Assert.Equal((SalesOrderStatus.Handling, SalesOrderStatus.PendingApproval), (submitted.FromStatus!.Value,
            submitted.ToStatus));
    }

    [Fact]
    public void RejectionReturnsTheTermsToTheAgentAndResubmissionKeepsTheNumber()
    {
        var order = Claimed();
        order.SaveTerms(Guid.NewGuid(), Terms(900m), Settings(), Agent, Reserved.AddHours(2));
        order.Submit(Guid.NewGuid(), Settings(), "VPO-2026-000003", Agent, Reserved.AddHours(3));

        Assert.Throws<InvalidOperationException>(() => order.Reject(Guid.NewGuid(), Agent, "Too low", Reserved.AddHours(4)));
        var rejected = order.Reject(Guid.NewGuid(), Manager, "Margin too thin", Reserved.AddHours(4));

        Assert.Equal((SalesOrderStatus.Handling, 900m, "Margin too thin"), (order.Status, order.NegotiatedDiscount,
            rejected.Reason));
        Assert.Throws<ArgumentException>(() => order.Submit(Guid.NewGuid(), Settings(), "VPO-2026-000009", Agent,
            Reserved.AddHours(5)));
        order.Submit(Guid.NewGuid(), Settings(1000m), "VPO-2026-000003", Agent, Reserved.AddHours(5));
        Assert.Equal(SalesOrderStatus.Approved, order.Status);
    }

    [Fact]
    public void SavingTermsOnAnApprovedSaleWithdrawsTheApproval()
    {
        var order = Claimed();
        order.SaveTerms(Guid.NewGuid(), Terms(900m), Settings(), Agent, Reserved.AddHours(2));
        order.Submit(Guid.NewGuid(), Settings(), "VPO-2026-000004", Agent, Reserved.AddHours(3));
        order.Approve(Guid.NewGuid(), Manager, Reserved.AddHours(4));

        order.SaveTerms(Guid.NewGuid(), Terms(800m), Settings(), Agent, Reserved.AddHours(5));

        Assert.Equal((SalesOrderStatus.Handling, null, null), (order.Status, order.DecidedById, order.DiscountLimitAtSubmit));
        Assert.Throws<InvalidOperationException>(() => order.Reassign(Guid.NewGuid(), Agent, Manager, Reserved.AddHours(6)));
    }

    [Fact]
    public void TheApprovingManagerCannotBecomeTheAgent()
    {
        var order = Claimed();
        order.SaveTerms(Guid.NewGuid(), Terms(900m), Settings(), Agent, Reserved.AddHours(2));
        order.Submit(Guid.NewGuid(), Settings(), "VPO-2026-000005", Agent, Reserved.AddHours(3));
        order.Approve(Guid.NewGuid(), Manager, Reserved.AddHours(4));

        Assert.Throws<InvalidOperationException>(() => order.Reassign(Guid.NewGuid(), Manager, Manager,
            Reserved.AddHours(5)));
    }

    [Fact]
    public void UnchangedTermsAndInvalidCommandsLeaveTheSaleAsItWas()
    {
        var order = Claimed();

        Assert.Throws<InvalidOperationException>(() => order.SaveTerms(Guid.NewGuid(), Terms(0m), Settings(), Agent,
            Reserved.AddHours(2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => order.SaveTerms(Guid.NewGuid(), Terms(70000m), Settings(),
            Agent, Reserved.AddHours(2)));
        Assert.Throws<ArgumentException>(() => order.SaveTerms(Guid.Empty, Terms(100m), Settings(), Agent,
            Reserved.AddHours(2)));
        Assert.Throws<ArgumentException>(() => order.SaveTerms(Guid.NewGuid(), Terms(100m), Settings(), Agent,
            Reserved));
        Assert.Equal((2L, 0m), (order.Version, order.NegotiatedDiscount));
    }

    [Fact]
    public void TermChangesRecordReadableBeforeAndAfterValues()
    {
        var order = Claimed();
        var warranty = new DealWarranty(Guid.NewGuid(), "ApexShield Gold", 2400m);
        var tradeIn = new DealTradeIn(Guid.NewGuid(), "WXY 1234 · 2018 Perodua Myvi", 15000m);

        var saved = order.SaveTerms(Guid.NewGuid(), Terms(300m, warranty, tradeIn), Settings(), Agent,
            Reserved.AddHours(2));

        Assert.Equal(
        [
            new DealChange(DealChange.NegotiatedDiscount, "0.00", "300.00"),
            new DealChange(DealChange.Warranty, null, "ApexShield Gold"),
            new DealChange(DealChange.TradeIn, null, "WXY 1234 · 2018 Perodua Myvi"),
            new DealChange(DealChange.TradeInAllowance, "0.00", "15000.00")
        ], saved.Changes);
        Assert.Equal((3L, 300m, "ApexShield Gold"), (saved.Snapshot.Version, saved.Snapshot.NegotiatedDiscount,
            saved.Snapshot.WarrantyName));
    }

    [Fact]
    public void CancellationClosesTheSaleWithItsRefundAndCustomersCancelOnlyOpenSales()
    {
        var open = Opened(out var reservation);
        var handled = Claimed();
        var releaseId = Guid.NewGuid();

        Assert.Throws<InvalidOperationException>(() => handled.CancelByCustomer(Guid.NewGuid(), handled.CustomerId,
            releaseId, Reserved.AddHours(2)));
        var customer = open.CancelByCustomer(Guid.NewGuid(), reservation.CustomerId, releaseId, Reserved.AddHours(2));
        var staff = handled.Cancel(Guid.NewGuid(), Manager, "Buyer withdrew", Guid.NewGuid(), Reserved.AddHours(2));

        Assert.Equal((SalesOrderStatus.Cancelled, releaseId), (open.Status, customer.ReleaseId));
        Assert.Equal((SalesOrderStatus.Cancelled, Agent, null), (handled.Status, staff.PreviousAgentId, handled.AgentId));
        Assert.Throws<InvalidOperationException>(() => handled.Cancel(Guid.NewGuid(), Manager, "Again", Guid.NewGuid(),
            Reserved.AddHours(3)));
        Assert.Throws<ArgumentException>(() => Claimed().Cancel(Guid.NewGuid(), Manager, " ", Guid.NewGuid(),
            Reserved.AddHours(3)));
    }

    [Fact]
    public void AnExpiredOpenSaleCannotBeClaimed()
    {
        var order = Opened(out var reservation);

        Assert.Throws<InvalidOperationException>(() => order.Claim(Guid.NewGuid(), Agent, Agent, reservation.ExpiresAt));
        Assert.Throws<InvalidOperationException>(() => order.Claim(Guid.NewGuid(), Agent, Agent,
            reservation.ExpiresAt.AddMilliseconds(-500)));
    }
}
