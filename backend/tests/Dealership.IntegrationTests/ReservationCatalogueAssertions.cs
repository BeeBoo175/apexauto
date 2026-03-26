using System.Net;
using System.Net.Http.Json;
using Dealership.Application.Vehicles;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dealership.IntegrationTests;

internal static class ReservationCatalogueAssertions
{
    internal static async Task VerifyAsync(IntakePhotoFixture fixture, IServiceProvider services, Guid branchId,
        IReadOnlyList<Guid> vehicleIds)
    {
        await using var clock = new ReservationTestClock(fixture.Database);
        await clock.InitializeAsync(DateTimeOffset.UtcNow);
        var filter = new PublicListingFilter(BranchId: branchId, Sort: "price", Direction: "asc");
        var store = new PublicListingStore(clock.Maintenance);
        var initial = (await store.ReadAsync(filter, CancellationToken.None)).Catalogue!;
        var next = (await store.ReadAsync(filter with { Page = 2 }, CancellationToken.None)).Catalogue!;
        var total = initial.Total;
        var expected = initial.Items.Concat(next.Items).Select(item => item.Id).ToArray();
        var holds = new List<Reservation>();
        foreach (var vehicleId in vehicleIds)
        {
            var customer = await ReservationTestData.RegisterCustomerAsync(services, clock.Maintenance);
            var publication = await clock.Maintenance.ListingPublications.AsNoTracking()
                .SingleAsync(item => item.VehicleId == vehicleId);
            await using var writer = clock.CreateContext();
            var held = await new ReservationService(new ReservationStore(writer, new MailDeliverySignal(), new ReservationExpirySignal()))
                .ReserveAsync(customer, vehicleId, new(Guid.NewGuid(), publication.Id, null), CancellationToken.None);
            Assert.Equal(ReservationStatus.Succeeded, held.Status);
            holds.Add(held.Reservation!);
        }

        Assert.Equal(PublicListingFilter.PageSize + 1, holds.Count);
        var expiry = Assert.Single(holds.Select(item => item.ExpiresAt).Distinct());
        await clock.AdvanceAsync(expiry.AddTicks(-10));
        await using (var before = clock.CreateContext())
        {
            var catalogue = (await new PublicListingStore(before).ReadAsync(filter, CancellationToken.None)).Catalogue!;
            Assert.Equal(total - holds.Count, catalogue.Total);
            Assert.False(await before.ReservationReleases.AnyAsync());
        }

        await clock.AdvanceAsync(expiry);
        await using var source = clock.CreateContext();
        await using (var unmatched = clock.CreateContext())
        {
            var empty = await new PublicListingStore(unmatched).ReadAsync(filter with { Make = "Unknown make" }, CancellationToken.None);
            Assert.Equal(ListingStatus.Succeeded, empty.Status);
            Assert.Equal(0, empty.Catalogue!.Total);
            Assert.False(await unmatched.ReservationReleases.AnyAsync());
        }

        using var factory = fixture.CreateFactory().WithWebHostBuilder(builder => builder.ConfigureServices(collection =>
        {
            collection.RemoveAll<DbContextOptions<DealershipDbContext>>();
            collection.RemoveAll<DealershipDbContext>();
            collection.AddDbContext<DealershipDbContext>(options => options.UseNpgsql(source.Database.GetConnectionString()));
        }));
        using var client = factory.CreateClient();
        var incomplete = await client.GetAsync($"/api/vehicles?branchId={branchId}&page={int.MaxValue}");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, incomplete.StatusCode);
        Assert.True(incomplete.Headers.CacheControl?.NoStore);
        var refundedIds = await source.ReservationReleases.Select(item => item.ReservationId).ToArrayAsync();
        Assert.Equal(PublicListingFilter.PageSize, refundedIds.Length);
        Assert.All(refundedIds, id => Assert.Contains(holds, item => item.Id == id));
        var orderedVehicles = await source.Vehicles.Where(item => vehicleIds.Contains(item.Id))
            .OrderBy(item => item.Id).Select(item => item.Id).ToArrayAsync();
        var refundedVehicles = await source.Reservations.Where(item => refundedIds.Contains(item.Id))
            .OrderBy(item => item.VehicleId).Select(item => item.VehicleId).ToArrayAsync();
        Assert.Equal(orderedVehicles.Take(PublicListingFilter.PageSize), refundedVehicles);

        await using var first = clock.CreateContext();
        await using var second = clock.CreateContext();
        var completed = await Task.WhenAll(
            new PublicListingStore(first).ReadAsync(filter, CancellationToken.None),
            new PublicListingStore(second).ReadAsync(filter with { Page = 2 }, CancellationToken.None));
        Assert.All(completed, result =>
        {
            Assert.Equal(ListingStatus.Succeeded, result.Status);
            Assert.Equal(total, result.Catalogue!.Total);
        });
        Assert.Equal(expected, completed.SelectMany(result => result.Catalogue!.Items).Select(item => item.Id));
        var refunds = await source.ReservationReleases.AsNoTracking().ToArrayAsync();
        Assert.Equal(holds.Count, refunds.Length);
        Assert.All(refunds, refund =>
        {
            Assert.Equal(ReservationReleaseKind.Expired, refund.Kind);
            Assert.Equal(500m, refund.Refund);
            Assert.Equal(expiry, refund.RecordedAt);
        });

        var beyond = (await client.GetFromJsonAsync<PublicListingPage>($"/api/vehicles?branchId={branchId}&page={int.MaxValue}"))!;
        Assert.Equal(total, beyond.Total);
        Assert.Empty(beyond.Items);
        Assert.Equal(holds.Count, await source.ReservationReleases.CountAsync());
    }
}
