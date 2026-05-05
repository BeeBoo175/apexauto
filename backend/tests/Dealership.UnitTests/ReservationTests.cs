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
    }

    [Fact]
    public void ReleaseRecordsTheFullDepositAndRequiresTheAuthoritativeCause()
    {
        var reservation = Create();
        var expired = new ReservationRelease(Guid.NewGuid(), reservation, reservation.ExpiresAt, ReservationReleaseKind.Expired,
            null, null, reservation.ExpiresAt);
        Assert.Equal(reservation.Deposit, expired.Refund);
        Assert.Throws<ArgumentException>(() => new ReservationRelease(Guid.NewGuid(), reservation, reservation.ExpiresAt,
            ReservationReleaseKind.Expired, null, null, reservation.ExpiresAt.AddTicks(-1)));
        Assert.Throws<ArgumentException>(() => new ReservationRelease(Guid.NewGuid(), reservation, reservation.ExpiresAt,
            ReservationReleaseKind.CustomerCancelled, reservation.CustomerId, null, reservation.ExpiresAt));
        Assert.Throws<ArgumentException>(() => new ReservationRelease(Guid.NewGuid(), reservation, reservation.ExpiresAt,
            ReservationReleaseKind.CustomerCancelled, Guid.NewGuid(), null, Now));
        Assert.Throws<ArgumentException>(() => new ReservationRelease(Guid.NewGuid(), reservation, reservation.ExpiresAt,
            ReservationReleaseKind.StaffCancelled, Guid.NewGuid(), " ", Now));
        Assert.Throws<ArgumentException>(() => new ReservationRelease(Guid.NewGuid(), reservation, reservation.ExpiresAt,
            ReservationReleaseKind.Withdrawn, reservation.CustomerId, null, Now));
        var staff = new ReservationRelease(Guid.NewGuid(), reservation, reservation.ExpiresAt, ReservationReleaseKind.StaffCancelled,
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
    public void AHandledSaleHasNoRunningDeadlineSoItsHoldCannotExpire()
    {
        var reservation = Create();

        Assert.Throws<ArgumentException>(() => new ReservationRelease(Guid.NewGuid(), reservation, null,
            ReservationReleaseKind.Expired, null, null, reservation.ExpiresAt.AddDays(3)));
    }

    internal static Reservation Create(DateTimeOffset? promoExpiry = null, bool promoActive = true,
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
