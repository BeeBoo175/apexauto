using Dealership.Application.Accounts;
using Microsoft.Extensions.DependencyInjection;
using static Dealership.IntegrationTests.DepositPaymentSupport;
using static Dealership.IntegrationTests.ReservationDepositRequestTests;

namespace Dealership.IntegrationTests;

public sealed class CustomerClosureBlockerTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task APendingDepositRequestBlocksClosureAndAnUnknownAccountHasNoBlockers()
    {
        using var scenario = await ReservationDepositScenario.CreateAsync(fixture);
        await RequestAsync(scenario);
        using var scope = scenario.Factory.Services.CreateScope();
        var closures = scope.ServiceProvider.GetRequiredService<CustomerClosureService>();

        var blockers = await closures.BlockersAsync(scenario.Data.OwnerId, CancellationToken.None);

        var blocker = Assert.Single(blockers!);
        Assert.Equal(ClosureBlockerTypes.DepositRequest, blocker.Type);
        var refused = await closures.CloseAsync(scenario.Data.OwnerId,
            new CustomerClosureRequest(Guid.NewGuid(), "Password123!"), CancellationToken.None);
        Assert.Equal(CustomerClosureStatus.Blocked, refused.Status);
        Assert.Null(await closures.BlockersAsync(Guid.NewGuid(), CancellationToken.None));
    }
}
