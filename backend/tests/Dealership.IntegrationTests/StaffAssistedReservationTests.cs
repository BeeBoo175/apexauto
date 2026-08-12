using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dealership.Application.Accounts;
using Dealership.Application.Branches;
using Dealership.Application.Vehicles;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Dealership.IntegrationTests.ReservationTestData;

namespace Dealership.IntegrationTests;

public sealed class StaffAssistedReservationTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task SalesStaffReserveForACustomerAtTheirBranchAndTheCustomerIsStillEmailed()
    {
        using var data = await CheckInTestData.CreateAsync(fixture);
        using var scope = data.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var manager = await ProvisionAsync(services, data.BranchId, AccountRoles.BranchManager);
        var technician = await ProvisionAsync(services, data.BranchId, AccountRoles.WorkshopTechnician);
        var listing = await CreateListingAsync(data, services, manager, technician, 0);
        await using var database = NewContext();
        database.PurchaseSettings.Add(new(500m, 0.08m, manager, DateTimeOffset.UtcNow));
        await database.SaveChangesAsync();
        var path = $"/api/vehicles/{listing.Revision.VehicleId}/reservations";
        var request = new ReservationRequest(Guid.NewGuid(), listing.Publication.Id, null);
        using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();

        await LoginAsync(client, data.CustomerEmail);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(client, path, request, data.OwnerId)).StatusCode);

        var otherBranch = Guid.NewGuid();
        await services.GetRequiredService<BranchProvisioningService>().ProvisionBranchAsync(
            DevelopmentFixtureManifest.Branches[1] with { Id = otherBranch }, CancellationToken.None);
        var elsewhere = await ProvisionAsync(services, otherBranch, AccountRoles.SalesAgent);
        var elsewhereEmail = await database.Users.Where(item => item.Id == elsewhere).Select(item => item.Email).SingleAsync();
        await LoginAsync(client, elsewhereEmail!);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(client, path, request, data.OwnerId)).StatusCode);
        Assert.False(await database.Reservations.AnyAsync());

        await LoginAsync(client, data.StaffEmail);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, path, request, Guid.Empty)).StatusCode);
        var accepted = await Post(client, path, request, data.OwnerId);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var reservation = await database.Reservations.AsNoTracking().SingleAsync();
        Assert.Equal(data.OwnerId, reservation.CustomerId);
        Assert.Equal(data.StaffId, reservation.BookedByStaffId);
        Assert.Equal(1, await database.ReservationMailOutbox.CountAsync(item => item.ReservationId == reservation.Id));
        Assert.Equal(HttpStatusCode.OK, (await Post(client, path, request, data.OwnerId)).StatusCode);
    }

    private DealershipDbContext NewContext() => new(new DbContextOptionsBuilder<DealershipDbContext>()
        .UseNpgsql(fixture.Database.RuntimeConnectionString).Options);

    private static async Task<HttpResponseMessage> Post(HttpClient client, string path, ReservationRequest request, Guid customerId)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(request) };
        message.Headers.Add("X-On-Behalf-Of", customerId.ToString());
        return await client.SendAsync(message);
    }

    private static async Task LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(Guid.NewGuid(), email, "Password123!"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = (await response.Content.ReadFromJsonAsync<LoginSession>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
    }
}
