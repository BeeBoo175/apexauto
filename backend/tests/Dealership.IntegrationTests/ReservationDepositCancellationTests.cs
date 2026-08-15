using System.Net;
using System.Net.Http.Json;
using Dealership.Application.Vehicles;
using Dealership.Infrastructure.Vehicles;
using Microsoft.EntityFrameworkCore;
using static Dealership.IntegrationTests.ReservationDepositRequestTests;

namespace Dealership.IntegrationTests;

public sealed class ReservationDepositCancellationTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task StaffCancelAPendingRequestWhichTheListingShowsUntilThenAndReleasesTheCar()
    {
        using var scenario = await ReservationDepositScenario.CreateAsync(fixture);
        await LoginAsync(scenario.Client, scenario.Data.StaffEmail);
        Assert.Equal(HttpStatusCode.OK,
            (await Post(scenario.Client, scenario.RequestPath, scenario.Request, scenario.Data.OwnerId)).StatusCode);
        var deposit = await scenario.Database.ReservationDepositRequests.AsNoTracking().SingleAsync();
        var cancelPath = $"/api/reservation-requests/{deposit.Id}/cancellations";
        var token = scenario.Tokens.Create(deposit);

        // The listing read carries the pending request, with its customer and deadline, for the timer.
        var workspace = (await new ListingReadStore(scenario.Database)
            .WorkspaceAsync(scenario.ManagerId, scenario.VehicleId, CancellationToken.None)).Workspace!;
        Assert.Equal(deposit.Id, workspace.PendingDeposit!.RequestId);
        Assert.Equal(deposit.ExpiresAt, workspace.PendingDeposit.ExpiresAtUtc);

        var elsewhere = await scenario.ProvisionOtherBranchStaffEmailAsync();
        await LoginAsync(scenario.Client, elsewhere);
        Assert.Equal(HttpStatusCode.Forbidden, (await scenario.Client.PostAsJsonAsync(cancelPath, new { })).StatusCode);
        await LoginAsync(scenario.Client, scenario.Data.CustomerEmail);
        Assert.Equal(HttpStatusCode.Forbidden, (await scenario.Client.PostAsJsonAsync(cancelPath, new { })).StatusCode);

        await LoginAsync(scenario.Client, scenario.Data.StaffEmail);
        Assert.Equal(HttpStatusCode.NotFound,
            (await scenario.Client.PostAsJsonAsync($"/api/reservation-requests/{Guid.NewGuid()}/cancellations", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await scenario.Client.PostAsJsonAsync(cancelPath, new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await scenario.Client.PostAsJsonAsync(cancelPath, new { })).StatusCode);

        // The cancelled request no longer holds the car, shows on the listing or confirms.
        workspace = (await new ListingReadStore(scenario.Database)
            .WorkspaceAsync(scenario.ManagerId, scenario.VehicleId, CancellationToken.None)).Workspace!;
        Assert.Null(workspace.PendingDeposit);
        Assert.Equal(HttpStatusCode.Gone, (await Confirm(scenario.Client, token)).StatusCode);
        var other = await scenario.RegisterOtherCustomerAsync();
        Assert.Equal(HttpStatusCode.OK, (await Post(scenario.Client, scenario.ReservePath,
            scenario.Request with { OperationId = Guid.NewGuid() }, other)).StatusCode);
        Assert.Equal(1, await scenario.Database.Reservations.CountAsync());
    }
}
