using Dealership.Application.Sales;
using Dealership.Application.Vehicles;
using Dealership.Domain.Sales;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Development;

public sealed class DevelopmentReservationFixtures(DealershipDbContext database, ReservationService reservations,
    ReservationCancellationService cancellations, SaleTermsService sales)
{
    public async Task<IReadOnlyList<DevelopmentFixtureReport>> RunAsync(bool seed, CancellationToken cancellationToken)
    {
        var now = await database.DatabaseNowAsync(cancellationToken);
        var reports = new List<DevelopmentFixtureReport>();
        foreach (var scenario in DevelopmentReservationScenarios.All)
        {
            reports.Add(await RunOneAsync(seed, scenario, now, cancellationToken));
        }

        return reports;
    }

    private async Task<DevelopmentFixtureReport> RunOneAsync(bool seed, DevelopmentReservationScenario scenario,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        var label = $"Reservation ({scenario.Listing.Intake.RegistrationPlate}; {scenario.Index + 1})";
        var customerId = await database.CustomerProfiles.Where(item => item.RegistrationKey == scenario.RegistrationKey)
            .Select(item => (Guid?)item.UserId).SingleOrDefaultAsync(cancellationToken);
        var vehicleId = await database.Vehicles.Where(item => item.IntakeAppointmentId == scenario.Listing.Id)
            .Select(item => (Guid?)item.Id).SingleOrDefaultAsync(cancellationToken);
        if (customerId is null || vehicleId is null)
        {
            return new(label, "customer/vehicle missing; preserved", true);
        }

        var existing = await database.Reservations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == scenario.Id, cancellationToken);
        if (existing is not null && (existing.CustomerId != customerId || existing.VehicleId != vehicleId ||
            existing.PublicationId != scenario.PublicationId || existing.PromoId != scenario.PromoId))
        {
            return new(label, "confirmation drift; preserved", true);
        }

        var created = false;
        if (existing is null && seed)
        {
            var result = await reservations.ReserveAsync(customerId.Value, vehicleId.Value,
                new(scenario.Id, scenario.PublicationId, scenario.PromoId), cancellationToken);
            if (result.Status != ReservationStatus.Succeeded)
            {
                return new(label, "hold unavailable; preserved", true);
            }

            existing = result.Reservation;
            created = true;
        }

        if (existing is null)
        {
            return new(label, "confirmation missing", true);
        }

        var release = await database.ReservationReleases.AsNoTracking()
            .SingleOrDefaultAsync(item => item.ReservationId == existing.Id, cancellationToken);
        var state = created ? "created" : "matches";
        if (scenario.KeepActive)
        {
            // Never replay an old confirmation to renew its deadline or replace a later release.
            var active = existing.IsActive && release is null;
            if (!active)
            {
                return new(label, "released; preserved", true);
            }

            var expired = await database.SalesOrders.AnyAsync(item => item.Id == existing.Id &&
                item.Status == SalesOrderStatus.Open && item.DueAt <= now, cancellationToken);
            return new(label, expired ? "expired; preserved" : state, expired);
        }

        if (release is null && seed)
        {
            // Customers cancel online; staff cancel the hold's sale, which records the same refund.
            var cancelled = scenario.Reason is null
                ? (await cancellations.CancelAsync(customerId.Value, existing.Id, new(scenario.ReleaseId, null),
                    cancellationToken)).Status == ReservationStatus.Succeeded
                : (await sales.CancelAsync(scenario.CancellationActor(customerId.Value), existing.Id,
                    new(scenario.ReleaseId, 1, scenario.Reason), cancellationToken)).Status == SaleResultStatus.Succeeded;
            if (!cancelled)
            {
                return new(label, "cancellation unavailable; preserved", true);
            }

            release = await database.ReservationReleases.AsNoTracking()
                .SingleOrDefaultAsync(item => item.ReservationId == existing.Id, cancellationToken);
        }

        var matches = release is not null &&
            release.ActorId == scenario.CancellationActor(customerId.Value) && release.Reason == scenario.Reason &&
            release.Kind == (scenario.Reason is null ? ReservationReleaseKind.CustomerCancelled : ReservationReleaseKind.StaffCancelled);
        return new(label, matches ? state : "release missing/changed; preserved", !matches);
    }
}
