using Dealership.Application.Vehicles;
using Dealership.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Dealership.IntegrationTests.DepositPaymentSupport;

namespace Dealership.IntegrationTests;

public sealed class ReservationClosedPaymentInsertTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task APaymentIsRefusedWhenTheCustomerClosedTheAccountAfterTheBillWasCreated()
    {
        using var scenario = await ReservationDepositScenario.CreateAsync(fixture);
        var gateway = new FakePaymentGateway(Key);
        using var paying = Paying(scenario, gateway);
        await RequestAsync(scenario);
        var requestId = (await scenario.Database.ReservationDepositRequests.AsNoTracking().SingleAsync()).Id;
        using var scope = paying.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IReservationPaymentStore>();
        var payment = new ReservationDepositPayment(requestId, "bill-closed-owner", "https://pay.example/bills/closed",
            50000, DateTimeOffset.UtcNow);
        await ClosedOwnerSql.CloseAsync(scenario);

        var result = await store.AddAsync(payment, CancellationToken.None);

        Assert.Equal(DepositPaymentAddResult.Refused, result);
        Assert.False(await scenario.Database.ReservationDepositPayments.AnyAsync());
    }
}
