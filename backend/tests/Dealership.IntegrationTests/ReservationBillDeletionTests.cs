using System.Net;
using Dealership.Application.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Dealership.IntegrationTests.DepositPaymentSupport;
using static Dealership.IntegrationTests.ReservationDepositRequestTests;

namespace Dealership.IntegrationTests;

public sealed class ReservationBillDeletionTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task ABillIsMarkedDeletedOnlyWhenTheProviderConfirmsTheDeletion()
    {
        using var scenario = await ReservationDepositScenario.CreateAsync(fixture);
        var gateway = new FakePaymentGateway(Key);
        using var paying = Paying(scenario, gateway);
        using var client = paying.CreateClient();
        var token = await RequestAsync(scenario);
        Assert.Equal(HttpStatusCode.OK, (await Confirm(client, token)).StatusCode);
        var requestId = (await scenario.Database.ReservationDepositRequests.AsNoTracking().SingleAsync()).Id;
        using var scope = paying.Services.CreateScope();
        var payments = scope.ServiceProvider.GetRequiredService<ReservationPaymentService>();

        gateway.DeleteFails = true;
        await payments.ReleaseBillAsync(requestId, CancellationToken.None);
        Assert.Null((await scenario.Database.ReservationDepositPayments.AsNoTracking().SingleAsync()).BillDeletedAt);

        gateway.DeleteFails = false;
        await payments.ReleaseBillAsync(requestId, CancellationToken.None);
        Assert.NotNull((await scenario.Database.ReservationDepositPayments.AsNoTracking().SingleAsync()).BillDeletedAt);
    }
}
