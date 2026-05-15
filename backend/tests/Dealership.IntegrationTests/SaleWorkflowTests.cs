using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dealership.Application.Accounts;
using Dealership.Application.Sales;
using Dealership.Application.Vehicles;
using Dealership.Domain.Sales;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Dealership.IntegrationTests;

public sealed class SaleWorkflowTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task VehicleReferencesAndPhotosFollowCurrentSaleAccessAndRetainTheReservedPublication()
    {
        var world = await SaleWorld.SharedAsync(fixture);
        var held = await world.HoldAsync(withPhoto: true);
        var photos = world.Services.GetRequiredService<ListingPhotoService>();
        var salePhotos = world.Services.GetRequiredService<SaleVehiclePhotoService>();
        var listings = world.Services.GetRequiredService<ListingService>();
        var reference = await world.Reads.VehicleAsync(world.Agent, held.Id, default);
        Assert.Equal(SaleStatus.Succeeded, reference.Status);
        var photoId = Assert.Single(reference.Value!.Listing.PhotoIds);
        using var content = new MemoryStream();
        Assert.Equal(ListingPhotoStatus.Available,
            (await salePhotos.DownloadAsync(world.Agent, held.Id, photoId, content, default)).Status);
        Assert.Equal(IntakePhotoFixture.Image(), content.ToArray());
        using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var path = $"/api/staff/sales/{held.Id}/vehicle";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
        await using (var accounts = NewContext())
        {
            var email = await accounts.Users.Where(item => item.Id == world.Agent).Select(item => item.Email).SingleAsync();
            var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(Guid.NewGuid(), email!, "Password123!"));
            var session = (await login.Content.ReadFromJsonAsync<LoginSession>())!;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        }
        var detail = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.True(detail.Headers.CacheControl!.NoStore);
        Assert.Equal(held.Id, (await detail.Content.ReadFromJsonAsync<SaleVehicleReference>())!.SaleId);
        var delivered = await client.GetAsync($"{path}/photos/{photoId}");
        Assert.Equal(HttpStatusCode.OK, delivered.StatusCode);
        Assert.True(delivered.Headers.CacheControl!.NoStore);
        Assert.Equal(IntakePhotoFixture.Image(), await delivered.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/staff/listings/{held.VehicleId}")).StatusCode);
        Assert.Equal(ListingStatus.NotFound, (await world.Services.GetRequiredService<IPublicListingStore>()
            .DetailAsync(held.VehicleId, default)).Status);
        Assert.Equal(ListingPhotoStatus.NotFound,
            (await photos.DownloadAsync(null, held.VehicleId, photoId, content, default)).Status);

        foreach (var actor in new[] { world.OtherBranchAgent, held.CustomerId })
        {
            Assert.NotEqual(SaleStatus.Succeeded, (await world.Reads.VehicleAsync(actor, held.Id, default)).Status);
            Assert.NotEqual(ListingPhotoStatus.Available,
                (await salePhotos.DownloadAsync(actor, held.Id, photoId, content, default)).Status);
        }

        // A later private draft and its photo cannot replace the customer's reserved publication.
        var privatePhoto = Guid.NewGuid();
        using var image = new MemoryStream(IntakePhotoFixture.Image());
        Assert.Equal(ListingPhotoStatus.Available, (await photos.UploadAsync(world.Manager, held.VehicleId,
            privatePhoto, "image/jpeg", image, default)).Status);
        Assert.Equal(ListingStatus.Succeeded, (await listings.SaveAsync(world.Manager, held.VehicleId,
            new(Guid.NewGuid(), 1, 62000m, 100m, VehicleTransmission.Manual, VehicleFuel.Hybrid, "Updated private draft")
            {
                PhotoIds = [privatePhoto]
            }, default)).Status);
        var retained = (await world.Reads.VehicleAsync(world.Manager, held.Id, default)).Value!;
        Assert.Equal(60000m, retained.Listing.VehiclePrice);
        Assert.Equal("Inspected Malaysian vehicle", retained.Listing.Description);
        Assert.Equal([photoId], retained.Listing.PhotoIds);
        Assert.Equal(ListingPhotoStatus.NotFound,
            (await salePhotos.DownloadAsync(world.Agent, held.Id, privatePhoto, content, default)).Status);

        await world.Handling.ClaimAsync(world.Agent, held.Id, new(Guid.NewGuid(), 1), default);
        Assert.Equal(SaleStatus.NotFound,
            (await world.Reads.VehicleAsync(world.Colleague, held.Id, default)).Status);
        Assert.Equal(ListingPhotoStatus.NotFound,
            (await salePhotos.DownloadAsync(world.Colleague, held.Id, photoId, content, default)).Status);
        await world.Handling.AssignAsync(world.Manager, held.Id, new(Guid.NewGuid(), 2, world.Colleague), default);
        Assert.Equal(SaleStatus.NotFound, (await world.Reads.VehicleAsync(world.Agent, held.Id, default)).Status);
        Assert.Equal(ListingPhotoStatus.NotFound,
            (await salePhotos.DownloadAsync(world.Agent, held.Id, photoId, content, default)).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{path}/photos/{photoId}")).StatusCode);
        Assert.Equal(SaleStatus.Succeeded, (await world.Reads.VehicleAsync(world.Colleague, held.Id, default)).Status);

        await using var database = NewContext();
        await database.Users.Where(item => item.Id == world.Colleague)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.IsActive, false));
        try
        {
            Assert.Equal(SaleStatus.Forbidden, (await world.Reads.VehicleAsync(world.Colleague, held.Id, default)).Status);
            Assert.Equal(ListingPhotoStatus.Forbidden,
                (await salePhotos.DownloadAsync(world.Colleague, held.Id, photoId, content, default)).Status);
        }
        finally
        {
            await database.Users.Where(item => item.Id == world.Colleague)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.IsActive, true));
        }
    }

    [Fact]
    public async Task OneSaleCarriesAHoldFromConfirmationThroughClaimReleaseAndReclaim()
    {
        var world = await SaleWorld.SharedAsync(fixture);
        var held = await world.HoldAsync();

        await using (var database = NewContext())
        {
            var opened = await database.SalesOrders.AsNoTracking().SingleAsync(item => item.Id == held.Id);
            Assert.Equal((SalesOrderStatus.Open, held.ExpiresAt, 1L), (opened.Status, opened.DueAt, opened.Version));
            var created = await database.SalesOrderEvents.AsNoTracking().SingleAsync(item => item.SalesOrderId == held.Id);
            Assert.Equal((SalesOrderEventKind.Created, held.CustomerId), (created.Kind, created.ActorId));
        }

        // Only an agent of the sale's branch claims; a replay returns the same receipt and a second claim conflicts.
        Assert.Equal(SaleStatus.Forbidden, (await world.Handling.ClaimAsync(world.Manager, held.Id,
            new(Guid.NewGuid(), 1), default)).Status);
        Assert.Equal(SaleStatus.NotFound, (await world.Handling.ClaimAsync(world.OtherBranchAgent, held.Id,
            new(Guid.NewGuid(), 1), default)).Status);
        var claim = new SaleCommandRequest(Guid.NewGuid(), 1);
        var claimed = await world.Handling.ClaimAsync(world.Agent, held.Id, claim, default);
        Assert.Equal(SaleStatus.Succeeded, claimed.Status);
        Assert.Equal(claimed.Receipt, (await world.Handling.ClaimAsync(world.Agent, held.Id, claim, default)).Receipt);
        // Once handled, the sale is invisible to other agents, so a late claim learns nothing about it.
        Assert.Equal(SaleStatus.NotFound, (await world.Handling.ClaimAsync(world.Colleague, held.Id,
            new(Guid.NewGuid(), 1), default)).Status);
        Assert.Equal(SaleStatus.Conflict, (await world.Handling.ClaimAsync(world.Agent, held.Id,
            new(claim.OperationId, 2), default)).Status);

        // The handling agent now reads the customer's contact; the colleague no longer sees the sale at all.
        var view = (await world.Reads.GetAsync(world.Agent, held.Id, default)).Value!;
        Assert.Equal((SalesOrderStatus.Handling, null, held.Buyer.Mobile), (view.Sale.Status, view.Sale.DueAt,
            view.Context.CustomerMobile));
        Assert.True(view.Actions.SaveTerms && view.Actions.Submit && view.Actions.Release && !view.Actions.Decide);
        Assert.Equal(SaleStatus.NotFound, (await world.Reads.GetAsync(world.Colleague, held.Id, default)).Status);

        var terms = await world.Terms.SaveTermsAsync(world.Agent, held.Id,
            new(Guid.NewGuid(), 2, null, 300m, null, 0m), default);
        Assert.Equal((SaleStatus.Succeeded, 3L), (terms.Status, terms.Receipt!.Version));

        var released = await world.Handling.ReleaseAsync(world.Agent, held.Id, new(Guid.NewGuid(), 3), default);
        Assert.Equal(SaleStatus.Succeeded, released.Status);
        var reclaimed = await world.Handling.AssignAsync(world.Manager, held.Id,
            new(Guid.NewGuid(), 4, world.Colleague), default);
        Assert.Equal(("Assigned", 5L), (reclaimed.Receipt!.Kind, reclaimed.Receipt.Version));

        await using (var database = NewContext())
        {
            // Release and reclaim reuse the one sale; release reset the terms and resumed the remaining time.
            Assert.Equal(1, await database.SalesOrders.CountAsync(item => item.Id == held.Id));
            var sale = await database.SalesOrders.AsNoTracking().SingleAsync(item => item.Id == held.Id);
            Assert.Equal((world.Colleague, 0m, null), (sale.AgentId, sale.NegotiatedDiscount, sale.DueAt));
        }

        // Every entry, newest first, can be opened as the sale stood after it, beside the version before it.
        var history = (await world.Reads.HistoryAsync(world.Manager, held.Id, 1, 10, default)).Value!;
        Assert.Equal(["Assigned", "Released", "TermsSaved", "Claimed", "Created"], history.Items.Select(item => item.Kind));
        Assert.Contains(history.Items[1].Changes,
            change => change is { Field: SaleChange.NegotiatedDiscount, Before: "300.00" });
        for (var version = 1; version <= 5; version++)
        {
            var past = (await world.Reads.VersionAsync(world.Manager, held.Id, version, default)).Value!;
            Assert.Equal(version, past.Snapshot.Version);
            Assert.Equal(version == 1, past.Previous is null);
        }

        Assert.Equal(300m,
            (await world.Reads.VersionAsync(world.Manager, held.Id, 3, default)).Value!.Snapshot.NegotiatedDiscount);
        Assert.Equal(SaleStatus.NotFound, (await world.Reads.HistoryAsync(world.Agent, held.Id, 1, 10, default)).Status);
    }

    [Fact]
    public async Task TheDatabaseKeepsSalesAndTheirHistoryIntact()
    {
        var world = await SaleWorld.SharedAsync(fixture);
        var held = await world.HoldAsync();
        await using var database = NewContext();

        foreach (var statement in new[]
        {
            $"DELETE FROM \"SalesOrders\" WHERE \"Id\" = '{held.Id}'",
            $"UPDATE \"SalesOrders\" SET \"BasePrice\" = 1, \"Version\" = 2 WHERE \"Id\" = '{held.Id}'",
            $"UPDATE \"SalesOrders\" SET \"NegotiatedDiscount\" = 0 WHERE \"Id\" = '{held.Id}'",
            $"UPDATE \"SalesOrderEvents\" SET \"Reason\" = 'x' WHERE \"SalesOrderId\" = '{held.Id}'"
        })
        {
            await Assert.ThrowsAnyAsync<PostgresException>(() => database.Database.ExecuteSqlRawAsync(statement));
        }

        // A change without its history entry is refused at commit.
        await using var transaction = await database.Database.BeginTransactionAsync();
        await database.Database.ExecuteSqlRawAsync(
            $"UPDATE \"SalesOrders\" SET \"Version\" = 2, \"UpdatedAt\" = clock_timestamp() WHERE \"Id\" = '{held.Id}'");
        await Assert.ThrowsAnyAsync<PostgresException>(() => transaction.CommitAsync());
    }

    private DealershipDbContext NewContext() => new(new DbContextOptionsBuilder<DealershipDbContext>()
        .UseNpgsql(fixture.Database.RuntimeConnectionString).Options);
}
