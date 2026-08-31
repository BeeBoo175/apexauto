using Dealership.Application;
using Dealership.Infrastructure;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Dealership.IntegrationTests;

// The minimal seed (accounts and branches) provisions the one purchase settings record, so a database without the
// demo data can still serve quotes and the Settings page, and no page read has to create it.
public sealed class MinimalSeedSettingsTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task TheMinimalSeedCreatesTheSettingsRecordOnceAndReviewsItAfterwards()
    {
        var connection = fixture.Database.ConnectionString;
        var target = DevelopmentFixtureTarget.Validate("Development", new NpgsqlConnectionStringBuilder(connection).Database,
            connection);
        await using var provider = CreateProvider(connection);

        var first = await SeedAsync(provider, target);
        Assert.Equal("created", first.Single(report => report.Fixture == "Purchase settings").State);
        var second = await SeedAsync(provider, target);
        Assert.Equal("matches", second.Single(report => report.Fixture == "Purchase settings").State);

        await using var scope = provider.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var settings = await database.PurchaseSettings.AsNoTracking().SingleAsync();
        Assert.Equal((500m, 0.08m, 1L), (settings.ProcessingFee, settings.SstRate, settings.Version));
        Assert.Equal(DevelopmentPurchaseFixtures.Settings.UpdatedById, settings.UpdatedById);

        var review = await scope.ServiceProvider.GetRequiredService<DevelopmentAccountFixtures>()
            .ReviewAsync(target, CancellationToken.None);
        Assert.False(review.Single(report => report.Fixture == "Purchase settings").RequiresAttention);
    }

    private static ServiceProvider CreateProvider(string connection)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"))
            .AddEnvironmentVariables()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = connection })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration);
        services.AddDevelopmentAccounts(configuration);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    private static async Task<IReadOnlyList<DevelopmentFixtureReport>> SeedAsync(ServiceProvider provider,
        DevelopmentFixtureTarget target)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<DevelopmentAccountFixtures>()
            .SeedAsync(target, CancellationToken.None);
    }
}
