using Dealership.Application.Accounts;
using Dealership.Application.Vehicles;
using Dealership.Domain.Customers;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Development;

public sealed class DevelopmentDepositRequestFixtures(DealershipDbContext database, ReservationService reservations,
    ReservationCancellationService cancellations, IReservationDepositRequestStore requests)
{
    public async Task<IReadOnlyList<DevelopmentFixtureReport>> RunAsync(bool seed, CancellationToken cancellationToken)
    {
        var now = await database.DatabaseNowAsync(cancellationToken);
        var reports = new List<DevelopmentFixtureReport>();
        foreach (var scenario in DevelopmentDepositRequestScenarios.All)
        {
            reports.Add(await RunOneAsync(seed, scenario, now, cancellationToken));
        }

        return reports;
    }

    private async Task<DevelopmentFixtureReport> RunOneAsync(bool seed, DevelopmentDepositRequestScenario scenario,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        var label = $"Deposit request ({scenario.Listing.Intake.RegistrationPlate}; {scenario.Name})";
        var customer = await database.CustomerProfiles.AsNoTracking()
            .SingleOrDefaultAsync(item => item.RegistrationKey == scenario.CustomerKey, cancellationToken);
        var publication = await database.ListingPublications.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == scenario.PublicationId, cancellationToken);
        if (customer is null || publication is null)
        {
            return new(label, "customer/listing missing; preserved", true);
        }

        var existing = await requests.FindAsync(scenario.Id, cancellationToken);
        if (existing is not null && (existing.CustomerId != customer.UserId ||
            existing.VehicleId != publication.VehicleId || existing.PublicationId != publication.Id))
        {
            return new(label, "request drift; preserved", true);
        }

        var created = false;
        if (existing is null && seed)
        {
            // Backdated so the timers of different requests show different stages; the car is held from that moment.
            var request = new ReservationDepositRequest(scenario.Id, customer, publication, null, scenario.StaffId,
                CreatedAt(scenario, publication, now));
            database.ReservationDepositRequests.Add(request);
            await database.SaveChangesAsync(cancellationToken);
            database.ChangeTracker.Clear();
            created = true;
            if (!await SettleAsync(scenario, request, customer, now, cancellationToken))
            {
                return new(label, "outcome unavailable; preserved", true);
            }

            existing = await requests.FindAsync(scenario.Id, cancellationToken);
        }

        return existing is null
            ? new(label, "request missing", true)
            : Judge(label, scenario, existing, created ? "created" : "matches", now);
    }

    // A listing published less than the scenario's age ago cannot be requested earlier than its publication.
    public static DateTimeOffset CreatedAt(DevelopmentDepositRequestScenario scenario, ListingPublication publication,
        DateTimeOffset now) => DateTimeOffset.Compare(now - scenario.Age, publication.RecordedAt) < 0
            ? publication.RecordedAt
            : now - scenario.Age;

    private async Task<bool> SettleAsync(DevelopmentDepositRequestScenario scenario, ReservationDepositRequest request,
        CustomerProfile customer, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (scenario.Outcome == DevelopmentDepositRequestOutcome.Cancelled)
        {
            return await requests.CancelAsync(request.Id, request.CreatedAt + TimeSpan.FromHours(2), cancellationToken);
        }

        if (scenario.Outcome != DevelopmentDepositRequestOutcome.Confirmed)
        {
            return true;
        }

        // The customer's click makes the ordinary reservation under the request's own operation ID, booked by the
        // staff member; the customer then released it, so only the request and its history remain.
        var staff = new AssistingStaff(scenario.StaffId, AccountRoles.SalesAgent, request.BranchId);
        var reserved = await reservations.ReserveAsync(customer.UserId, request.VehicleId,
            new(request.Id, request.PublicationId, request.PromoId), cancellationToken, staff);
        if (reserved.Status != ReservationStatus.Succeeded)
        {
            return false;
        }

        await requests.MarkConfirmedAsync(request.Id, now, cancellationToken);
        var released = await cancellations.CancelAsync(customer.UserId, request.Id,
            new(scenario.Listing.OperationId("deposit-request-release"), null), cancellationToken);
        return released.Status == ReservationStatus.Succeeded;
    }

    private static DevelopmentFixtureReport Judge(string label, DevelopmentDepositRequestScenario scenario,
        ReservationDepositRequest request, string state, DateTimeOffset now)
    {
        var matches = scenario.Outcome switch
        {
            DevelopmentDepositRequestOutcome.Cancelled => request.CancelledAt is not null,
            DevelopmentDepositRequestOutcome.Confirmed => request.ConfirmedAt is not null,
            _ => request.ConfirmedAt is null && request.CancelledAt is null
        };
        if (!matches)
        {
            return new(label, "outcome changed; preserved", true);
        }

        // A pending request that ran out its 24 hours no longer holds the car; never renew it.
        var lapsed = scenario.Outcome == DevelopmentDepositRequestOutcome.Pending && !request.IsPending(now);
        return new(label, lapsed ? "lapsed; preserved" : state, lapsed);
    }
}
