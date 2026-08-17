using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dealership.Application.Accounts;
using Dealership.Application.Branches;
using Dealership.Application.Intake;
using Dealership.Application.Vehicles;
using Dealership.Domain.Common;
using Dealership.Domain.Intake;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Intake;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Dealership.IntegrationTests.ReservationTestData;

namespace Dealership.IntegrationTests;

public sealed class StaffAssistedIntakeTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task SalesStaffUploadPhotosAndBookAnIntakeForACustomerAtTheirBranchAndTheCustomerIsStillEmailed()
    {
        using var data = await CheckInTestData.CreateAsync(fixture);
        using var scope = data.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        await using var database = NewContext();
        // A separate factory keeps the real clock and configured schedule, so tokens and slots are valid.
        using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var photoIds = Enum.GetValues<PhotoAngle>().Select(_ => Guid.NewGuid()).ToArray();

        await LoginAsync(client, data.CustomerEmail);
        Assert.Equal(HttpStatusCode.Forbidden, (await UploadAsync(client, photoIds[0], PhotoAngle.Driver, data.OwnerId)).StatusCode);

        var elsewhere = await ProvisionOtherBranchStaffAsync(services, database);
        await LoginAsync(client, elsewhere);
        var request = BookingFor(factory.Services, data, photoIds);
        Assert.Equal(HttpStatusCode.Forbidden, (await BookAsync(client, request, data.OwnerId)).StatusCode);
        Assert.False(await database.IntakeAppointments.AnyAsync());

        await LoginAsync(client, data.StaffEmail);
        Assert.Equal(HttpStatusCode.BadRequest, (await UploadAsync(client, photoIds[0], PhotoAngle.Driver, Guid.Empty)).StatusCode);

        foreach (var (id, angle) in photoIds.Zip(Enum.GetValues<PhotoAngle>()))
        {
            Assert.Equal(HttpStatusCode.OK, (await UploadAsync(client, id, angle, data.OwnerId)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.BadRequest, (await BookAsync(client, request, Guid.Empty)).StatusCode);
        var accepted = await BookAsync(client, request, data.OwnerId);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var receipt = (await accepted.Content.ReadFromJsonAsync<AppointmentReceipt>())!;

        var appointment = await database.IntakeAppointments.AsNoTracking().SingleAsync();
        Assert.Equal(receipt.Id, appointment.Id);
        Assert.Equal(data.OwnerId, appointment.OwnerId);
        Assert.Equal(data.StaffId, appointment.BookedByStaffId);
        var confirmation = await database.AppointmentMailOutbox.AsNoTracking()
            .SingleAsync(item => item.AppointmentId == appointment.Id && item.Kind == AppointmentMailKind.Confirmation);
        Assert.Equal(data.CustomerEmail, confirmation.RecipientEmail);
        Assert.Equal(HttpStatusCode.OK, (await BookAsync(client, request, data.OwnerId)).StatusCode);
        Assert.Equal(1, await database.IntakeAppointments.CountAsync());
    }

    private DealershipDbContext NewContext() => new(new DbContextOptionsBuilder<DealershipDbContext>()
        .UseNpgsql(fixture.Database.RuntimeConnectionString).Options);

    private static BookingRequest BookingFor(IServiceProvider services, CheckInTestData data, Guid[] photoIds)
    {
        var make = VehicleTaxonomy.Makes[0];
        var model = make.Models[0];
        var vehicle = new ValuationRequest(make.Name, model.Name, model.Trims[0].Name, 2020, 90000);
        var quote = services.GetRequiredService<ValuationService>().Calculate(vehicle).Quote!;
        var now = DateTimeOffset.UtcNow;
        var schedule = services.GetRequiredService<AppointmentSchedule>();
        var tomorrow = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, DealershipLocale.Malaysia).DateTime).AddDays(1);
        var startsAt = Enumerable.Range(0, 7).SelectMany(day => schedule.GetSlots(tomorrow.AddDays(day), now)).First().StartsAt;
        return new BookingRequest(Guid.NewGuid(), data.BranchId, startsAt, "WYY 8821", vehicle, quote, photoIds);
    }

    private static async Task<string> ProvisionOtherBranchStaffAsync(IServiceProvider services, DealershipDbContext database)
    {
        var otherBranch = Guid.NewGuid();
        await services.GetRequiredService<BranchProvisioningService>().ProvisionBranchAsync(
            DevelopmentFixtureManifest.Branches[1] with { Id = otherBranch }, CancellationToken.None);
        var staff = await ProvisionAsync(services, otherBranch, AccountRoles.SalesAgent);
        return (await database.Users.Where(item => item.Id == staff).Select(item => item.Email).SingleAsync())!;
    }

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient client, Guid id, PhotoAngle angle, Guid customerId)
    {
        using var body = new ByteArrayContent(IntakePhotoFixture.Image());
        body.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        using var message = new HttpRequestMessage(HttpMethod.Put, $"/api/intake/photos/{id}?angle={angle}") { Content = body };
        message.Headers.Add("X-On-Behalf-Of", customerId.ToString());
        return await client.SendAsync(message);
    }

    private static async Task<HttpResponseMessage> BookAsync(HttpClient client, BookingRequest request, Guid customerId)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/intake/appointments")
        {
            Content = JsonContent.Create(request)
        };
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
