using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

internal static class DevelopmentDepositRequestFixtureAssertions
{
    public static async Task VerifyAsync(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var database = services.GetRequiredService<DealershipDbContext>();
        var now = await database.Database.SqlQuery<DateTimeOffset>($"select now() as \"Value\"").SingleAsync();
        var requests = await database.ReservationDepositRequests.AsNoTracking().ToArrayAsync();
        Assert.Equal(DevelopmentDepositRequestScenarios.All.Count, requests.Length);

        foreach (var scenario in DevelopmentDepositRequestScenarios.All)
        {
            var request = Assert.Single(requests, item => item.Id == scenario.Id);
            Assert.Equal(scenario.StaffId, request.BookedByStaffId);
            Assert.Equal(scenario.PublicationId, request.PublicationId);
            Assert.Equal(ReservationDepositRequest.Validity, request.ExpiresAt - request.CreatedAt);
            var age = now - request.CreatedAt;
            var publication = await database.ListingPublications.AsNoTracking()
                .SingleAsync(item => item.Id == scenario.PublicationId);
            var expectedAge = now - DevelopmentDepositRequestFixtures.CreatedAt(scenario, publication, now);
            Assert.InRange(age, expectedAge - TimeSpan.FromMinutes(30), expectedAge + TimeSpan.FromMinutes(30));
            Assert.Equal(scenario.Outcome == DevelopmentDepositRequestOutcome.Pending, request.IsPending(now));
            Assert.Equal(scenario.Outcome == DevelopmentDepositRequestOutcome.Cancelled, request.CancelledAt is not null);
            Assert.Equal(scenario.Outcome == DevelopmentDepositRequestOutcome.Confirmed, request.ConfirmedAt is not null);

            // Only a pending request holds its car; the customer keeps no other hold while one is pending.
            var vehicleHolds = await database.Reservations.CountAsync(item => item.VehicleId == request.VehicleId &&
                item.IsActive);
            var customerHolds = await database.Reservations.CountAsync(item => item.CustomerId == request.CustomerId &&
                item.IsActive);
            Assert.Equal(0, vehicleHolds);
            Assert.Equal(0, customerHolds);
            Assert.Equal(VehicleStatus.Available,
                (await database.Vehicles.SingleAsync(item => item.Id == request.VehicleId)).Status);
        }

        // One car and one customer never carry two pending requests.
        var pending = requests.Where(item => item.IsPending(now)).ToArray();
        Assert.Equal(pending.Length, pending.Select(item => item.VehicleId).Distinct().Count());
        Assert.Equal(pending.Length, pending.Select(item => item.CustomerId).Distinct().Count());

        // The confirmed request made the reservation under its own operation ID, which the customer then released.
        var confirmed = Assert.Single(DevelopmentDepositRequestScenarios.All,
            item => item.Outcome == DevelopmentDepositRequestOutcome.Confirmed);
        var reservation = await database.Reservations.AsNoTracking().SingleAsync(item => item.Id == confirmed.Id);
        Assert.Equal(confirmed.StaffId, reservation.BookedByStaffId);
        Assert.Single(await database.ReservationReleases.Where(item => item.ReservationId == confirmed.Id).ToArrayAsync());

        // Review replays every request without changing it.
        var before = await database.ReservationDepositRequests.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync();
        var fixtures = services.GetRequiredService<DevelopmentDepositRequestFixtures>();
        Assert.All(await fixtures.RunAsync(false, CancellationToken.None),
            report => Assert.False(report.RequiresAttention, $"{report.Fixture}: {report.State}"));
        Assert.All(await fixtures.RunAsync(true, CancellationToken.None),
            report => Assert.False(report.RequiresAttention, $"{report.Fixture}: {report.State}"));
        database.ChangeTracker.Clear();
        var after = await database.ReservationDepositRequests.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync();
        Assert.Equal(before.Select(item => (item.Id, item.CreatedAt, item.ConfirmedAt, item.CancelledAt)),
            after.Select(item => (item.Id, item.CreatedAt, item.ConfirmedAt, item.CancelledAt)));
    }
}
