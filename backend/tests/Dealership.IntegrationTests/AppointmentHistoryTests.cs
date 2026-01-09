using System.Net;
using System.Net.Http.Json;
using Dealership.Application.Branches;
using Dealership.Application.Intake;
using Dealership.Domain.Intake;
using Dealership.Infrastructure.Development;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

public sealed class AppointmentHistoryTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task HistoryScopesFiltersAndPagesCanonicalBookingsAndSurvivesRestart()
    {
        using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var registration = await IntakePhotoFixture.RegisterAsync(client);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/intake/appointments")).StatusCode);
        await IntakePhotoFixture.VerifyAsync(factory, client, registration);
        var owner = await BookingTestData.OwnerAsync(factory, registration);
        var request = await BookingTestData.RequestAsync(factory, client);
        var appointments = new List<AppointmentReceipt>();
        using (var scope = factory.Services.CreateScope())
        {
            var schedule = scope.ServiceProvider.GetRequiredService<InspectionSchedule>();
            var slots = Enumerable.Range(1, 20).SelectMany(day => schedule.GetSlots(
                schedule.GetDateWindow(DateTimeOffset.UtcNow).FirstDate.AddDays(day), DateTimeOffset.UtcNow)).Take(41);

            foreach (var slot in slots)
            {
                using var bookingScope = factory.Services.CreateScope();
                var photos = bookingScope.ServiceProvider.GetRequiredService<IntakePhotoService>();
                var ids = new List<Guid>();

                foreach (var angle in Enum.GetValues<PhotoAngle>())
                {
                    var id = Guid.NewGuid();
                    using var image = new MemoryStream(IntakePhotoFixture.Image());
                    Assert.Equal(PhotoStatus.Available, (await photos.UploadAsync(owner, id, angle, "image/jpeg", image, CancellationToken.None)).Status);
                    ids.Add(id);
                }

                var result = await bookingScope.ServiceProvider.GetRequiredService<IntakeBookingService>()
                    .BookAsync(owner, request with { Id = Guid.NewGuid(), StartsAt = slot.StartsAt, PhotoIds = ids }, CancellationToken.None);
                Assert.Equal(BookingStatus.Succeeded, result.Status);
                appointments.Add(result.Receipt!);
            }

            var secondBranch = DevelopmentFixtureManifest.Branches[0] with { Id = Guid.NewGuid(), Name = "ApexAuto Petaling Jaya" };
            await scope.ServiceProvider.GetRequiredService<BranchProvisioningService>().ProvisionBranchAsync(secondBranch, CancellationToken.None);
            appointments.Add(await BookingTestData.BookAsync(client, request with { BranchId = secondBranch.Id }));
        }

        Assert.Equal(42, appointments.Count);
        var expected = appointments.OrderByDescending(item => item.Slot.StartsAt).ThenByDescending(item => item.Id).Select(item => item.Id).ToArray();
        var actual = new List<Guid>();

        for (var page = 1; page <= 3; page++)
        {
            var response = await client.GetAsync($"/api/intake/appointments?page={page}");
            Assert.True(response.Headers.CacheControl?.NoStore);
            var result = (await response.Content.ReadFromJsonAsync<AppointmentHistoryPage>())!;
            Assert.Equal(42, result.Total);
            Assert.Equal(page == 3 ? 2 : 20, result.Items.Count);
            actual.AddRange(result.Items.Select(item => item.Id));
        }

        Assert.Equal(expected, actual);
        var beyond = (await client.GetFromJsonAsync<AppointmentHistoryPage>("/api/intake/appointments?page=4"))!;
        Assert.Empty(beyond.Items);
        Assert.Equal(42, beyond.Total);
        var date = DateOnly.FromDateTime(request.StartsAt.AddHours(8).DateTime);
        var filtered = (await client.GetFromJsonAsync<AppointmentHistoryPage>($"/api/intake/appointments?from={date:yyyy-MM-dd}&to={date:yyyy-MM-dd}"))!;
        Assert.Equal(appointments.Count(item => DateOnly.FromDateTime(item.Slot.StartsAt.AddHours(8).DateTime) == date), filtered.Total);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/intake/appointments?from={date:yyyy-MM-dd}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/intake/appointments?pageSize=51")).StatusCode);
        using var other = factory.CreateClient();
        var outsider = await IntakePhotoFixture.RegisterAsync(other);
        await IntakePhotoFixture.VerifyAsync(factory, other, outsider);
        var empty = (await other.GetFromJsonAsync<AppointmentHistoryPage>("/api/intake/appointments"))!;
        Assert.Empty(empty.Items);
        Assert.Equal(0, empty.Total);

        using var restarted = fixture.CreateFactory();
        using var restartedClient = restarted.CreateClient();
        await IntakePhotoFixture.LoginAsync(restartedClient, registration);
        var again = (await restartedClient.GetFromJsonAsync<AppointmentHistoryPage>("/api/intake/appointments"))!;
        Assert.Equal(expected.Take(20), again.Items.Select(item => item.Id));
    }
}
