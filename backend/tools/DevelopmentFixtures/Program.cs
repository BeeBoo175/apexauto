using Dealership.Application;
using Dealership.Infrastructure;
using Dealership.Infrastructure.Configuration;
using Dealership.Infrastructure.Development;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

if (args.Length != 1 || args[0] is not ("seed" or "review" or "clear"))
{
    Console.Error.WriteLine("Usage: DevelopmentFixtures seed|review|clear");
    return 1;
}

var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
var webEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

if (environment != "Development" || webEnvironment != "Development")
{
    Console.Error.WriteLine("Fixtures require both environment settings to be Development.");
    return 1;
}

try
{
    DotEnvLoader.LoadForDevelopment(Directory.GetCurrentDirectory());
    var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
    var target = DevelopmentFixtureTarget.Validate(environment, configuration["POSTGRES_DB"],
        configuration.GetConnectionString("DefaultConnection"));
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddApplication();
    services.AddInfrastructure(configuration);
    services.AddScoped<DevelopmentFixtureRunner>();
    await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
    {
        ValidateScopes = true,
        ValidateOnBuild = true
    });
    await using var scope = provider.CreateAsyncScope();
    var runner = scope.ServiceProvider.GetRequiredService<DevelopmentFixtureRunner>();
    using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));

    if (args[0] == "clear")
    {
        var databaseName = new NpgsqlConnectionStringBuilder(target.ConnectionString).Database;
        Console.WriteLine($"This erases every record in local database {databaseName} and reapplies migrations.");
        Console.Write($"Type rebuild {databaseName} to continue: ");
        await runner.RebuildAsync(target, Console.ReadLine(), deadline.Token);
        Console.WriteLine("Local database rebuilt. Run seed deliberately to recreate fixtures.");
        return 0;
    }

    var reports = args[0] == "seed"
        ? await runner.SeedAsync(target, deadline.Token)
        : await runner.ReviewAsync(target, deadline.Token);

    foreach (var report in reports)
    {
        Console.WriteLine($"{report.Fixture}: {report.State}");
    }

    return reports.Any(report => report.State is "drift; preserved" or "conflict; preserved" or "missing") ? 2 : 0;
}
catch (Exception exception)
{
    // Provider errors can contain connection details; disclose only the failure category.
    Console.Error.WriteLine($"Fixture operation failed ({exception.GetType().Name}). Check local configuration and applied migrations; existing records are preserved.");
    return 1;
}
