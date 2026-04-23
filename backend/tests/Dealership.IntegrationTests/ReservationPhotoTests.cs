using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dealership.Application.Accounts;
using Dealership.Application.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Dealership.IntegrationTests.ReservationTestData;

namespace Dealership.IntegrationTests;

public sealed class ReservationPhotoTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task OwnReservationRetainsApprovedPhotoAccessAcrossWithdrawalWithoutGrantingDraftOrOtherCustomerAccess()
    {
        using var data = await CheckInTestData.CreateAsync(fixture);
        using var scope = data.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var database = services.GetRequiredService<DealershipDbContext>();
        var manager = await ProvisionAsync(services, data.BranchId, AccountRoles.BranchManager);
        var technician = await ProvisionAsync(services, data.BranchId, AccountRoles.WorkshopTechnician);
        var listing = await CreateListingAsync(data, services, manager, technician, 0);
        var photos = services.GetRequiredService<ListingPhotoService>();
        var bytes = IntakePhotoFixture.Image();
        var cover = Guid.NewGuid();
        var draftOnly = Guid.NewGuid();
        foreach (var id in new[] { cover, draftOnly })
        {
            using var image = new MemoryStream(bytes);
            Assert.Equal(ListingPhotoStatus.Available,
                (await photos.UploadAsync(manager, listing.Publication.VehicleId, id, "image/jpeg", image, CancellationToken.None)).Status);
        }

        var listings = services.GetRequiredService<ListingService>();
        var draft = await listings.SaveAsync(manager, listing.Publication.VehicleId,
            new(Guid.NewGuid(), 1, 60000m, 90m, listing.Revision.Transmission, listing.Revision.Fuel, "Approved reservation photography")
            { PhotoIds = [cover] }, CancellationToken.None);
        Assert.Equal(ListingStatus.Succeeded, draft.Status);
        var published = await listings.PublishAsync(manager, listing.Publication.VehicleId,
            new(Guid.NewGuid(), draft.Draft!.Id, listing.Publication.SignedRevisionId), CancellationToken.None);
        Assert.Equal(ListingStatus.Succeeded, published.Status);
        database.PurchaseSettings.Add(new(500m, 0.08m, manager, DateTimeOffset.UtcNow));
        await database.SaveChangesAsync();
        var hold = await services.GetRequiredService<ReservationService>().ReserveAsync(data.OwnerId,
            listing.Publication.VehicleId, new(Guid.NewGuid(), published.Publication!.Id, null), CancellationToken.None);
        Assert.Equal(ReservationStatus.Succeeded, hold.Status);
        var reservation = hold.Reservation!;
        var history = (await services.GetRequiredService<IReservationReadStore>()
            .AccountAsync(data.OwnerId, 1, CancellationToken.None)).History!;
        Assert.Equal(cover, history.Active!.CoverPhotoId);
        Assert.Equal(cover, Assert.Single(history.Items).CoverPhotoId);

        using var httpFactory = fixture.CreateFactory();
        using var client = httpFactory.CreateClient();
        var path = $"/api/account/reservations/{reservation.Id}/photos/{cover}";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
        await LoginAsync(client, data.CustomerEmail);
        using (var response = await client.GetAsync(path))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
            Assert.Equal(bytes, await response.Content.ReadAsByteArrayAsync());
        }

        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/account/reservations/{reservation.Id}/photos/{draftOnly}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/account/reservations/{Guid.NewGuid()}/photos/{cover}")).StatusCode);
        var outsider = await RegisterCustomerAsync(services, database);
        await LoginAsync(client, (await database.Users.Where(user => user.Id == outsider).Select(user => user.Email).SingleAsync())!);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
        await LoginAsync(client, (await database.Users.Where(user => user.Id == manager).Select(user => user.Email).SingleAsync())!);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode);

        Assert.Equal(ListingStatus.Succeeded, (await listings.WithdrawAsync(manager, reservation.VehicleId,
            new(Guid.NewGuid(), published.Publication.Id, "Withdraw fixture listing"), CancellationToken.None)).Status);
        await LoginAsync(client, data.CustomerEmail);
        Assert.Equal(bytes, await client.GetByteArrayAsync(path));
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/vehicles/{reservation.VehicleId}/photos/{cover}")).StatusCode);
        var after = (await services.GetRequiredService<IReservationReadStore>()
            .AccountAsync(data.OwnerId, 1, CancellationToken.None)).History!;
        Assert.Null(after.Active);
        Assert.Equal(cover, Assert.Single(after.Items).CoverPhotoId);
    }

    private static async Task LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(Guid.NewGuid(), email, "Password123!"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = (await response.Content.ReadFromJsonAsync<LoginSession>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
    }
}
