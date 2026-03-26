using System.Net;
using System.Net.Http.Json;
using Dealership.Application.Vehicles;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

internal static class PurchaseQuoteAssertions
{
    public static async Task VerifyAsync(IServiceProvider services, HttpClient client, Guid vehicleId, Guid updater)
    {
        var database = services.GetRequiredService<DealershipDbContext>();
        var quotes = services.GetRequiredService<PurchaseQuoteService>();
        Assert.Equal(PurchaseQuoteStatus.Unavailable, (await quotes.QuoteAsync(vehicleId, null, CancellationToken.None)).Status);
        database.PurchaseSettings.Add(new(500m, 0.08m, updater, DateTimeOffset.UtcNow));
        database.PromoCodes.AddRange(
            new PromoCode(Guid.NewGuid(), "APEX500", 500m, DateTimeOffset.UtcNow.AddDays(1), true),
            new PromoCode(Guid.NewGuid(), "apex500", 100m, DateTimeOffset.UtcNow.AddDays(1), true),
            new PromoCode(Guid.NewGuid(), "RAYA1000", 1000m, DateTimeOffset.UtcNow.AddDays(1), true),
            new PromoCode(Guid.NewGuid(), "PAST500", 500m, DateTimeOffset.UtcNow.AddDays(-1), true),
            new PromoCode(Guid.NewGuid(), "INACTIVE500", 500m, DateTimeOffset.UtcNow.AddDays(1), false),
            new PromoCode(Guid.NewGuid(), "EXCESS", 60000.01m, DateTimeOffset.UtcNow.AddDays(1), true));
        await database.SaveChangesAsync();

        using var accepted = await client.PostAsJsonAsync("/api/promos/validate", new PromoValidationRequest(vehicleId, "\u00a0APEX500\t"));
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.True(accepted.Headers.CacheControl!.NoStore);
        var quote = (await accepted.Content.ReadFromJsonAsync<PurchaseQuote>())!;
        Assert.Equal("APEX500", quote.PromoCode);
        Assert.Equal(new PurchasePrice(60000m, 500m, 90m, 40m, 500m, 60130m), quote.Price);
        Assert.Equal(100m, (await quotes.QuoteAsync(vehicleId, "apex500", CancellationToken.None)).Quote!.Price.PromoDiscount);
        foreach (var code in new[] { "Apex500", "PAST500", "INACTIVE500", "EXCESS", "APEX500,RAYA1000", "" })
        {
            using var denied = await client.PostAsJsonAsync("/api/promos/validate", new PromoValidationRequest(vehicleId, code));
            Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        }

        using var missing = await client.PostAsJsonAsync("/api/promos/validate", new PromoValidationRequest(Guid.NewGuid(), "APEX500"));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var oversized = await client.PostAsJsonAsync("/api/promos/validate", new PromoValidationRequest(vehicleId, new string('a', 20000)));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, oversized.StatusCode);
        var undiscounted = await quotes.QuoteAsync(vehicleId, null, CancellationToken.None);
        Assert.Equal(60630m, undiscounted.Quote!.Price.Total);
        await database.PurchaseSettings.ExecuteUpdateAsync(update => update
            .SetProperty(item => item.ProcessingFee, 600m).SetProperty(item => item.SstRate, 0.10m)
            .SetProperty(item => item.Version, 2));
        Assert.Equal(60250m, (await quotes.QuoteAsync(vehicleId, "APEX500", CancellationToken.None)).Quote!.Price.Total);
        await database.PurchaseSettings.ExecuteUpdateAsync(update => update
            .SetProperty(item => item.ProcessingFee, 500m).SetProperty(item => item.SstRate, 0.08m)
            .SetProperty(item => item.Version, 3));
        database.ChangeTracker.Clear();
    }
}
