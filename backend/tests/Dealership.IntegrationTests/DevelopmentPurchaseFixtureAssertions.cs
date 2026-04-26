using System.Text.Json;
using Dealership.Application.Vehicles;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

internal static class DevelopmentPurchaseFixtureAssertions
{
    public static async Task VerifyAsync(IServiceProvider provider, DevelopmentFixtureTarget target)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var database = services.GetRequiredService<DealershipDbContext>();
        var settings = await database.PurchaseSettings.SingleAsync();
        Assert.Equal(500m, settings.ProcessingFee);
        Assert.Equal(0.08m, settings.SstRate);
        Assert.Equal(DevelopmentPurchaseFixtures.Settings.UpdatedById, settings.UpdatedById);
        Assert.Equal(2, await database.PromoCodes.CountAsync());
        Assert.Equal("CBA", string.Concat((await database.WarrantyPackages.AsNoTracking().ToListAsync())
            .OrderBy(item => item.Price).Select(item => item.MinimumGrade)));
        var vehicleId = await database.ListingPublications.Where(item =>
            !database.Reservations.Any(hold => hold.VehicleId == item.VehicleId && hold.IsActive))
            .Select(item => item.VehicleId).FirstAsync();
        var quotes = services.GetRequiredService<PurchaseQuoteService>();
        Assert.Equal(500m, (await quotes.QuoteAsync(vehicleId, "APEX500", CancellationToken.None)).Quote!.Price.PromoDiscount);
        Assert.Equal(1000m, (await quotes.QuoteAsync(vehicleId, "RAYA1000", CancellationToken.None)).Quote!.Price.PromoDiscount);

        var before = await SnapshotAsync(database);
        Assert.All(await services.GetRequiredService<DevelopmentPurchaseFixtures>().RunAsync(true, CancellationToken.None),
            report => Assert.False(report.RequiresAttention));
        Assert.Equal(before, await SnapshotAsync(database));
        await database.PurchaseSettings.ExecuteUpdateAsync(update => update
            .SetProperty(item => item.ProcessingFee, 650m).SetProperty(item => item.Version, 2));
        await database.PromoCodes.Where(item => item.Code == "APEX500").ExecuteUpdateAsync(update => update
            .SetProperty(item => item.Discount, 450m).SetProperty(item => item.IsActive, false));
        var changed = await SnapshotAsync(database);
        try
        {
            var reports = await services.GetRequiredService<DevelopmentFixtureRunner>().SeedAsync(target, CancellationToken.None);
            Assert.True(reports.Single(item => item.Fixture == "Purchase settings").RequiresAttention);
            Assert.True(reports.Single(item => item.Fixture == "Promo (APEX500)").RequiresAttention);
            Assert.Equal(changed, await SnapshotAsync(database));
        }
        finally
        {
            await database.PurchaseSettings.ExecuteUpdateAsync(update => update
                .SetProperty(item => item.ProcessingFee, 500m).SetProperty(item => item.Version, 1));
            await database.PromoCodes.Where(item => item.Code == "APEX500").ExecuteUpdateAsync(update => update
                .SetProperty(item => item.Discount, 500m).SetProperty(item => item.IsActive, true));
        }

        var requested = DevelopmentPurchaseFixtures.Promos[0] with { Id = Guid.NewGuid(), Code = "APEX500" };
        Assert.False((await services.GetRequiredService<PurchaseConfigurationService>()
            .ProvisionPromoAsync(requested, CancellationToken.None)).MatchesRequestedState);
        requested = DevelopmentPurchaseFixtures.Promos[0] with { Code = "CHANGED500" };
        Assert.False((await services.GetRequiredService<PurchaseConfigurationService>()
            .ProvisionPromoAsync(requested, CancellationToken.None)).MatchesRequestedState);
        Assert.Equal(before, await SnapshotAsync(database));
    }

    private static async Task<string> SnapshotAsync(DealershipDbContext database) => JsonSerializer.Serialize(new
    {
        Settings = await database.PurchaseSettings.AsNoTracking().ToArrayAsync(),
        Promos = await database.PromoCodes.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync(),
        Warranties = await database.WarrantyPackages.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync()
    });
}
