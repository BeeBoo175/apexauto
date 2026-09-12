using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dealership.Application.Accounts;
using Dealership.Application.Intake;
using Dealership.Application.Vehicles;
using Dealership.Domain.Intake;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.IntegrationTests;

// Staff commands that name a customer by id stay reachable after that customer closed their account, so each answers
// with the stable customer-closed code instead of throwing on the removed address.
public sealed class StaffClosedCustomerTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task CheckInAndAssistedCommandsAnswerCustomerClosedForAClosedCustomer()
    {
        using var data = await CheckInTestData.CreateAsync(fixture);
        var appointment = await data.BookAsync();
        await data.ClaimAsync(appointment.Id);
        await using var database = new DealershipDbContext(new DbContextOptionsBuilder<DealershipDbContext>()
            .UseNpgsql(fixture.Database.RuntimeConnectionString).Options);
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "AspNetUsers" SET "ClosedAt" = now(), "IsActive" = false, "PasswordHash" = NULL,
                "EmailConfirmed" = false, "Email" = NULL, "NormalizedEmail" = NULL, "AccountUsername" = NULL,
                "UserName" = NULL, "NormalizedUserName" = NULL
            WHERE "Id" = {data.OwnerId}
            """);
        using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, data.StaffEmail);

        var checkIn = await client.PostAsJsonAsync($"/api/staff/intake/appointments/{appointment.Id}/check-ins",
            CheckInTestData.Evidence);
        await AssertClosedAsync(checkIn);
        Assert.False(await database.AppointmentCheckIns.AnyAsync());

        var upload = await SendAsync(client, HttpMethod.Put, $"/api/intake/photos/{Guid.NewGuid()}?angle={PhotoAngle.Driver}",
            data.OwnerId, new ByteArrayContent(IntakePhotoFixture.Image())
            {
                Headers = { ContentType = new MediaTypeHeaderValue("image/jpeg") }
            });
        await AssertClosedAsync(upload);

        var booking = await SendAsync(client, HttpMethod.Post, "/api/intake/appointments", data.OwnerId,
            JsonContent.Create(new BookingRequest(Guid.NewGuid(), data.BranchId, DateTimeOffset.UtcNow.AddDays(2),
                "WYY 8821", new ValuationRequest("Toyota", "Vios", "1.5 E", 2020, 90000),
                new ValuationResponse(1m, 2m, "MYR", 2026), [])));
        await AssertClosedAsync(booking);

        var deposit = await SendAsync(client, HttpMethod.Post, $"/api/vehicles/{Guid.NewGuid()}/reservation-requests",
            data.OwnerId, JsonContent.Create(new ReservationRequest(Guid.NewGuid(), Guid.NewGuid(), null)));
        await AssertClosedAsync(deposit);

        var testDrive = await SendAsync(client, HttpMethod.Post, $"/api/vehicles/{Guid.NewGuid()}/test-drives",
            data.OwnerId, JsonContent.Create(new
            {
                operationId = Guid.NewGuid(),
                publicationId = Guid.NewGuid(),
                startsAt = DateTimeOffset.UtcNow.AddDays(2)
            }));
        await AssertClosedAsync(testDrive);
    }

    private static async Task AssertClosedAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Conflict, body);
        Assert.Contains("customer-closed", body);
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path,
        Guid customerId, HttpContent content)
    {
        using var message = new HttpRequestMessage(method, path) { Content = content };
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
