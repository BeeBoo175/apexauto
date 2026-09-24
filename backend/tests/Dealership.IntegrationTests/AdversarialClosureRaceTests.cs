using System.Net;
using Dealership.Application.Accounts;
using Dealership.Application.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Dealership.IntegrationTests.ReservationDepositRequestTests;
using static Dealership.IntegrationTests.ReservationTestData;

namespace Dealership.IntegrationTests;

// Its own class: the deposit scenario provisions one branch per class database.
public sealed class AdversarialClosureRaceTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task ClosingAnAccountWhileStaffRequestADepositForItNeverStrandsAnObligation()
    {
        // Guarantee: closure and a new deposit request serialize on the customer, so a closed account never has a
        // pending request and a request that was accepted blocks the closure.
        // The scenario provisions one branch per class database, so every round after the first adds its own customer
        // and listing to it instead of creating another scenario.
        using var scenario = await ReservationDepositScenario.CreateAsync(fixture);
        await LoginAsync(scenario.Client, scenario.Data.StaffEmail);
        using var scope = scenario.Factory.Services.CreateScope();
        using var dataScope = scenario.Data.Factory.Services.CreateScope();
        var closures = scope.ServiceProvider.GetRequiredService<CustomerClosureService>();
        var technician = await ProvisionAsync(dataScope.ServiceProvider, scenario.Data.BranchId, AccountRoles.WorkshopTechnician);

        for (var round = 0; round < 6; round++)
        {
            var owner = scenario.Data.OwnerId;
            var request = scenario.Request;
            var path = scenario.RequestPath;

            if (round > 0)
            {
                owner = await scenario.RegisterOtherCustomerAsync();
                var listing = await CreateListingAsync(scenario.Data, dataScope.ServiceProvider, scenario.ManagerId,
                    technician, round);
                request = new ReservationRequest(Guid.NewGuid(), listing.Publication.Id, null);
                path = $"/api/vehicles/{listing.Revision.VehicleId}/reservation-requests";
            }

            var closing = Task.Run(() => closures.CloseAsync(owner, new CustomerClosureRequest(Guid.NewGuid(), "Password123!"),
                CancellationToken.None));
            var requesting = Post(scenario.Client, path, request, owner);
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
}
