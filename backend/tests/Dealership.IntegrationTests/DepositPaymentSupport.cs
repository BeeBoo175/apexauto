using System.Net;
using System.Net.Http.Json;
using Dealership.Application.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Dealership.IntegrationTests.ReservationDepositRequestTests;

namespace Dealership.IntegrationTests;

internal static class DepositPaymentSupport
{
    public const string Key = "test-x-signature-key";

    internal static Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> Paying(
        ReservationDepositScenario scenario, FakePaymentGateway gateway)
    {
        return scenario.Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPaymentGateway>();
            services.AddSingleton<IPaymentGateway>(gateway);
        }));
    }

    internal static async Task<string> RequestAsync(ReservationDepositScenario scenario)
    {
        await LoginAsync(scenario.Client, scenario.Data.StaffEmail);
        var accepted = await Post(scenario.Client, scenario.RequestPath, scenario.Request, scenario.Data.OwnerId);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        return scenario.Tokens.Create(await scenario.Database.ReservationDepositRequests.AsNoTracking().SingleAsync());
    }

    internal static async Task<string> StatusAsync(HttpClient client, Guid requestId)
    {
        var response = await client.GetFromJsonAsync<PaymentStatus>($"/api/reservation-deposits/{requestId}/status");
        return response!.Status;
    }

    internal static Task<HttpResponseMessage> Callback(HttpClient client, Dictionary<string, string> fields)
    {
        return client.PostAsync("/api/payments/billplz/callback", new FormUrlEncodedContent(fields));
    }

    internal sealed record PaymentAddress(string? PaymentUrl);

    internal sealed record PaymentStatus(string Status);
}
