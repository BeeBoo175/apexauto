using System.Net;
using Dealership.Application.Accounts;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Dealership.IntegrationTests.ReservationDepositRequestTests;

namespace Dealership.IntegrationTests;

// Its own class: the deposit scenario provisions one branch per class database.
public sealed class AdversarialClosureRaceTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task ClosingAnAccountWhileStaffRequestADepositForItNeverStrandsAnObligation()
    {
        // Guarantee: closure and a new deposit request serialize on the customer, so a closed account never has a
        // pending request and a request that was accepted blocks the closure.
        using var scenario = await ReservationDepositScenario.CreateAsync(fixture);
        await LoginAsync(scenario.Client, scenario.Data.StaffEmail);
        using var scope = scenario.Factory.Services.CreateScope();
        var closures = scope.ServiceProvider.GetRequiredService<CustomerClosureService>();
        var owner = scenario.Data.OwnerId;

        var closing = Task.Run(() => closures.CloseAsync(owner, new CustomerClosureRequest(Guid.NewGuid(), "Password123!"),
            CancellationToken.None));
        var requesting = Post(scenario.Client, scenario.RequestPath, scenario.Request, owner);
        var closingResult = await closing;
        var requestingResult = await requesting;

        var closed = await scenario.Database.Users.AsNoTracking().AnyAsync(user => user.Id == owner && user.ClosedAt != null);
        var requests = await scenario.Database.ReservationDepositRequests.AsNoTracking()
            .CountAsync(item => item.CustomerId == owner);
        Assert.False(closed && requests > 0, "A closed account must not keep a deposit request.");
        Assert.False(closed && requestingResult.StatusCode == HttpStatusCode.OK);
        Assert.False(!closed && closingResult.Status == CustomerClosureStatus.Closed);
        if (requests > 0)
        {
            Assert.Equal(CustomerClosureStatus.Blocked, closingResult.Status);
        }

        // Whatever won, a second closure attempt gives the same durable answer and never a server error.
        var again = await closures.CloseAsync(owner, new CustomerClosureRequest(Guid.NewGuid(), "Password123!"),
            CancellationToken.None);
        Assert.Equal(closed ? CustomerClosureStatus.Forbidden : CustomerClosureStatus.Blocked, again.Status);
    }
}
