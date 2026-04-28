using Dealership.Domain.Customers;
using Dealership.Domain.Vehicles;

namespace Dealership.UnitTests;

public sealed class ReservationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 7, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ConfirmationRetainsItemizedTermsAndFixedDepositAndDeadline()
    {
        var reservation = Create();

        Assert.Equal(new PurchasePrice(60000m, 500m, 90m, 40m, 1000m, 59630m), reservation.Price);
        Assert.Equal("RAYA1000", reservation.PromoCode);
        Assert.Equal(500m, reservation.Deposit);
        Assert.Equal(Now.AddHours(48), reservation.ExpiresAt);
        Assert.False(reservation.HasExpired(reservation.ExpiresAt.AddTicks(-1)));
        Assert.True(reservation.HasExpired(reservation.ExpiresAt));
        Assert.True(reservation.HasExpired(reservation.ExpiresAt.AddDays(1)));
    }

    [Fact]
    public void ReleaseRecordsTheFullDepositAndRequiresTheAuthoritativeCause()
    {
        var reservation = Create();
        var expired = new ReservationRelease(Guid.NewGuid(), reservation, ReservationReleaseKind.Expired,
            null, null, reservation.ExpiresAt);
        Assert.Equal(reservation.Deposit, expired.Refund);
        Assert.Throws<ArgumentException>(() => new ReservationRelease(Guid.NewGuid(), reservation,
            ReservationReleaseKind.Expired, null, null, reservation.ExpiresAt.AddTicks(-1)));
        Assert.Throws<ArgumentException>(() => new ReservationRelease(Guid.NewGuid(), reservation,
            ReservationReleaseKind.CustomerCancelled, reservation.CustomerId, null, reservation.ExpiresAt));
        Assert.Throws<ArgumentException>(() => new ReservationRelease(Guid.NewGuid(), reservation,
            ReservationReleaseKind.CustomerCancelled, Guid.NewGuid(), null, Now));
        Assert.Throws<ArgumentException>(() => new ReservationRelease(Guid.NewGuid(), reservation,
            ReservationReleaseKind.StaffCancelled, Guid.NewGuid(), " ", Now));
        Assert.Throws<ArgumentException>(() => new ReservationRelease(Guid.NewGuid(), reservation,
            ReservationReleaseKind.Withdrawn, reservation.CustomerId, null, Now));
        var staff = new ReservationRelease(Guid.NewGuid(), reservation, ReservationReleaseKind.StaffCancelled,
            Guid.NewGuid(), "  Buyer requested assistance  ", Now);
        Assert.Equal("Buyer requested assistance", staff.Reason);
    }

    [Fact]
    public void ConfirmationRejectsUnavailablePromoAndUnrelatedPublication()
    {
        Assert.Throws<ArgumentException>(() => Create(promoExpiry: Now));
        Assert.Throws<ArgumentException>(() => Create(promoActive: false));
        Assert.Throws<ArgumentException>(() => Create(promoDiscount: 60001m));
        Assert.Throws<ArgumentException>(() => Create(unrelated: true));
    }

    [Fact]
    public void ClaimFreezesTheTimerAndReleaseResumesFromTheRemainingTime()
    {
        var reservation = Create();
        var agent = Guid.NewGuid();
        var claimedAt = Now.AddHours(30);

        var claimed = reservation.Claim(agent, agent, 1, claimedAt);

        Assert.Equal((ReservationEventKind.Claimed, agent, null), (claimed.Kind, claimed.AgentId, claimed.PreviousAgentId));
        Assert.Equal((agent, claimedAt, TimeSpan.FromHours(18)), (reservation.ClaimedById, reservation.ClaimedAt,
            reservation.FrozenRemaining));
        // A claimed hold no longer expires, however long the agent takes.
        Assert.False(reservation.HasExpired(Now.AddDays(30)));

        var releasedAt = claimedAt.AddDays(10);
        var released = reservation.Release(agent, 2, releasedAt);

        Assert.Equal((ReservationEventKind.Released, null, agent), (released.Kind, released.AgentId, released.PreviousAgentId));
        Assert.Equal(releasedAt.AddHours(18), reservation.ExpiresAt);
        Assert.Null(reservation.ClaimedById);
        Assert.Null(reservation.FrozenRemaining);
        Assert.False(reservation.HasExpired(releasedAt.AddHours(18).AddTicks(-1)));
        Assert.True(reservation.HasExpired(releasedAt.AddHours(18)));
    }

    [Fact]
    public void AManagerAssignsAndReassignsAHoldWithoutTouchingTheFrozenTime()
    {
        var reservation = Create();
        var manager = Guid.NewGuid();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        var assigned = reservation.Claim(first, manager, 1, Now.AddHours(1));
        var moved = reservation.Reassign(second, manager, 2, Now.AddHours(5));

        Assert.Equal((ReservationEventKind.Assigned, manager), (assigned.Kind, assigned.ActorId));
        Assert.Equal((ReservationEventKind.Reassigned, second, first), (moved.Kind, moved.AgentId, moved.PreviousAgentId));
        Assert.Equal((second, TimeSpan.FromHours(47)), (reservation.ClaimedById, reservation.FrozenRemaining));
        Assert.Equal(Now.AddHours(5), reservation.ClaimedAt);
        Assert.Throws<InvalidOperationException>(() => reservation.Reassign(second, manager, 3, Now.AddHours(6)));
    }

    [Fact]
    public void ClaimsRequireAnOpenUnexpiredHoldAndAValidCommand()
    {
        var agent = Guid.NewGuid();
        var reservation = Create();

        Assert.Throws<InvalidOperationException>(() => reservation.Release(agent, 1, Now.AddHours(1)));
        Assert.Throws<InvalidOperationException>(() => reservation.Reassign(agent, agent, 1, Now.AddHours(1)));
        Assert.Throws<InvalidOperationException>(() => reservation.Claim(agent, agent, 1, reservation.ExpiresAt));
        Assert.Throws<ArgumentException>(() => reservation.Claim(Guid.Empty, agent, 1, Now.AddHours(1)));
        Assert.Throws<ArgumentException>(() => reservation.Claim(agent, agent, 0, Now.AddHours(1)));
        Assert.Throws<ArgumentException>(() => reservation.Claim(agent, agent, 1, Now.AddHours(1).ToOffset(TimeSpan.FromHours(8))));
        reservation.Claim(agent, agent, 1, Now.AddHours(1));
        Assert.Throws<InvalidOperationException>(() => reservation.Claim(Guid.NewGuid(), Guid.NewGuid(), 2, Now.AddHours(2)));
        Assert.Throws<InvalidOperationException>(() => reservation.Release(agent, 2, Now));
    }

    private static Reservation Create(DateTimeOffset? promoExpiry = null, bool promoActive = true,
        decimal promoDiscount = 1000m, bool unrelated = false)
    {
        var customer = new CustomerProfile(Guid.NewGuid(), Guid.NewGuid(), "Nur Aina Abdullah", "940215105422",
            "0123456789", "18 Jalan Melati, Petaling Jaya");
        var manager = Guid.NewGuid();
        var listing = new ListingRevision(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), manager, 1,
            60000m, 90m, VehicleTransmission.Automatic, VehicleFuel.Petrol, "Inspected hatchback", Now);
        var publication = new ListingPublication(Guid.NewGuid(), listing, Guid.NewGuid(), manager, Now);
        if (unrelated)
        {
            listing = new(Guid.NewGuid(), listing.VehicleId, listing.BranchId, manager, 2, 62000m, 90m,
                VehicleTransmission.Automatic, VehicleFuel.Petrol, "Revised hatchback", Now);
        }

        return new(Guid.NewGuid(), customer, "aina@example.invalid", publication, listing,
            new(500m, 0.08m, manager, Now),
            new(Guid.NewGuid(), "RAYA1000", promoDiscount, promoExpiry ?? Now.AddDays(1), promoActive), Now);
    }
}
