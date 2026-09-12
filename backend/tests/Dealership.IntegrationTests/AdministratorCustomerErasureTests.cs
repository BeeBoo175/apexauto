using System.Net;
using System.Net.Http.Json;
using Dealership.Application.Accounts;
using Dealership.Domain.Accounts;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

public sealed class AdministratorCustomerErasureTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    private const string AdministratorPassword = "Password123!";

    [Fact]
    public async Task AnAdministratorErasesACustomerWithTheirOwnPasswordAndAReason()
    {
        using var factory = fixture.CreateFactory();
        using var admin = factory.CreateClient();
        await StaffInvitationTests.SignInAdministratorAsync(factory, admin);
        using var customer = factory.CreateClient();
        var registration = await IntakePhotoFixture.RegisterAsync(customer);
        var customerId = await CustomerIdAsync(factory, registration.Email!);
        var operationId = Guid.NewGuid();
        var path = $"/api/accounts/{customerId}/erasures";

        var preview = await admin.GetAsync($"/api/accounts/{customerId}/erasure-blockers");
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var wrong = await admin.PostAsJsonAsync(path,
            new { operationId, reason = "Subject request", password = "Wrong-Password1!" });
        Assert.Equal(HttpStatusCode.Conflict, wrong.StatusCode);
        Assert.Contains("wrong-password", await wrong.Content.ReadAsStringAsync());
        // The customer cannot read the Administrator preview. The command shares the five-attempt login window,
        // so this test keeps to three calls after its two sign-ins.
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync($"/api/accounts/{customerId}/erasure-blockers")).StatusCode);

        var erased = await admin.PostAsJsonAsync(path,
            new { operationId, reason = "Subject request", password = AdministratorPassword });
        Assert.Equal(HttpStatusCode.OK, erased.StatusCode);
        var receipt = (await erased.Content.ReadFromJsonAsync<CustomerClosureReceipt>())!;
        Assert.Equal(operationId, receipt.ReceiptId);

        // Replaying the same operation returns the receipt.
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync(path,
            new { operationId, reason = "Subject request", password = AdministratorPassword })).StatusCode);

        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var user = await database.Users.AsNoTracking().SingleAsync(item => item.Id == customerId);
        Assert.NotNull(user.ClosedAt);
        Assert.Null(user.Email);
        var erasure = await database.AccountEvents.AsNoTracking().SingleAsync(item => item.Id == operationId);
        Assert.Equal(AccountEventKind.Erased, erasure.Kind);
        Assert.NotEqual(customerId, erasure.ActorId);
        Assert.Null(erasure.Reason);
        Assert.NotNull(erasure.MinimizedAt);
    }

    private static async Task<Guid> CustomerIdAsync(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory, string email)
    {
        using var scope = factory.Services.CreateScope();
        var normalized = email.ToUpperInvariant();
        return await scope.ServiceProvider.GetRequiredService<DealershipDbContext>().Users
            .Where(user => user.NormalizedEmail == normalized).Select(user => user.Id).SingleAsync();
    }
}
