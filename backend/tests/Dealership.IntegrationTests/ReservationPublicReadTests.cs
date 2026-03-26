using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using Dealership.Application.Accounts;
using Dealership.Application.Vehicles;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Dealership.IntegrationTests.ReservationTestData;

namespace Dealership.IntegrationTests;

public sealed class ReservationPublicReadTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task PublicTargetReadsCommitExactDeadlineRefundsAcrossDenialConcurrencyAndRestart()
    {
        using var data = await CheckInTestData.CreateAsync(fixture);
        using var scope = data.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var manager = await ProvisionAsync(services, data.BranchId, AccountRoles.BranchManager);
        var technician = await ProvisionAsync(services, data.BranchId, AccountRoles.WorkshopTechnician);
        var listing = await CreateListingAsync(data, services, manager, technician, 0);
        var photoId = Guid.NewGuid();
        using (var photo = new MemoryStream(IntakePhotoFixture.Image()))
        {
            Assert.Equal(ListingPhotoStatus.Available, (await services.GetRequiredService<ListingPhotoService>()
                .UploadAsync(manager, listing.Publication.VehicleId, photoId, "image/jpeg", photo, CancellationToken.None)).Status);
        }

        var listings = services.GetRequiredService<ListingService>();
        var draft = await listings.SaveAsync(manager, listing.Publication.VehicleId,
            new(Guid.NewGuid(), 1, 60000m, 90m, VehicleTransmission.Automatic, VehicleFuel.Petrol,
                "Inspected Malaysian vehicle")
            { PhotoIds = [photoId] }, CancellationToken.None);
        Assert.Equal(ListingStatus.Succeeded, draft.Status);
        var published = await listings.PublishAsync(manager, listing.Publication.VehicleId,
            new(Guid.NewGuid(), draft.Draft!.Id, listing.Publication.SignedRevisionId), CancellationToken.None);
        Assert.Equal(ListingStatus.Succeeded, published.Status);
        var publication = published.Publication!;
        await using var clock = new ReservationTestClock(fixture.Database);
        var now = DateTimeOffset.UtcNow;
        await clock.InitializeAsync(now);
        clock.Maintenance.PurchaseSettings.Add(new(500m, 0.08m, manager, now));
        await clock.Maintenance.SaveChangesAsync();

        // Reuse one canonical listing; each entry independently initiates its own refund.
        foreach (var read in Enum.GetValues<PublicRead>())
        {
            var held = await HoldAsync(clock, data.OwnerId, publication);
            await clock.AdvanceAsync(held.ExpiresAt.AddTicks(-10));
            await using (var before = clock.CreateContext())
            {
                await AssertReadAsync(before, publication.VehicleId, photoId, read, false);
                Assert.False(await before.ReservationReleases.AnyAsync(item => item.ReservationId == held.Id));
            }

            await clock.AdvanceAsync(held.ExpiresAt);
            await using var first = clock.CreateContext();
            await using var competing = clock.CreateContext();
            await Task.WhenAll(
                AssertReadAsync(first, publication.VehicleId, photoId, read, true),
                AssertReadAsync(competing, publication.VehicleId, photoId, read, true));
            var refund = await clock.Maintenance.ReservationReleases.AsNoTracking()
                .SingleAsync(item => item.ReservationId == held.Id);
            Assert.Equal(ReservationReleaseKind.Expired, refund.Kind);
            Assert.Equal(500m, refund.Refund);
            Assert.Equal(held.ExpiresAt, refund.RecordedAt);
            Assert.False(await first.Reservations.Where(item => item.Id == held.Id).Select(item => item.IsActive).SingleAsync());
            Assert.Equal(VehicleStatus.Available, await first.Vehicles.Where(item => item.Id == publication.VehicleId)
                .Select(item => item.Status).SingleAsync());
            await using var restarted = clock.CreateContext();
            await AssertReadAsync(restarted, publication.VehicleId, photoId, read, true);
            Assert.Equal(refund.Id, (await restarted.ReservationReleases.SingleAsync(item => item.ReservationId == held.Id)).Id);
        }

        await AssertCompetingCancellationAsync(clock, data.OwnerId, publication);
        await AssertUnavailableAsync(clock, publication.VehicleId, photoId);
    }

    private async Task AssertUnavailableAsync(ReservationTestClock clock, Guid vehicleId, Guid photoId)
    {
        var failures = new FailSnapshot();
        await using var source = clock.CreateContext();
        using var factory = fixture.CreateFactory().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<DealershipDbContext>>();
            services.RemoveAll<DealershipDbContext>();
            services.AddDbContext<DealershipDbContext>(options => options
                .UseNpgsql(source.Database.GetConnectionString()).AddInterceptors(failures));
        }));
        using var client = factory.CreateClient();
        var detail = await client.GetAsync($"/api/vehicles/{vehicleId}");
        var quote = await client.PostAsJsonAsync("/api/promos/validate", new PromoValidationRequest(vehicleId, "APEX500"));
        var photo = await client.GetAsync($"/api/vehicles/{vehicleId}/photos/{photoId}");
        foreach (var response in new[] { detail, quote, photo })
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
        }

        Assert.Equal(9, failures.Attempts);
    }

    private static async Task AssertCompetingCancellationAsync(ReservationTestClock clock, Guid customerId,
        ListingPublication publication)
    {
        var held = await HoldAsync(clock, customerId, publication);
        var race = new CancelAfterSnapshot(async () =>
        {
            await using var writer = clock.CreateContext();
            var cancelled = await new ReservationCancellationService(new ReservationCancellationStore(writer))
                .CancelAsync(customerId, held.Id, new(Guid.NewGuid(), null), CancellationToken.None);
            Assert.Equal(ReservationStatus.Succeeded, cancelled.Status);
        });
        await using var source = clock.CreateContext();
        await using var reader = new DealershipDbContext(new DbContextOptionsBuilder<DealershipDbContext>()
            .UseNpgsql(source.Database.GetConnectionString()).AddInterceptors(race).Options);
        var detail = await new PublicListingStore(reader).DetailAsync(publication.VehicleId, CancellationToken.None);
        Assert.True(race.Invoked);
        Assert.True(race.LockAttempts > 1);
        Assert.Equal(ListingStatus.Succeeded, detail.Status);
        Assert.Equal(ReservationReleaseKind.CustomerCancelled,
            (await reader.ReservationReleases.SingleAsync(item => item.ReservationId == held.Id)).Kind);
    }

    private static async Task<Reservation> HoldAsync(ReservationTestClock clock, Guid customerId,
        ListingPublication publication)
    {
        await using var database = clock.CreateContext();
        var result = await new ReservationService(new ReservationStore(database, new MailDeliverySignal()))
            .ReserveAsync(customerId, publication.VehicleId, new(Guid.NewGuid(), publication.Id, null), CancellationToken.None);
        Assert.Equal(ReservationStatus.Succeeded, result.Status);
        return result.Reservation!;
    }

    private static async Task AssertReadAsync(DealershipDbContext database, Guid vehicleId, Guid photoId,
        PublicRead read, bool released)
    {
        if (read == PublicRead.Detail)
        {
            var detail = await new PublicListingStore(database).DetailAsync(vehicleId, CancellationToken.None);
            Assert.Equal(released ? ListingStatus.Succeeded : ListingStatus.NotFound, detail.Status);
            return;
        }

        if (read == PublicRead.Quote)
        {
            var quote = await new PurchaseQuoteService(new PurchaseQuoteStore(database))
                .QuoteAsync(vehicleId, null, CancellationToken.None);
            Assert.Equal(released ? PurchaseQuoteStatus.Succeeded : PurchaseQuoteStatus.NotFound, quote.Status);
            return;
        }

        var id = read == PublicRead.MissingPhoto ? Guid.NewGuid() : photoId;
        var photo = await new ListingPhotoStore(database).FindAsync(null, vehicleId, id, CancellationToken.None);
        var available = released && read == PublicRead.Photo;
        Assert.Equal(available ? ListingPhotoStatus.Available : ListingPhotoStatus.NotFound, photo.Status);
    }

    private enum PublicRead
    {
        Detail,
        Quote,
        Photo,
        MissingPhoto
    }

    private sealed class CancelAfterSnapshot(Func<Task> cancel) : DbCommandInterceptor
    {
        public bool Invoked { get; private set; }

        public int LockAttempts { get; private set; }

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!command.CommandText.Contains("FROM \"Vehicles\"", StringComparison.Ordinal))
            {
                return result;
            }

            LockAttempts++;
            if (Invoked)
            {
                return result;
            }

            Invoked = true;
            // Establish a real PostgreSQL snapshot before the competing production cancellation.
            await using var snapshot = command.Connection!.CreateCommand();
            snapshot.Transaction = command.Transaction;
            snapshot.CommandText = "SELECT count(*) FROM \"Reservations\"";
            await snapshot.ExecuteScalarAsync(cancellationToken);
            await cancel();
            return result;
        }
    }

    private sealed class FailSnapshot : DbCommandInterceptor
    {
        public int Attempts { get; private set; }

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!command.CommandText.Contains("FROM \"Vehicles\"", StringComparison.Ordinal))
            {
                return result;
            }

            Attempts++;
            await using var failure = command.Connection!.CreateCommand();
            failure.Transaction = command.Transaction;
            failure.CommandText = "DO $$ BEGIN RAISE EXCEPTION 'Concurrent reservation change' USING ERRCODE = '40001'; END $$";
            await failure.ExecuteNonQueryAsync(cancellationToken);
            return result;
        }
    }
}
