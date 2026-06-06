using Dealership.Domain.Sales;
using Dealership.Application.Sales;
using Dealership.Infrastructure.Sales;
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
        Assert.Equal(57 + DevelopmentSaleVolumeManifest.All.Count, await database.Reservations.CountAsync());
        Assert.Equal(49, await database.ReservationReleases.CountAsync());
        Assert.Equal(57 + DevelopmentSaleVolumeManifest.All.Count, await database.ReservationMailOutbox.CountAsync());
        Assert.Equal(4 + DevelopmentSaleVolumeManifest.All.Count, await database.Reservations.CountAsync(item => item.IsActive));
        // The demo sales work the branches' open holds, which stay active while they are handled.
        foreach (var sale in DevelopmentSaleScenarios.All)
        {
            var persisted = await database.SalesOrders.AsNoTracking().SingleAsync(item => item.Id == sale.ReservationId);
            Assert.Equal(sale.Target, persisted.Status);
            Assert.Equal(sale.FinalVersion, persisted.Version);
            if (sale.Target is SalesOrderStatus.Settled or SalesOrderStatus.Delivered)
            {
                Assert.NotNull(persisted.PaymentEvidence);
                Assert.Equal(sale.PaymentMethod, persisted.PaymentEvidence.Method);
                Assert.Equal(persisted.PaymentEvidence, persisted.PaymentEvidence.Normalize(persisted.BalancePayable));
                Assert.Equal(sale.TradeInAppointmentId, persisted.TradeInAppointmentId);
                Assert.Equal(sale.TradeInAppointmentId is not null, persisted.AcquiredVehicleId is not null);
                if (sale.TradeInAppointmentId is { } appointmentId)
                {
                    var checkIn = await database.AppointmentCheckIns.SingleAsync(item => item.AppointmentId == appointmentId);
                    Assert.Equal(checkIn.VehicleId, persisted.AcquiredVehicleId);
                    var appointment = await database.IntakeAppointments.SingleAsync(item => item.Id == appointmentId);
                    Assert.Equal(persisted.CustomerId, appointment.OwnerId);
                    Assert.InRange(persisted.TradeInAllowance, 0.01m, appointment.Valuation.Maximum);
                    Assert.Equal(0m, persisted.BalancePayable);
                    Assert.Equal(0m, persisted.NegotiatedDiscount);
                    Assert.True(persisted.TradeInAllowance > persisted.DepositCredit);
                    Assert.False((await database.InspectionDrafts.SingleAsync(item => item.VehicleId == checkIn.VehicleId)).IsSigned);
                    Assert.Equal(VehicleStatus.PendingInspection,
                        (await database.Vehicles.SingleAsync(item => item.Id == checkIn.VehicleId)).Status);
                }
            }
        }
        var retained = await database.Reservations.AsNoTracking().SingleAsync(item => item.Id == interrupted.Id);
        Assert.Equal(ReservationReceipt.From(interrupted), ReservationReceipt.From(retained));
        Assert.False(retained.IsActive);
        Assert.All(await database.ReservationReleases.ToArrayAsync(), release => Assert.Equal(500m, release.Refund));
        Assert.All(await database.Reservations.ToArrayAsync(), hold =>
            Assert.Equal(TimeSpan.FromHours(48), hold.ExpiresAt - hold.CreatedAt));

        var reads = services.GetRequiredService<IReservationReadStore>();
        var buyerKey = DevelopmentFixtureManifest.Customers[0].Registration.RegistrationKey;
        var buyerId = await database.CustomerProfiles.Where(item => item.RegistrationKey == buyerKey)
            .Select(item => item.UserId).SingleAsync();
        var buyerHistory = new List<Guid>();
        for (var page = 1; buyerHistory.Count < 15; page++)
        {
            var result = await reads.AccountAsync(buyerId, page, CancellationToken.None);
            Assert.Equal(ReservationStatus.Succeeded, result.Status);
            Assert.Equal(15, result.History!.Total);
            Assert.Null(result.History.Active);
            Assert.Equal(ReservationHistory.PageSize, result.History.PageSize);
            Assert.Equal(Math.Min(ReservationHistory.PageSize, 15 - buyerHistory.Count), result.History.Items.Count);
            buyerHistory.AddRange(result.History.Items.Select(item => item.Receipt.Id));
        }
        Assert.Equal(await database.Reservations.Where(item => item.CustomerId == buyerId)
            .OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id).Select(item => item.Id).ToArrayAsync(), buyerHistory);

        foreach (var branch in DevelopmentFixtureManifest.Branches)
        {
            var scenario = DevelopmentReservationScenarios.All.Single(item =>
                item.Index == 51 && item.Listing.Intake.BranchId == branch.Id);
            var customerId = await database.CustomerProfiles.Where(item => item.RegistrationKey == scenario.RegistrationKey)
                .Select(item => item.UserId).SingleAsync();
            var expected = await database.Reservations.Where(item => item.CustomerId == customerId)
                .OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id).Select(item => item.Id).ToArrayAsync();
            Assert.Equal(branch == DevelopmentFixtureManifest.Branches[1] ? 14 : 13, expected.Length);
            var actual = new List<Guid>();
            for (var page = 1; actual.Count < expected.Length; page++)
            {
                var result = await reads.AccountAsync(customerId, page, CancellationToken.None);
                Assert.Equal(ReservationStatus.Succeeded, result.Status);
                Assert.Equal(expected.Length, result.History!.Total);
                Assert.Equal(ReservationHistory.PageSize, result.History.PageSize);
                Assert.Equal(Math.Min(ReservationHistory.PageSize, expected.Length - actual.Count), result.History.Items.Count);
                var settled = DevelopmentSaleScenarios.All.Any(item => item.ReservationId == scenario.Id &&
                    item.Target is SalesOrderStatus.Settled or SalesOrderStatus.Delivered);
                if (settled)
                {
                    Assert.Null(result.History.Active);
                }
                else
                {
                    Assert.Equal(scenario.Id, result.History.Active!.Receipt.Id);
                }
                actual.AddRange(result.History.Items.Select(item => item.Receipt.Id));
            }

            Assert.Equal(expected, actual);
            var publicRead = services.GetRequiredService<IPublicListingStore>();
            var held = await publicRead.DetailAsync(retained.VehicleId, CancellationToken.None);
            Assert.Equal(ListingStatus.Succeeded, held.Status);
            var active = await database.Reservations.SingleAsync(item => item.Id == scenario.Id);
            Assert.Equal(ListingStatus.NotFound, (await publicRead.DetailAsync(active.VehicleId, CancellationToken.None)).Status);
        }

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
        // Customers cancel online only while nobody handles the sale, so an open hold is chosen.
        var active = await database.Reservations.AsNoTracking().FirstAsync(item => item.IsActive &&
            database.SalesOrders.Any(sale => sale.Id == item.Id && sale.Status == SalesOrderStatus.Open));
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
        await VerifySalePreservationAsync(services, database, runner, target);
        await VerifyExpiredReportAsync(services, fixture);
    }

    private static async Task VerifySalePreservationAsync(IServiceProvider services, DealershipDbContext database,
        DevelopmentFixtureRunner runner, DevelopmentFixtureTarget target)
    {
        var scenario = DevelopmentSaleScenarios.All.Single(item => item.Target == SalesOrderStatus.Handling);
        var saved = await services.GetRequiredService<SaleTermsService>().SaveTermsAsync(scenario.AgentId,
            scenario.ReservationId, new(Guid.NewGuid(), scenario.FinalVersion, null, scenario.Discount + 1m, null, 0m),
            CancellationToken.None);
        Assert.Equal(SaleResultStatus.Succeeded, saved.Status);
        var before = await SnapshotAsync(database);
        foreach (var seed in new[] { false, true })
        {
            var reports = seed
                ? await runner.SeedAsync(target, CancellationToken.None)
                : await runner.ReviewAsync(target, CancellationToken.None);
            Assert.Contains(reports, report => report.Fixture.StartsWith("Sale (", StringComparison.Ordinal) &&
                report.State == "sale version changed elsewhere; preserved" && report.RequiresAttention);
            Assert.Equal(before, await SnapshotAsync(database));
        }
    }

    private static async Task VerifyExpiredReportAsync(IServiceProvider services, RegistrationDatabase fixture)
    {
        await using var clock = new ReservationTestClock(fixture);
        var expiresAt = await clock.Maintenance.Reservations.Where(item => item.IsActive).MaxAsync(item => item.ExpiresAt);
        await clock.InitializeAsync(expiresAt);
        // Holds an agent handles keep their paused time, so only the open ones expire at the original deadline.
        var open = await clock.Maintenance.SalesOrders.CountAsync(item => item.Status == SalesOrderStatus.Open);
        await using var database = clock.CreateContext();
        var mail = services.GetRequiredService<MailDeliverySignal>();
        var expiry = services.GetRequiredService<ReservationExpirySignal>();
        var reservations = new ReservationService(new ReservationStore(database, mail, expiry));
        var cancellations = new ReservationCancellationService(new ReservationCancellationStore(database));
        var sales = new SaleTermsService(new SaleCommandStore(database, new Dealership.Infrastructure.Mail.MailDeliverySignal()));
        var fixtures = new DevelopmentReservationFixtures(database, reservations, cancellations, sales,
            services.GetRequiredService<DevelopmentSaleFixtures>());
        var before = await SnapshotAsync(database);
        foreach (var seed in new[] { false, true })
        {
            var reports = await fixtures.RunAsync(seed, CancellationToken.None);
            Assert.Equal(open, reports.Count(item => item.State == "expired; preserved" && item.RequiresAttention));
            Assert.Equal(before, await SnapshotAsync(database));
        }
    }

    private static async Task<string> SnapshotAsync(DealershipDbContext database) => JsonSerializer.Serialize(new
    {
        Holds = await database.Reservations.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync(),
        Releases = await database.ReservationReleases.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync(),
        Mail = await database.ReservationMailOutbox.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync(),
        Sales = await database.SalesOrders.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync(),
        Events = await database.SalesOrderEvents.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync(),
        Documents = await database.SaleDocuments.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync(),
        Corrections = await database.SaleDocumentCorrections.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync(),
        Deliveries = await database.SaleDeliveryMailOutbox.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync()
    });
}
