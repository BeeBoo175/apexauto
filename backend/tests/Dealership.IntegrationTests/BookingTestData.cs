using System.Net;
using System.Net.Http.Json;
using Dealership.Application.Accounts;
using Dealership.Application.Branches;
using Dealership.Application.Intake;
using Dealership.Application.Vehicles;
using Dealership.Domain.Intake;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

internal static class BookingTestData
{
    public static async Task<BookingRequest> RequestAsync(WebApplicationFactory<Program> factory, HttpClient client)
    {
        using var scope = factory.Services.CreateScope();
        var branch = DevelopmentFixtureManifest.Branches[0];
        await scope.ServiceProvider.GetRequiredService<BranchProvisioningService>()
            .ProvisionBranchAsync(branch, CancellationToken.None);
        var make = VehicleTaxonomy.Makes[0];
        var model = make.Models[0];
        var vehicle = new ValuationRequest(make.Name, model.Name, model.Trims[0].Name, 2020, 90000);
        var quote = await (await client.PostAsJsonAsync("/api/valuations", vehicle)).Content.ReadFromJsonAsync<ValuationResponse>();
        var schedule = scope.ServiceProvider.GetRequiredService<InspectionSchedule>();
        var now = DateTimeOffset.UtcNow;
        var date = schedule.GetDateWindow(now).FirstDate.AddDays(1);
        var slots = schedule.GetSlots(date, now);

        while (slots.Count == 0)
        {
            date = date.AddDays(1);
            slots = schedule.GetSlots(date, now);
        }

        return new BookingRequest(Guid.NewGuid(), branch.Id, slots[0].StartsAt, "WYY 8821", vehicle, quote,
            await PhotosAsync(client));
    }

    public static async Task<Guid[]> PhotosAsync(HttpClient client)
    {
        var ids = new List<Guid>();

        foreach (var angle in Enum.GetValues<PhotoAngle>())
        {
            var id = Guid.NewGuid();
            Assert.Equal(HttpStatusCode.OK, (await IntakePhotoFixture.UploadAsync(client, id,
                IntakePhotoFixture.Image(), angle.ToString())).StatusCode);
            ids.Add(id);
        }

        return [.. ids];
    }

    public static async Task<Guid> OwnerAsync(WebApplicationFactory<Program> factory, RegistrationRequest registration)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<DealershipDbContext>().Users
            .Where(user => user.Email == registration.Email).Select(user => user.Id).SingleAsync();
    }

    public static async Task<AppointmentReceipt> BookAsync(HttpClient client, BookingRequest request)
    {
        var response = await client.PostAsJsonAsync("/api/intake/appointments", request);
        Assert.True(response.IsSuccessStatusCode, $"Booking returned {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        Assert.True(response.Headers.CacheControl?.NoStore);
        return (await response.Content.ReadFromJsonAsync<AppointmentReceipt>())!;
    }
}
