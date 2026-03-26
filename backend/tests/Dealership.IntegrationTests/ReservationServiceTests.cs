using Dealership.Application.Accounts;
using Dealership.Application.Vehicles;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Dealership.IntegrationTests.ReservationTestData;

namespace Dealership.IntegrationTests;

public sealed class ReservationServiceTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task ProductionCommandSerializesHoldsAndReplaysRetainedTermsAfterRestartAndRelease()
    {
        using var data = await CheckInTestData.CreateAsync(fixture);
        using var scope = data.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var manager = await ProvisionAsync(services, data.BranchId, AccountRoles.BranchManager);
        var technician = await ProvisionAsync(services, data.BranchId, AccountRoles.WorkshopTechnician);
        var first = await CreateListingAsync(data, services, manager, technician, 0);
        var second = await CreateListingAsync(data, services, manager, technician, 1);
        await using var database = NewContext();
        var promo = new PromoCode(Guid.NewGuid(), "RAYA1000", 1000m, DateTimeOffset.UtcNow.AddDays(1), true);
        database.PurchaseSettings.Add(new(500m, 0.08m, manager, DateTimeOffset.UtcNow));
        database.PromoCodes.Add(promo);
        await database.SaveChangesAsync();
        var vehicleId = first.Revision.VehicleId;
        var quote = await services.GetRequiredService<PurchaseQuoteService>()
            .QuoteAsync(vehicleId, " RAYA1000 ", CancellationToken.None);
        Assert.Equal(promo.Id, quote.Quote!.PromoId);
        var request = new ReservationRequest(Guid.NewGuid(), first.Publication.Id, quote.Quote.PromoId);

        Assert.Equal(ReservationStatus.Invalid, (await ReserveAsync(data, data.OwnerId, vehicleId,
            request with { OperationId = Guid.Empty })).Status);
        Assert.Equal(ReservationStatus.Forbidden, (await ReserveAsync(data, data.StaffId, vehicleId, request)).Status);
        await database.Users.Where(item => item.Id == data.OwnerId)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.EmailConfirmed, false));
        Assert.Equal(ReservationStatus.Forbidden, (await ReserveAsync(data, data.OwnerId, vehicleId, request)).Status);
        await database.Users.Where(item => item.Id == data.OwnerId)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.EmailConfirmed, true));
        Assert.Equal(ReservationStatus.NotFound, (await ReserveAsync(data, data.OwnerId, Guid.NewGuid(), request)).Status);
        Assert.Equal(ReservationStatus.Conflict, (await ReserveAsync(data, data.OwnerId, vehicleId,
            request with { PublicationId = second.Publication.Id })).Status);
        Assert.Equal(ReservationStatus.PromoUnavailable, (await ReserveAsync(data, data.OwnerId, vehicleId,
            request with { PromoId = Guid.NewGuid() })).Status);

        var retries = await Task.WhenAll(ReserveAsync(data, data.OwnerId, vehicleId, request),
            ReserveAsync(data, data.OwnerId, vehicleId, request));
        Assert.All(retries, result => Assert.Equal(ReservationStatus.Succeeded, result.Status));
        var retained = retries[0].Reservation!;
        Assert.Equal(request.OperationId, retained.Id);
        Assert.Equal(new PurchasePrice(60000m, 500m, 90m, 40m, 1000m, 59630m), retained.Price);
        Assert.Equal(TimeSpan.FromHours(48), retained.ExpiresAt - retained.CreatedAt);
        Assert.Equal(500m, retained.Deposit);
        Assert.Equal(1, await database.Reservations.CountAsync());
        Assert.Equal(ReservationStatus.Conflict, (await ReserveAsync(data, data.OwnerId, vehicleId,
            request with { PromoId = null })).Status);
        Assert.Equal(ReservationStatus.Conflict, (await ReserveAsync(data, data.OwnerId, second.Revision.VehicleId, request)).Status);

        await database.PromoCodes.Where(item => item.Id == promo.Id)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.IsActive, false).SetProperty(item => item.Discount, 2000m));
        await database.PurchaseSettings.ExecuteUpdateAsync(update => update.SetProperty(item => item.ProcessingFee, 700m));
        var draft = await services.GetRequiredService<ListingService>().SaveAsync(manager, vehicleId,
            new(Guid.NewGuid(), 1, 62000m, 90m, VehicleTransmission.Automatic, VehicleFuel.Petrol, "Updated asking price"),
            CancellationToken.None);
        Assert.Equal(ListingStatus.Succeeded, draft.Status);
        Assert.Equal(ListingStatus.Succeeded, (await services.GetRequiredService<ListingService>().PublishAsync(manager,
            vehicleId, new(Guid.NewGuid(), draft.Draft!.Id, first.Publication.SignedRevisionId), CancellationToken.None)).Status);
        await AssertReplayAsync(data, request, retained);
        await ReleaseAsync(retained);
        await AssertReplayAsync(data, request, retained);

        var other = await RegisterCustomerAsync(services, database);
        Assert.Equal(ReservationStatus.Conflict, (await ReserveAsync(data, other, vehicleId, request)).Status);
        var one = new ReservationRequest(Guid.NewGuid(), second.Publication.Id, null);
        var two = one with { OperationId = Guid.NewGuid() };
        var sameVehicle = await Task.WhenAll(ReserveAsync(data, data.OwnerId, second.Revision.VehicleId, one),
            ReserveAsync(data, other, second.Revision.VehicleId, two));
        Assert.Single(sameVehicle, result => result.Status == ReservationStatus.Succeeded);
        Assert.Single(sameVehicle, result => result.Status == ReservationStatus.Conflict);
        await ReleaseAsync(sameVehicle.Single(result => result.Status == ReservationStatus.Succeeded).Reservation!);

        var currentPublication = await database.ListingPublications.Where(item => item.RevisionId == draft.Draft.Id).SingleAsync();
        var sameCustomer = await Task.WhenAll(ReserveAsync(data, data.OwnerId, vehicleId,
            new(Guid.NewGuid(), currentPublication.Id, null)), ReserveAsync(data, data.OwnerId, second.Revision.VehicleId,
            new(Guid.NewGuid(), second.Publication.Id, null)));
        Assert.Single(sameCustomer, result => result.Status == ReservationStatus.Succeeded);
        Assert.Single(sameCustomer, result => result.Status == ReservationStatus.Conflict);
        var winner = sameCustomer.Single(result => result.Status == ReservationStatus.Succeeded).Reservation!;
        await ReleaseAsync(winner);
        var releaseId = await database.ReservationReleases.Where(item => item.ReservationId == winner.Id).Select(item => item.Id).SingleAsync();
        Assert.Equal(ReservationStatus.Conflict, (await ReserveAsync(data, data.OwnerId, second.Revision.VehicleId,
            new(releaseId, second.Publication.Id, null))).Status);
        Assert.Equal(3, await database.Reservations.CountAsync());
        Assert.Equal(3, await database.ReservationReleases.CountAsync());
        Assert.False(await database.Reservations.AnyAsync(item => item.IsActive));
        Assert.All(await database.Vehicles.ToListAsync(), vehicle => Assert.Equal(VehicleStatus.Available, vehicle.Status));
        await VerifyDeadlineAsync(data, currentPublication, second.Publication, promo.Id);
    }

    private async Task VerifyDeadlineAsync(CheckInTestData data, ListingPublication first, ListingPublication second, Guid promoId)
    {
        var request = new ReservationRequest(Guid.NewGuid(), first.Id, null);
        var held = (await ReserveAsync(data, data.OwnerId, first.VehicleId, request)).Reservation!;
        await using var clock = new ReservationTestClock(fixture.Database);
        await clock.InitializeAsync(held.ExpiresAt);
        await clock.Maintenance.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION reservation_test_clock.expire_promo() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                UPDATE public."PromoCodes" SET "ExpiresAtUtc" = clock_timestamp() WHERE "Id" = NEW."PromoId";
                RETURN NEW;
            END $$;
            CREATE TRIGGER admission_promo_expiry BEFORE INSERT ON "Reservations"
                FOR EACH ROW WHEN (NEW."PromoId" IS NOT NULL) EXECUTE FUNCTION reservation_test_clock.expire_promo();
            """);
        try
        {
            await using var restarted = clock.CreateContext();
            var service = new ReservationService(new ReservationStore(restarted, new MailDeliverySignal()));
            Assert.Equal(held.ExpiresAt, await restarted.Database
                .SqlQuery<DateTimeOffset>($"SELECT clock_timestamp() AS \"Value\"").SingleAsync());
            var rejected = await service.ReserveAsync(data.OwnerId, second.VehicleId,
                new(Guid.NewGuid(), first.Id, null), CancellationToken.None);
            Assert.Equal(ReservationStatus.Conflict, rejected.Status);
            var refund = await restarted.ReservationReleases.SingleAsync(item => item.ReservationId == held.Id);
            Assert.Equal(ReservationReleaseKind.Expired, refund.Kind);
            Assert.Equal(held.ExpiresAt, refund.RecordedAt);
            Assert.Equal(500m, refund.Refund);
            Assert.False(await restarted.Reservations.Where(item => item.Id == held.Id).Select(item => item.IsActive).SingleAsync());
            var replacement = await service.ReserveAsync(data.OwnerId, second.VehicleId,
                new(Guid.NewGuid(), second.Id, null), CancellationToken.None);
            Assert.Equal(ReservationStatus.Succeeded, replacement.Status);
            await clock.AdvanceAsync(replacement.Reservation!.ExpiresAt);
            await restarted.PromoCodes.Where(item => item.Id == promoId).ExecuteUpdateAsync(update => update
                .SetProperty(item => item.IsActive, true).SetProperty(item => item.ExpiresAtUtc, held.ExpiresAt.AddDays(10)));
            var failedAdmission = await service.ReserveAsync(data.OwnerId, first.VehicleId,
                new(Guid.NewGuid(), first.Id, promoId), CancellationToken.None);
            Assert.Equal(ReservationStatus.Conflict, failedAdmission.Status);
            Assert.False(await restarted.Reservations.Where(item => item.Id == replacement.Reservation.Id)
                .Select(item => item.IsActive).SingleAsync());
            Assert.Equal(1, await restarted.ReservationReleases.CountAsync(item => item.ReservationId == replacement.Reservation.Id));
            var replay = await service.ReserveAsync(data.OwnerId, first.VehicleId, request, CancellationToken.None);
            Assert.Equal(ReservationStatus.Succeeded, replay.Status);
            Assert.Equal(held.Id, replay.Reservation!.Id);
            Assert.Equal(held.Price, replay.Reservation.Price);
            Assert.Equal(1, await restarted.ReservationReleases.CountAsync(item => item.ReservationId == held.Id));
        }
        finally
        {
            await clock.Maintenance.Database.ExecuteSqlRawAsync("DROP TRIGGER admission_promo_expiry ON \"Reservations\"");
        }
    }

    private static async Task<ReservationResult> ReserveAsync(CheckInTestData data, Guid customerId, Guid vehicleId,
        ReservationRequest request)
    {
        using var scope = data.Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ReservationService>()
            .ReserveAsync(customerId, vehicleId, request, CancellationToken.None);
    }

    private static async Task AssertReplayAsync(CheckInTestData data, ReservationRequest request, Reservation retained)
    {
        var replay = await ReserveAsync(data, data.OwnerId, retained.VehicleId, request);
        Assert.Equal(ReservationStatus.Succeeded, replay.Status);
        Assert.Equal(retained.Id, replay.Reservation!.Id);
        Assert.Equal(retained.Price, replay.Reservation.Price);
        Assert.Equal(retained.Buyer, replay.Reservation.Buyer);
        Assert.Equal(retained.PromoCode, replay.Reservation.PromoCode);
        Assert.Equal(retained.CreatedAt, replay.Reservation.CreatedAt);
        Assert.Equal(retained.ExpiresAt, replay.Reservation.ExpiresAt);
    }

    private async Task ReleaseAsync(Reservation reservation)
    {
        await using var database = NewContext();
        var now = await database.Database.SqlQuery<DateTimeOffset>($"SELECT clock_timestamp() AS \"Value\"").SingleAsync();
        database.ReservationReleases.Add(new(Guid.NewGuid(), reservation, ReservationReleaseKind.CustomerCancelled,
            reservation.CustomerId, null, now));
        await database.SaveChangesAsync();
    }

    private DealershipDbContext NewContext() => new(new DbContextOptionsBuilder<DealershipDbContext>()
        .UseNpgsql(fixture.Database.RuntimeConnectionString).Options);
}
