using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dealership.Application.Accounts;
using Dealership.Application.Branches;
using Dealership.Application.TestDrives;
using Dealership.Domain.Common;
using Dealership.Domain.TestDrives;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.TestDrives;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Dealership.IntegrationTests.ReservationTestData;

namespace Dealership.IntegrationTests;

public sealed class StaffAssistedTestDriveTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task SalesStaffBookATestDriveForACustomerAtTheirBranchAndTheCustomerIsStillEmailed()
    {
        var allDay = new AppointmentSchedule(new TimeOnly(1, 0), new TimeOnly(23, 59), 90, 30, 0, Enum.GetValues<DayOfWeek>());
        using var data = await CheckInTestData.CreateAsync(fixture, allDay);
        using var scope = data.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var manager = await ProvisionAsync(services, data.BranchId, AccountRoles.BranchManager);
        var technician = await ProvisionAsync(services, data.BranchId, AccountRoles.WorkshopTechnician);
        var listing = await CreateListingAsync(data, services, manager, technician, 0);
        await using var database = NewContext();
        var now = DateTimeOffset.UtcNow;
        // A separate factory keeps the real clock and the configured schedule, so tokens and slots are valid.
        using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var tomorrow = DateOnly.FromDateTime(now.AddHours(8).DateTime).AddDays(1);
        var schedule = factory.Services.GetRequiredService<AppointmentSchedule>();
        var slot = Enumerable.Range(0, 7).SelectMany(day => schedule.GetSlots(tomorrow.AddDays(day), now,
            TestDriveBooking.Duration)).First();
        var path = $"/api/vehicles/{listing.Revision.VehicleId}/test-drives";
        var request = new TestDriveBookingRequest(Guid.NewGuid(), listing.Publication.Id, slot.StartsAt);

        await LoginAsync(client, data.CustomerEmail);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(client, path, request, data.OwnerId)).StatusCode);

        var otherBranch = Guid.NewGuid();
        await services.GetRequiredService<BranchProvisioningService>().ProvisionBranchAsync(
            DevelopmentFixtureManifest.Branches[1] with { Id = otherBranch }, CancellationToken.None);
        var elsewhere = await ProvisionAsync(services, otherBranch, AccountRoles.SalesAgent);
        var elsewhereEmail = await database.Users.Where(item => item.Id == elsewhere).Select(item => item.Email).SingleAsync();
        await LoginAsync(client, elsewhereEmail!);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(client, path, request, data.OwnerId)).StatusCode);
        Assert.False(await database.TestDriveBookings.AnyAsync());

        await LoginAsync(client, data.StaffEmail);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, path, request, Guid.Empty)).StatusCode);
        var accepted = await Post(client, path, request, data.OwnerId);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var booking = await database.TestDriveBookings.AsNoTracking().SingleAsync();
        Assert.Equal(data.OwnerId, booking.CustomerId);
        Assert.Equal(data.StaffId, booking.BookedByStaffId);
        var confirmation = await database.TestDriveMailOutbox.AsNoTracking()
            .SingleAsync(item => item.BookingId == booking.Id && item.Kind == TestDriveMailKind.Confirmation);
        Assert.Equal(data.CustomerEmail, confirmation.RecipientEmail);
        Assert.Equal(HttpStatusCode.OK, (await Post(client, path, request, data.OwnerId)).StatusCode);
        Assert.Equal(1, await database.TestDriveBookings.CountAsync());
    }

    private DealershipDbContext NewContext() => new(new DbContextOptionsBuilder<DealershipDbContext>()
        .UseNpgsql(fixture.Database.RuntimeConnectionString).Options);

    private static async Task<HttpResponseMessage> Post(HttpClient client, string path, TestDriveBookingRequest request,
        Guid customerId)
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
