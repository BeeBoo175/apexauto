using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dealership.Application.Accounts;
using Dealership.Application.Vehicles;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

internal static class PurchaseQuoteAssertions
{
    public static async Task VerifyAsync(IServiceProvider services, HttpClient client, HttpClient listingClient,
        Guid vehicleId, Guid updater)
    {
        var database = services.GetRequiredService<DealershipDbContext>();
        var quotes = services.GetRequiredService<PurchaseQuoteService>();
        Assert.Equal(PurchaseQuoteStatus.Unavailable, (await quotes.QuoteAsync(vehicleId, null, CancellationToken.None)).Status);
        using var missingSettings = await client.GetAsync($"/api/vehicles/{vehicleId}");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, missingSettings.StatusCode);
        var publicStore = services.GetRequiredService<IPublicListingStore>();
        Assert.Equal(ListingStatus.Unavailable, (await publicStore.ReadAsync(new(), CancellationToken.None)).Status);
        var estimateRequest = await VerifyEstimateAccessAsync(database, listingClient, vehicleId, updater);
        database.PurchaseSettings.Add(new(500m, 0.08m, updater, DateTimeOffset.UtcNow));
        database.PromoCodes.AddRange(
            new PromoCode(Guid.NewGuid(), "APEX500", 500m, DateTimeOffset.UtcNow.AddDays(1), true),
            new PromoCode(Guid.NewGuid(), "apex500", 100m, DateTimeOffset.UtcNow.AddDays(1), true),
            new PromoCode(Guid.NewGuid(), "RAYA1000", 1000m, DateTimeOffset.UtcNow.AddDays(1), true),
            new PromoCode(Guid.NewGuid(), "PAST500", 500m, DateTimeOffset.UtcNow.AddDays(-1), true),
            new PromoCode(Guid.NewGuid(), "INACTIVE500", 500m, DateTimeOffset.UtcNow.AddDays(1), false),
            new PromoCode(Guid.NewGuid(), "EXCESS", 60000.01m, DateTimeOffset.UtcNow.AddDays(1), true));
        await database.SaveChangesAsync();
        await VerifyEstimatePriceAsync(listingClient, vehicleId, updater, estimateRequest, 500m, 0.08m, 40m, 61780.01m);

        using var accepted = await client.PostAsJsonAsync("/api/promos/validations", new PromoValidationRequest(vehicleId, "\u00a0APEX500\t"));
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.True(accepted.Headers.CacheControl!.NoStore);
        var quote = (await accepted.Content.ReadFromJsonAsync<PurchaseQuote>())!;
        Assert.Equal("APEX500", quote.PromoCode);
        Assert.Equal(new PurchasePrice(60000m, 500m, 90m, 40m, 500m, 60130m), quote.Price);
        Assert.Equal(100m, (await quotes.QuoteAsync(vehicleId, "apex500", CancellationToken.None)).Quote!.Price.PromoDiscount);
        foreach (var code in new[] { "Apex500", "PAST500", "INACTIVE500", "EXCESS", "APEX500,RAYA1000", "" })
        {
            using var denied = await client.PostAsJsonAsync("/api/promos/validations", new PromoValidationRequest(vehicleId, code));
            Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        }

        using var missing = await client.PostAsJsonAsync("/api/promos/validations", new PromoValidationRequest(Guid.NewGuid(), "APEX500"));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var oversized = await client.PostAsJsonAsync("/api/promos/validations", new PromoValidationRequest(vehicleId, new string('a', 20000)));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, oversized.StatusCode);
        var undiscounted = await quotes.QuoteAsync(vehicleId, null, CancellationToken.None);
        Assert.Equal(60630m, undiscounted.Quote!.Price.Total);
        await database.PurchaseSettings.ExecuteUpdateAsync(update => update
            .SetProperty(item => item.ProcessingFee, 600m).SetProperty(item => item.SstRate, 0.10m)
            .SetProperty(item => item.Version, 2));
        Assert.Equal(60250m, (await quotes.QuoteAsync(vehicleId, "APEX500", CancellationToken.None)).Quote!.Price.Total);
        var detail = (await services.GetRequiredService<IPublicListingStore>()
            .DetailAsync(vehicleId, CancellationToken.None)).Detail!;
        Assert.Equal(60750m, detail.Price.Total);
        Assert.Equal(600m, detail.Price.ProcessingFee);
        Assert.Equal(60m, detail.Price.ServiceTax);
        Assert.Equal(0.10m, detail.SstRate);
        await VerifyEstimatePriceAsync(listingClient, vehicleId, updater, estimateRequest, 600m, 0.10m, 60m, 61900.01m);
        await database.PurchaseSettings.ExecuteUpdateAsync(update => update
            .SetProperty(item => item.ProcessingFee, 500m).SetProperty(item => item.SstRate, 0.08m)
            .SetProperty(item => item.Version, 3));
        database.ChangeTracker.Clear();
    }

    private static async Task<ListingEstimateRequest> VerifyEstimateAccessAsync(DealershipDbContext database,
        HttpClient client, Guid vehicleId, Guid manager)
    {
        var branchId = await database.Vehicles.Where(vehicle => vehicle.Id == vehicleId)
            .Select(vehicle => vehicle.BranchId).SingleAsync();
        var outsideVehicle = await database.Vehicles.Where(vehicle => vehicle.BranchId != branchId)
            .Select(vehicle => new { vehicle.Id, vehicle.BranchId }).FirstAsync();
        var request = new ListingEstimateRequest(branchId, 61111.01m, 129m);
        var path = $"/api/staff/listings/{vehicleId}/price-estimates";
        using var anonymous = await client.PostAsJsonAsync(path, request);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        var email = await database.Users.Where(user => user.Id == manager).Select(user => user.Email).SingleAsync();
        using var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(Guid.NewGuid(), email!, "Password123!"));
        var session = (await login.Content.ReadFromJsonAsync<LoginSession>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        using var missing = await client.PostAsJsonAsync(path, request);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, missing.StatusCode);
        Assert.True(missing.Headers.CacheControl!.NoStore);

        using var wrongBranch = await client.PostAsJsonAsync(path, request with { BranchId = outsideVehicle.BranchId });
        Assert.Equal(HttpStatusCode.Forbidden, wrongBranch.StatusCode);
        using var wrongVehicle = await client.PostAsJsonAsync($"/api/staff/listings/{outsideVehicle.Id}/price-estimates", request);
        Assert.Equal(HttpStatusCode.NotFound, wrongVehicle.StatusCode);
        using var incomplete = await client.PostAsJsonAsync(path, request with { RoadTax = null });
        Assert.Equal(HttpStatusCode.BadRequest, incomplete.StatusCode);

        try
        {
            await database.Users.Where(user => user.Id == manager).ExecuteUpdateAsync(update => update
                .SetProperty(user => user.Role, AccountRoles.Administrator).SetProperty(user => user.BranchId, (Guid?)null));
            using var forbidden = await client.PostAsJsonAsync(path, request);
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
            await database.Users.Where(user => user.Id == manager).ExecuteUpdateAsync(update => update
                .SetProperty(user => user.Role, AccountRoles.BranchManager).SetProperty(user => user.BranchId, branchId)
                .SetProperty(user => user.IsActive, false));
            using var inactive = await client.PostAsJsonAsync(path, request);
            Assert.Equal(HttpStatusCode.Unauthorized, inactive.StatusCode);
        }
        finally
        {
            await database.Users.Where(user => user.Id == manager).ExecuteUpdateAsync(update => update
                .SetProperty(user => user.Role, AccountRoles.BranchManager).SetProperty(user => user.BranchId, branchId)
                .SetProperty(user => user.IsActive, true));
        }

        return request;
    }

    private static async Task VerifyEstimatePriceAsync(HttpClient client, Guid vehicleId, Guid actorId,
        ListingEstimateRequest request, decimal processingFee, decimal sstRate, decimal serviceTax, decimal total)
    {
        using var response = await client.PostAsJsonAsync($"/api/staff/listings/{vehicleId}/price-estimates", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        var estimate = (await response.Content.ReadFromJsonAsync<ListingEstimate>())!;
        Assert.Equal(actorId, estimate.ActorId);
        Assert.Equal(vehicleId, estimate.VehicleId);
        Assert.Equal(request.BranchId, estimate.BranchId);
        Assert.Equal(sstRate, estimate.SstRate);
        Assert.Equal(new PurchasePrice(request.VehiclePrice!.Value, processingFee, request.RoadTax!.Value,
            serviceTax, 0m, total), estimate.Price);
    }
}
