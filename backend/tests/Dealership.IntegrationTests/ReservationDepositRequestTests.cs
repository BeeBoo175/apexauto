using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dealership.Application.Accounts;
using Dealership.Application.Branches;
using Dealership.Application.Vehicles;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Accounts;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Dealership.IntegrationTests.ReservationTestData;

namespace Dealership.IntegrationTests;

public sealed class ReservationDepositRequestTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task StaffRequestAHeldDepositAndTheCustomerConfirmsItIntoTheOrdinaryReservation()
    {
        using var scenario = await ReservationDepositScenario.CreateAsync(fixture);
        var path = scenario.RequestPath;
        var request = scenario.Request;

        await LoginAsync(scenario.Client, scenario.Data.CustomerEmail);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(scenario.Client, path, request, scenario.Data.OwnerId)).StatusCode);

        var elsewhere = await scenario.ProvisionOtherBranchStaffEmailAsync();
        await LoginAsync(scenario.Client, elsewhere);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(scenario.Client, path, request, scenario.Data.OwnerId)).StatusCode);
        Assert.False(await scenario.Database.ReservationDepositRequests.AnyAsync());

        await LoginAsync(scenario.Client, scenario.Data.StaffEmail);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(scenario.Client, path, request, Guid.Empty)).StatusCode);
        var accepted = await Post(scenario.Client, path, request, scenario.Data.OwnerId);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);

        // Nothing is reserved or paid yet: only the request holds the car, and the customer is emailed the request.
        var deposit = await scenario.Database.ReservationDepositRequests.AsNoTracking().SingleAsync();
        Assert.Equal(scenario.Data.StaffId, deposit.BookedByStaffId);
        Assert.Equal(deposit.CreatedAt.AddHours(24), deposit.ExpiresAt);
        Assert.Null(deposit.ConfirmedAt);
        Assert.False(await scenario.Database.Reservations.AnyAsync());
        var mail = await scenario.Database.ReservationDepositMailOutbox.AsNoTracking().SingleAsync();
        Assert.Equal(scenario.Data.CustomerEmail, mail.RecipientEmail);
        Assert.Equal(HttpStatusCode.OK, (await Post(scenario.Client, path, request, scenario.Data.OwnerId)).StatusCode);
        Assert.Equal(1, await scenario.Database.ReservationDepositRequests.CountAsync());

        // A customer with a request waiting is not offered for another reservation, but intake has no such limit.
        var lookup = new StaffAssistanceQuery(scenario.Database);
        var forReservation = await lookup.SearchCustomersAsync(scenario.Data.StaffId,
            new("Nur Aina", CustomerLookupPurpose.Reservation), CancellationToken.None);
        Assert.DoesNotContain(forReservation.Items!, row => row.Id == scenario.Data.OwnerId);
        var forIntake = await lookup.SearchCustomersAsync(scenario.Data.StaffId,
            new("Nur Aina", CustomerLookupPurpose.Intake), CancellationToken.None);
        Assert.Contains(forIntake.Items!, row => row.Id == scenario.Data.OwnerId);

        // The pending request holds the car against anyone else.
        var other = await scenario.RegisterOtherCustomerAsync();
        var reserveAsOther = await Post(scenario.Client, scenario.ReservePath,
            request with { OperationId = Guid.NewGuid() }, other, assisted: true);
        Assert.Equal(HttpStatusCode.Conflict, reserveAsOther.StatusCode);
        Assert.False(await scenario.Database.Reservations.AnyAsync());

        var token = scenario.Tokens.Create(deposit);
        Assert.Equal(HttpStatusCode.BadRequest, (await Confirm(scenario.Client, Tampered(token))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Confirm(scenario.Client, token)).StatusCode);

        // Confirming is the ordinary reservation, booked by the same staff member, with the ordinary receipt mail.
        var reservation = await scenario.Database.Reservations.AsNoTracking().SingleAsync();
        Assert.Equal(deposit.Id, reservation.Id);
        Assert.Equal(scenario.Data.OwnerId, reservation.CustomerId);
        Assert.Equal(scenario.Data.StaffId, reservation.BookedByStaffId);
        Assert.Equal(1, await scenario.Database.ReservationMailOutbox.CountAsync(item => item.ReservationId == reservation.Id));
        Assert.NotNull((await scenario.Database.ReservationDepositRequests.AsNoTracking().SingleAsync()).ConfirmedAt);
        Assert.Equal(HttpStatusCode.NoContent, (await Confirm(scenario.Client, token)).StatusCode);
        Assert.Equal(1, await scenario.Database.Reservations.CountAsync());
    }

    private static string Tampered(string token)
    {
        return token[..^1] + (token[^1] == '0' ? '1' : '0');
    }

    internal static async Task<HttpResponseMessage> Post(HttpClient client, string path, ReservationRequest request,
        Guid customerId, bool assisted = true)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(request) };
        if (assisted)
        {
            message.Headers.Add("X-On-Behalf-Of", customerId.ToString());
        }

        return await client.SendAsync(message);
    }

    internal static Task<HttpResponseMessage> Confirm(HttpClient client, string token)
    {
        return client.PostAsJsonAsync("/api/reservation-deposits/confirmations", new { token });
    }

    internal static async Task LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(Guid.NewGuid(), email, "Password123!"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = (await response.Content.ReadFromJsonAsync<LoginSession>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
    }
}
