using Dealership.Domain.Common;
using Dealership.Domain.Sales;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Development;

// Builds released holds of dated listings at their historical times inside the snapshot transaction, mirroring the
// canonical confirmation and cancellation commands, which can only stamp the database clock.
public sealed class DevelopmentReservationHistory(DealershipDbContext database)
{
    private static readonly TimeSpan HoldDuration = TimeSpan.FromHours(3);

    public static bool Covers(DevelopmentReservationScenario scenario) =>
        !scenario.KeepActive && DevelopmentWorkshopHistory.Covers(scenario.Listing);

    public static IReadOnlyList<DevelopmentReservationScenario> For(DevelopmentInspectionScenario listing) =>
        [.. DevelopmentReservationScenarios.All.Where(item => item.Listing.Id == listing.Id && Covers(item))];

    // Seller holds run in the first half of the gap to the snapshot, buyer holds in the second, so a vehicle's
    // successive holds never overlap.
    public static (DateTimeOffset HeldAt, DateTimeOffset ReleasedAt) Times(DevelopmentReservationScenario scenario,
        DateTimeOffset publishedAt, DateTimeOffset now)
    {
        var gap = now - publishedAt;
        var buyer = scenario.Index >= DevelopmentReservationScenarios.FirstBuyerIndex;
        var start = buyer ? 3 : 1;
        var heldAt = Instant(publishedAt, gap, start, 10);
        // A release inside the 48-hour hold keeps the receipt a cancellation rather than an expiry.
        var released = heldAt + TimeSpan.FromTicks(Math.Min(HoldDuration.Ticks, (gap / 10).Ticks));
        return (heldAt, UtcTime.TruncateToMicroseconds(released));
    }

    public async Task AddReleasedHoldAsync(DevelopmentReservationScenario scenario, DateTimeOffset publishedAt,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        var (heldAt, releasedAt) = Times(scenario, publishedAt, now);
        var customerId = await AddHoldAsync(scenario, heldAt, cancellationToken);
        await ReleaseAsync(scenario, customerId, releasedAt, cancellationToken);
    }

    // Confirms the hold and opens its sale, as the canonical confirmation does.
    public async Task<Guid> AddHoldAsync(DevelopmentReservationScenario scenario, DateTimeOffset heldAt,
        CancellationToken cancellationToken)
    {
        var profile = await database.CustomerProfiles.AsNoTracking()
            .SingleAsync(item => item.RegistrationKey == scenario.RegistrationKey, cancellationToken);
        var customer = await database.Users.AsNoTracking()
            .SingleAsync(item => item.Id == profile.UserId, cancellationToken);
        var publication = await database.ListingPublications.AsNoTracking()
            .SingleAsync(item => item.Id == scenario.PublicationId, cancellationToken);
        var revision = await database.ListingRevisions.AsNoTracking()
            .SingleAsync(item => item.Id == publication.RevisionId, cancellationToken);
        var settings = await database.PurchaseSettings.AsNoTracking().SingleAsync(cancellationToken);
        var promo = scenario.PromoId is null ? null : await database.PromoCodes.AsNoTracking()
            .SingleAsync(item => item.Id == scenario.PromoId, cancellationToken);

        var reservation = new Reservation(scenario.Id, profile, customer.Email!, publication, revision, settings, promo,
            heldAt);
        reservation.AssignNumber(await YearlyNumberAllocator.NextAsync<ReservationNumberCounter>(database, heldAt.Year,
            cancellationToken));
        database.Reservations.Add(reservation);
        var sale = SalesOrder.Open(reservation, Guid.NewGuid(), out var created);
        database.SalesOrders.Add(sale);
        database.SalesOrderEvents.Add(created);
        database.ReservationMailOutbox.Add(new ReservationMail(reservation));
        await database.SaveChangesAsync(cancellationToken);
        database.ChangeTracker.Clear();
        return customer.Id;
    }

    private async Task ReleaseAsync(DevelopmentReservationScenario scenario, Guid customerId, DateTimeOffset releasedAt,
        CancellationToken cancellationToken)
    {
        var reservation = await database.Reservations.AsNoTracking()
            .SingleAsync(item => item.Id == scenario.Id, cancellationToken);
        var sale = await database.SalesOrders.SingleAsync(item => item.Id == scenario.Id, cancellationToken);
        if (scenario.Reason is null)
        {
            var release = new ReservationRelease(scenario.ReleaseId, reservation, sale.DueAt,
                ReservationReleaseKind.CustomerCancelled, customerId, null, releasedAt);
            database.ReservationReleases.Add(release);
            await database.SaveChangesAsync(cancellationToken);
            database.SalesOrderEvents.Add(sale.CancelByCustomer(Guid.NewGuid(), customerId, release.Id, releasedAt));
        }
        else
        {
            var actorId = scenario.CancellationActor(customerId);
            var release = new ReservationRelease(Guid.NewGuid(), reservation, sale.DueAt,
                ReservationReleaseKind.StaffCancelled, actorId, scenario.Reason, releasedAt);
            database.ReservationReleases.Add(release);
            await database.SaveChangesAsync(cancellationToken);
            database.SalesOrderEvents.Add(sale.Cancel(scenario.ReleaseId, actorId, scenario.Reason, release.Id,
                releasedAt));
        }

        await database.SaveChangesAsync(cancellationToken);
        database.ChangeTracker.Clear();
    }

    public static DateTimeOffset Instant(DateTimeOffset from, TimeSpan gap, int step, int steps) =>
        UtcTime.TruncateToMicroseconds(from + gap * step / steps);
}
