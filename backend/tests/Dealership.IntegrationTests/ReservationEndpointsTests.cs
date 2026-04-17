using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Dealership.Application.Accounts;
using Dealership.Application.Vehicles;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Dealership.IntegrationTests.ReservationTestData;

namespace Dealership.IntegrationTests;

public sealed class ReservationEndpointsTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task AuthenticatedCommandsReturnBoundedPrivateImmutableReceiptsAcrossReleaseAndReplay()
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
                .UploadAsync(manager, listing.Revision.VehicleId, photoId, "image/jpeg", photo, CancellationToken.None)).Status);
        }

        var listings = services.GetRequiredService<ListingService>();
        var withPhoto = await listings.SaveAsync(manager, listing.Revision.VehicleId,
            new(Guid.NewGuid(), 1, 60000m, 90m, VehicleTransmission.Automatic, VehicleFuel.Petrol, "Inspected Malaysian vehicle")
            { PhotoIds = [photoId] }, CancellationToken.None);
        Assert.Equal(ListingStatus.Succeeded, withPhoto.Status);
        var published = await listings.PublishAsync(manager, listing.Revision.VehicleId,
            new(Guid.NewGuid(), withPhoto.Draft!.Id, listing.Publication.SignedRevisionId), CancellationToken.None);
        Assert.Equal(ListingStatus.Succeeded, published.Status);
        listing = listing with { Revision = withPhoto.Draft, Publication = published.Publication! };
        await using var database = new DealershipDbContext(new DbContextOptionsBuilder<DealershipDbContext>()
            .UseNpgsql(fixture.Database.RuntimeConnectionString).Options);
        database.PurchaseSettings.Add(new(500m, 0.08m, manager, DateTimeOffset.UtcNow));
        await database.SaveChangesAsync();
        var other = await RegisterCustomerAsync(services, database);
        var otherEmail = await database.Users.Where(item => item.Id == other).Select(item => item.Email).SingleAsync();
        var path = $"/api/vehicles/{listing.Revision.VehicleId}/reservations";
        var request = new ReservationRequest(Guid.NewGuid(), listing.Publication.Id, null);
        // Same-day booking setup uses midnight; bearer validation requires the normal live clock.
        using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var vehicleId = listing.Revision.VehicleId;
        var publicPhotoPath = $"/api/vehicles/{vehicleId}/photos/{photoId}";
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(publicPhotoPath)).StatusCode);
        Assert.Equal(1, (await client.GetFromJsonAsync<PublicListingPage>("/api/vehicles"))!.Total);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(path, request)).StatusCode);
        await LoginAsync(client, data.CustomerEmail);
        await AssertBodyBoundsAsync(client, path);

        var response = await client.PostAsJsonAsync(path, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var originalJson = await response.Content.ReadAsStringAsync();
        using var confirmed = JsonDocument.Parse(originalJson);
        Assert.Equal(request.OperationId, confirmed.RootElement.GetProperty("id").GetGuid());
        Assert.False(confirmed.RootElement.TryGetProperty("isActive", out _));
        Assert.Equal(500m, confirmed.RootElement.GetProperty("deposit").GetDecimal());
        Assert.Equal(data.CustomerEmail, confirmed.RootElement.GetProperty("buyer").GetProperty("email").GetString());
        Assert.Equal(60630m, confirmed.RootElement.GetProperty("price").GetProperty("total").GetDecimal());
        var receipt = JsonSerializer.Deserialize<ReservationReceipt>(originalJson, JsonSerializerOptions.Web)!;
        Assert.Equal(TimeSpan.FromHours(48), receipt.ExpiresAt - receipt.CreatedAt);
        Assert.Equal(1, await database.ReservationMailOutbox.CountAsync(item => item.ReservationId == receipt.Id));
        await AssertHeldVisibilityAsync(client, services, manager, vehicleId, publicPhotoPath, data.CustomerEmail);

        var cancellationPath = $"/api/reservations/{receipt.Id}/cancellations";
        var cancellation = new ReservationCancellationRequest(Guid.NewGuid(), null);
        await LoginAsync(client, otherEmail!);
        var denied = await client.PostAsJsonAsync(path, request);
        Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);
        Assert.DoesNotContain(data.CustomerEmail, await denied.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(cancellationPath, cancellation)).StatusCode);
        await LoginAsync(client, data.StaffEmail);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(path, request)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(cancellationPath,
            cancellation with { Reason = "Requested by buyer" })).StatusCode);

        await LoginAsync(client, data.CustomerEmail);
        var cancelled = await client.PostAsJsonAsync(cancellationPath, cancellation);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        Assert.True(cancelled.Headers.CacheControl?.NoStore);
        var refundJson = await cancelled.Content.ReadAsStringAsync();
        var refund = JsonSerializer.Deserialize<ReservationRefundReceipt>(refundJson, JsonSerializerOptions.Web)!;
        Assert.Equal("CustomerCancelled", refund.Kind);
        Assert.Equal(500m, refund.Refund);
        Assert.Equal(cancellation.OperationId, refund.Id);
        Assert.Equal(data.OwnerId, refund.ActorId);
        Assert.Equal(refundJson, await (await client.PostAsJsonAsync(cancellationPath, cancellation)).Content.ReadAsStringAsync());
        Assert.Equal(1, (await client.GetFromJsonAsync<PublicListingPage>("/api/vehicles"))!.Total);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/vehicles/{vehicleId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(publicPhotoPath)).StatusCode);

        await database.PurchaseSettings.ExecuteUpdateAsync(update => update.SetProperty(item => item.ProcessingFee, 900m));
        using var restarted = factory.CreateClient();
        restarted.DefaultRequestHeaders.Authorization = client.DefaultRequestHeaders.Authorization;
        var replay = await restarted.PostAsJsonAsync(path, request);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(originalJson, await replay.Content.ReadAsStringAsync());
        Assert.Equal(1, await database.ReservationMailOutbox.CountAsync(item => item.ReservationId == receipt.Id));
        Assert.False(await database.Reservations.Where(item => item.Id == receipt.Id).Select(item => item.IsActive).SingleAsync());
        Assert.Equal(1, await database.ReservationReleases.CountAsync(item => item.ReservationId == receipt.Id));
        var raceRequest = request with { OperationId = Guid.NewGuid() };
        using var holding = fixture.CreateFactory(observeDownload: _ =>
        {
            using var writer = factory.Services.CreateScope();
            var held = writer.ServiceProvider.GetRequiredService<ReservationService>().ReserveAsync(data.OwnerId,
                vehicleId, raceRequest, CancellationToken.None).GetAwaiter().GetResult();
            Assert.Equal(ReservationStatus.Succeeded, held.Status);
        });
        using var racingClient = holding.CreateClient();
        var withheld = await racingClient.GetAsync(publicPhotoPath);
        Assert.Equal(HttpStatusCode.NotFound, withheld.StatusCode);
        Assert.Empty(await withheld.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(
            $"/api/reservations/{raceRequest.OperationId}/cancellations",
            new ReservationCancellationRequest(Guid.NewGuid(), null))).StatusCode);
    }

    private static async Task AssertHeldVisibilityAsync(HttpClient client, IServiceProvider services, Guid manager,
        Guid vehicleId, string photoPath, string buyerEmail)
    {
        var catalogue = (await client.GetFromJsonAsync<PublicListingPage>("/api/vehicles"))!;
        Assert.Equal(0, catalogue.Total);
        Assert.Empty(catalogue.Items);
        var detail = await client.GetAsync($"/api/vehicles/{vehicleId}");
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
        Assert.DoesNotContain(buyerEmail, await detail.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(photoPath)).StatusCode);
        Assert.Equal(PurchaseQuoteStatus.NotFound, (await services.GetRequiredService<PurchaseQuoteService>()
            .QuoteAsync(vehicleId, null, CancellationToken.None)).Status);
        var workspace = (await services.GetRequiredService<IListingReadStore>()
            .WorkspaceAsync(manager, vehicleId, CancellationToken.None)).Workspace!;
        Assert.False(workspace.Vehicle.IsPublic);
    }

    private static async Task AssertBodyBoundsAsync(HttpClient client, string path)
    {
        using var malformed = new StringContent("{}", Encoding.UTF8, "application/json");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync(path, malformed)).StatusCode);
        using var nonJson = new StringContent("{}", Encoding.UTF8, "text/plain");
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, (await client.PostAsync(path, nonJson)).StatusCode);
        using var oversized = new StringContent(new string(' ', 16 * 1024 + 1), Encoding.UTF8, "application/json");
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await client.PostAsync(path, oversized)).StatusCode);
    }

    private static async Task LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(Guid.NewGuid(), email, "Password123!"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = (await response.Content.ReadFromJsonAsync<LoginSession>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
    }
}
