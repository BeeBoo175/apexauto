using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dealership.Application.Accounts;
using Dealership.Application.Branches;
using Dealership.Application.Vehicles;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Dealership.IntegrationTests.ReservationTestData;

namespace Dealership.IntegrationTests;

public sealed class ReservationDepositLapseTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task AnUnconfirmedRequestLapsesAfterTwentyFourHoursAndReleasesTheCar()
    {
        using var scenario = await ReservationDepositScenario.CreateAsync(fixture);
        await ReservationDepositRequestTests.LoginAsync(scenario.Client, scenario.Data.StaffEmail);
        Assert.Equal(HttpStatusCode.OK,
            (await ReservationDepositRequestTests.Post(scenario.Client, scenario.RequestPath, scenario.Request, scenario.Data.OwnerId)).StatusCode);
        var deposit = await scenario.Database.ReservationDepositRequests.AsNoTracking().SingleAsync();

        await scenario.Database.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "ReservationDepositRequests" SET "CreatedAt" = "CreatedAt" - interval '25 hours',
                "ExpiresAt" = "ExpiresAt" - interval '25 hours' WHERE "Id" = {deposit.Id}
            """);

        // The lapsed request no longer holds the car, so another customer may reserve it.
        var other = await scenario.RegisterOtherCustomerAsync();
        var reserved = await scenario.ReserveAsCustomerAsync(other, scenario.Request with { OperationId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.OK, reserved.StatusCode);

        var lapsed = await ReservationDepositRequestTests.Confirm(scenario.Client, scenario.Tokens.Create(
            await scenario.Database.ReservationDepositRequests.AsNoTracking().SingleAsync()));
        Assert.Equal(HttpStatusCode.Gone, lapsed.StatusCode);
        Assert.Equal(1, await scenario.Database.Reservations.CountAsync());
    }
}
