using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dealership.Application.Accounts;
using Dealership.Application.Sales;
using Dealership.Domain.Sales;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

// A delivered sale outlives its customer's account, but staff may no longer read or rewrite the buyer's personal
// details through the settlement review or a document correction.
public sealed class SaleClosedCustomerTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task SettlementReviewAndCorrectionAnswerCustomerClosedAfterTheBuyerClosesTheirAccount()
    {
        var world = await SaleWorld.SharedAsync(fixture);
        var hold = await world.HoldAsync();
        Assert.Equal(SaleResultStatus.Succeeded,
            (await world.Handling.ClaimAsync(world.Agent, hold.Id, new(Guid.NewGuid(), 1), default)).Status);
        Assert.Equal(SaleResultStatus.Succeeded,
            (await world.Terms.SubmitAsync(world.Agent, hold.Id, new(Guid.NewGuid(), 2), default)).Status);
        await using var database = NewContext();
        var sale = await database.SalesOrders.AsNoTracking().SingleAsync(item => item.Id == hold.Id);
        var profile = await database.CustomerProfiles.SingleAsync(item => item.UserId == sale.CustomerId);
        var vehicle = await database.Vehicles.SingleAsync(item => item.Id == sale.VehicleId);
        var review = SaleSettlementReview.From(profile, SaleReviewedVehicle.Of(vehicle));
        var payment = new SalePaymentEvidence(SalePaymentMethod.Cash, sale.BalancePayable, "CASH 43880",
            new DateOnly(2026, 11, 2), null, 0m, null, false, false);
        Assert.Equal(SaleResultStatus.Succeeded, (await world.Settlement.SettleAsync(world.Agent, hold.Id,
            new(Guid.NewGuid(), 3, payment, review), default)).Status);
        Assert.Equal(SaleResultStatus.Succeeded, (await world.Services.GetRequiredService<SaleHandoverService>()
            .DeliverAsync(world.Agent, hold.Id,
                new(Guid.NewGuid(), 4, new(vehicle.MileageKilometres, 2, true, true, true)), default)).Status);
        var original = await database.SaleDocuments.AsNoTracking().SingleAsync(item =>
            item.SalesOrderId == hold.Id && item.Kind == SaleDocumentKind.PurchaseOrder);

        var email = await database.Users.Where(item => item.Id == sale.CustomerId).Select(item => item.Email).SingleAsync();
        using var factory = fixture.CreateFactory();
        using var customer = factory.CreateClient();
        await LoginAsync(customer, email!);
        var closed = await customer.PostAsJsonAsync("/api/account/closure",
            new { operationId = Guid.NewGuid(), password = "Password123!" });
        Assert.True(closed.StatusCode == HttpStatusCode.OK, await closed.Content.ReadAsStringAsync());

        using var manager = factory.CreateClient();
        await LoginAsync(manager, (await database.Users.Where(item => item.Id == world.Manager)
            .Select(item => item.Email).SingleAsync())!);
        var reviewResponse = await manager.GetAsync($"/api/staff/sales/{hold.Id}/settlement-review");
        await AssertClosedAsync(reviewResponse);

        var correction = await manager.PostAsJsonAsync($"/api/staff/sales/{hold.Id}/document-corrections", new
        {
            operationId = Guid.NewGuid(),
            expectedVersion = 5,
            documentId = original.Id,
            buyerName = "Hafiz Rahman",
            buyerAddress = "27 Jalan Meranti, Petaling Jaya, Selangor",
            reason = "Correct buyer address spelling"
        });
        await AssertClosedAsync(correction);
        Assert.Equal(1, await database.SaleDocuments.CountAsync(item =>
            item.SalesOrderId == hold.Id && item.Kind == SaleDocumentKind.PurchaseOrder));
    }

    private static async Task AssertClosedAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Conflict, body);
        Assert.Contains("customer-closed", body);
    }

    private static async Task LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(Guid.NewGuid(), email, "Password123!"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = (await response.Content.ReadFromJsonAsync<LoginSession>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
    }

    private DealershipDbContext NewContext() => new(new DbContextOptionsBuilder<DealershipDbContext>()
        .UseNpgsql(fixture.Database.RuntimeConnectionString).Options);
}
