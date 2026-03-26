using System.Text.Json;
using Dealership.Application.Accounts;
using Dealership.Application.Vehicles;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

internal static class DevelopmentReservationFixtureAssertions
{
    public static async Task<Reservation> InterruptedAsync(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var hold = Assert.Single(await database.Reservations.AsNoTracking().ToArrayAsync());
        Assert.True(hold.IsActive);
        Assert.Empty(await database.ReservationReleases.ToArrayAsync());
        Assert.Equal(1, await database.ReservationMailOutbox.CountAsync());
        return hold;
    }

    public static async Task VerifyAsync(IServiceProvider provider, Reservation interrupted)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var database = services.GetRequiredService<DealershipDbContext>();
        Assert.Equal(156, await database.Reservations.CountAsync());
        Assert.Equal(153, await database.ReservationReleases.CountAsync());
        Assert.Equal(156, await database.ReservationMailOutbox.CountAsync());
        Assert.Equal(3, await database.Reservations.CountAsync(item => item.IsActive));
        var retained = await database.Reservations.AsNoTracking().SingleAsync(item => item.Id == interrupted.Id);
        Assert.Equal(ReservationReceipt.From(interrupted), ReservationReceipt.From(retained));
        Assert.False(retained.IsActive);
        Assert.All(await database.ReservationReleases.ToArrayAsync(), release => Assert.Equal(500m, release.Refund));
        Assert.All(await database.Reservations.ToArrayAsync(), hold =>
            Assert.Equal(TimeSpan.FromHours(48), hold.ExpiresAt - hold.CreatedAt));

        var reads = services.GetRequiredService<IReservationReadStore>();
        foreach (var branch in DevelopmentFixtureManifest.Branches)
        {
            var scenario = DevelopmentReservationScenarios.All.Single(item =>
                item.KeepActive && item.Listing.Intake.BranchId == branch.Id);
            var customerId = await database.CustomerProfiles.Where(item => item.RegistrationKey == scenario.RegistrationKey)
                .Select(item => item.UserId).SingleAsync();
            var expected = await database.Reservations.Where(item => item.CustomerId == customerId)
                .OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id).Select(item => item.Id).ToArrayAsync();
            Assert.Equal(52, expected.Length);
            var actual = new List<Guid>();
            for (var page = 1; page <= 3; page++)
            {
                var result = await reads.AccountAsync(customerId, page, CancellationToken.None);
                Assert.Equal(ReservationStatus.Succeeded, result.Status);
                Assert.Equal(52, result.History!.Total);
                Assert.Equal(page == 3 ? 2 : 25, result.History.Items.Count);
                Assert.Equal(scenario.Id, result.History.Active!.Receipt.Id);
                actual.AddRange(result.History.Items.Select(item => item.Receipt.Id));
            }

            Assert.Equal(expected, actual);
            var manager = DevelopmentFixtureManifest.Staff.Single(item =>
                item.BranchId == branch.Id && item.Role == AccountRoles.BranchManager);
            await VerifyStaffPagesAsync(reads, manager.Id, new(PageSize: 25), expected);
            var otherBranch = DevelopmentFixtureManifest.Branches.First(item => item.Id != branch.Id).Id;
            Assert.Equal(ReservationStatus.Forbidden, (await reads.StaffAsync(manager.Id,
                new(BranchId: otherBranch), CancellationToken.None)).Status);
            var publicRead = services.GetRequiredService<IPublicListingStore>();
            var held = await publicRead.DetailAsync(retained.VehicleId, CancellationToken.None);
            Assert.Equal(ListingStatus.Succeeded, held.Status);
            var active = await database.Reservations.SingleAsync(item => item.Id == scenario.Id);
            Assert.Equal(ListingStatus.NotFound, (await publicRead.DetailAsync(active.VehicleId, CancellationToken.None)).Status);
        }

        var general = DevelopmentFixtureManifest.Staff.Single(item => item.Role == AccountRoles.GeneralManager);
        var allIds = await database.Reservations.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id)
            .Select(item => item.Id).ToArrayAsync();
        await VerifyStaffPagesAsync(reads, general.Id, new(PageSize: 50), allIds);
        var sales = DevelopmentFixtureManifest.Staff.First(item => item.Role == AccountRoles.SalesAgent);
        Assert.Equal(ReservationStatus.Forbidden, (await reads.StaffAsync(sales.Id, new(), CancellationToken.None)).Status);
        var before = await SnapshotAsync(database);
        Assert.All(await services.GetRequiredService<DevelopmentReservationFixtures>().RunAsync(false, CancellationToken.None),
            report => Assert.False(report.RequiresAttention));
        Assert.Equal(before, await SnapshotAsync(database));
    }

    public static async Task VerifyPreservationAsync(IServiceProvider provider, DevelopmentFixtureTarget target,
        RegistrationDatabase fixture)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var database = services.GetRequiredService<DealershipDbContext>();
        var active = await database.Reservations.AsNoTracking().FirstAsync(item => item.IsActive);
        var request = new ReservationCancellationRequest(Guid.NewGuid(), null);
        var cancelled = await services.GetRequiredService<ReservationCancellationService>()
            .CancelAsync(active.CustomerId, active.Id, request, CancellationToken.None);
        Assert.Equal(ReservationStatus.Succeeded, cancelled.Status);
        var before = await SnapshotAsync(database);
        var runner = services.GetRequiredService<DevelopmentFixtureRunner>();
        Assert.Contains(await runner.ReviewAsync(target, CancellationToken.None), report =>
            report.Fixture.StartsWith("Reservation (", StringComparison.Ordinal) && report.State == "released; preserved");
        Assert.Equal(before, await SnapshotAsync(database));
        Assert.Contains(await runner.SeedAsync(target, CancellationToken.None), report =>
            report.Fixture.StartsWith("Reservation (", StringComparison.Ordinal) && report.State == "released; preserved");
        Assert.Equal(before, await SnapshotAsync(database));
        var replay = await services.GetRequiredService<ReservationService>().ReserveAsync(active.CustomerId, active.VehicleId,
            new(active.Id, active.PublicationId, active.PromoId), CancellationToken.None);
        Assert.Equal(ReservationStatus.Succeeded, replay.Status);
        Assert.Equal(ReservationReceipt.From(active), ReservationReceipt.From(replay.Reservation!));
        Assert.Equal(before, await SnapshotAsync(database));
        await VerifyExpiredReportAsync(services, fixture);
    }

    private static async Task VerifyExpiredReportAsync(IServiceProvider services, RegistrationDatabase fixture)
    {
        await using var clock = new ReservationTestClock(fixture);
        var expiresAt = await clock.Maintenance.Reservations.Where(item => item.IsActive).MaxAsync(item => item.ExpiresAt);
        await clock.InitializeAsync(expiresAt);
        await using var database = clock.CreateContext();
        var mail = services.GetRequiredService<MailDeliverySignal>();
        var expiry = services.GetRequiredService<ReservationExpirySignal>();
        var reservations = new ReservationService(new ReservationStore(database, mail, expiry));
        var cancellations = new ReservationCancellationService(new ReservationCancellationStore(database));
        var fixtures = new DevelopmentReservationFixtures(database, reservations, cancellations);
        var before = await SnapshotAsync(database);
        foreach (var seed in new[] { false, true })
        {
            var reports = await fixtures.RunAsync(seed, CancellationToken.None);
            Assert.Equal(2, reports.Count(item => item.State == "expired; preserved" && item.RequiresAttention));
            Assert.Equal(before, await SnapshotAsync(database));
        }
    }

    private static async Task VerifyStaffPagesAsync(IReservationReadStore reads, Guid actorId,
        ReservationReadFilter filter, Guid[] expected)
    {
        var actual = new List<Guid>();
        for (var page = 1; actual.Count < expected.Length; page++)
        {
            var result = await reads.StaffAsync(actorId, filter with { Page = page }, CancellationToken.None);
            Assert.Equal(ReservationStatus.Succeeded, result.Status);
            Assert.Equal(expected.Length, result.History!.Total);
            Assert.Equal(Math.Min(filter.PageSize, expected.Length - actual.Count), result.History.Items.Count);
            actual.AddRange(result.History.Items.Select(item => item.Receipt.Id));
        }

        Assert.Equal(expected, actual);
        Assert.Empty((await reads.StaffAsync(actorId, filter with { Page = int.MaxValue }, CancellationToken.None)).History!.Items);
    }

    private static async Task<string> SnapshotAsync(DealershipDbContext database) => JsonSerializer.Serialize(new
    {
        Holds = await database.Reservations.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync(),
        Releases = await database.ReservationReleases.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync(),
        Mail = await database.ReservationMailOutbox.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync()
    });
}
