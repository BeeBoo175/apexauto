using Dealership.Domain.Branches;
using Dealership.Application.Accounts;
using Dealership.Application;
using Dealership.Infrastructure;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Dealership.IntegrationTests;

public sealed class DevelopmentFixtureRebuildTests(RegistrationDatabase fixture) : IClassFixture<RegistrationDatabase>
{
    [Fact]
    public async Task RebuildRequiresExactConfirmationAndRecreatesMigratedEmptyDatabase()
    {
        var databaseName = new NpgsqlConnectionStringBuilder(fixture.ConnectionString).Database!;
        var target = DevelopmentFixtureTarget.Validate("Development", databaseName, fixture.ConnectionString);
        await using var provider = CreateProvider(fixture.ConnectionString);
        await using var scope = provider.CreateAsyncScope();
        var runner = scope.ServiceProvider.GetRequiredService<DevelopmentFixtureRunner>();
        var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var registration = new RegistrationRequest(Guid.NewGuid(), "Nur Aina Abdullah",
            $"nur.aina.{Guid.NewGuid():N}@example.invalid", "Password123!", "940215140865",
            "0123456789", "18 Jalan Melati, Petaling Jaya, Selangor");
        var registered = await scope.ServiceProvider.GetRequiredService<RegistrationService>()
            .RegisterAsync(registration, CancellationToken.None);
        Assert.Equal(RegistrationStatus.Succeeded, registered.Status);
        database.Branches.Add(new Branch(Guid.NewGuid(), "ApexAuto Glenmarie", "12 Jalan Pengaturcara U1/51A",
            "Shah Alam", "Selangor", "+60355651234", DateTimeOffset.UtcNow));
        await database.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RebuildAsync(
            target, $"rebuild {databaseName}_different", CancellationToken.None));
        Assert.Equal(1, await database.Users.CountAsync());
        var runtimeTarget = DevelopmentFixtureTarget.Validate("Development", databaseName, fixture.RuntimeConnectionString);
        var runtimeRole = new NpgsqlConnectionStringBuilder(fixture.RuntimeConnectionString).Username!;
        using var identifiers = new NpgsqlCommandBuilder();
        // A database owner can drop its database while still lacking CREATEDB for recreation.
        await using var ownerConnection = new NpgsqlConnection(fixture.ConnectionString);
        await ownerConnection.OpenAsync();
        await using var changeOwner = new NpgsqlCommand(
            $"ALTER DATABASE {identifiers.QuoteIdentifier(databaseName)} OWNER TO {identifiers.QuoteIdentifier(runtimeRole)}",
            ownerConnection);
        await changeOwner.ExecuteNonQueryAsync();
        await ownerConnection.CloseAsync();

        await using (var runtimeProvider = CreateProvider(fixture.RuntimeConnectionString))
        {
            await using var runtimeScope = runtimeProvider.CreateAsyncScope();
            var runtimeRunner = runtimeScope.ServiceProvider.GetRequiredService<DevelopmentFixtureRunner>();
            await Assert.ThrowsAsync<InvalidOperationException>(() => runtimeRunner.RebuildAsync(
                runtimeTarget, $"rebuild {databaseName}", CancellationToken.None));
        }

        Assert.Equal(1, await database.Users.CountAsync());
        await runner.RebuildAsync(target, $"rebuild {databaseName}", CancellationToken.None);
        Assert.Equal(0, await database.Users.CountAsync());
        Assert.Equal(0, await database.Branches.CountAsync());
        Assert.Equal(0, await database.CustomerProfiles.CountAsync());
        Assert.Empty(await database.Database.GetPendingMigrationsAsync());
    }

    private static ServiceProvider CreateProvider(string connection)
    {
        var configuration = new ConfigurationBuilder().AddEnvironmentVariables().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = connection }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration);
        services.AddScoped<DevelopmentFixtureRunner>();
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }
}
