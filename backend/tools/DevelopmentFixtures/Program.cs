using Dealership.Application;
using Dealership.Application.Intake;
using Dealership.Infrastructure;
using Dealership.Infrastructure.Configuration;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Development.Maintenance;
using Dealership.Infrastructure.Intake;
using Dealership.Infrastructure.Storage;
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
    var configuration = new ConfigurationBuilder()
        .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"))
        .AddEnvironmentVariables().Build();
    var target = DevelopmentFixtureTarget.Validate(environment, configuration["POSTGRES_DB"],
        configuration.GetConnectionString("DefaultConnection"));
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddApplication();
    services.AddInfrastructure(configuration);
    services.AddSingleton(InspectionScheduleConfiguration.Read(configuration));
    services.AddScoped<IntakeDiscoveryService>();
    services.AddScoped<IntakeBookingService>();
    services.AddS3Storage(configuration);
    services.AddIntakeMedia(configuration);
    services.AddScoped<DevelopmentFixtureRunner>();
    services.AddScoped<DevelopmentCustomerFixtures>();
    services.AddScoped<DevelopmentIntakeFixtures>();
    services.AddScoped<DevelopmentFixturePhotos>();
    services.AddScoped<DevelopmentDatabaseRebuilder>();
    await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
    {
        ValidateScopes = true,
        ValidateOnBuild = true
    });
    await using var scope = provider.CreateAsyncScope();
    using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));

    if (args[0] == "clear")
    {
        var databaseName = new NpgsqlConnectionStringBuilder(target.ConnectionString).Database;
        Console.WriteLine($"This erases every record in local database {databaseName} and reapplies migrations.");
        Console.Write($"Type rebuild {databaseName} to continue: ");
        var rebuilder = scope.ServiceProvider.GetRequiredService<DevelopmentDatabaseRebuilder>();
        await rebuilder.RebuildAsync(target, Console.ReadLine(), deadline.Token);
        Console.WriteLine("Local database rebuilt. Run seed deliberately to recreate fixtures.");
        return 0;
    }

    var runner = scope.ServiceProvider.GetRequiredService<DevelopmentFixtureRunner>();
    var reports = args[0] == "seed"
        ? await runner.SeedAsync(target, deadline.Token)
        : await runner.ReviewAsync(target, deadline.Token);

    foreach (var report in reports)
    {
        Console.WriteLine($"{report.Fixture}: {report.State}");
    }

    return reports.Any(report => report.RequiresAttention) ? 2 : 0;
}
catch (Exception exception)
{
    // Provider errors can contain connection details; disclose only the failure category.
    var recovery = args[0] == "clear"
        ? "The database may already have been deleted. Inspect its state before retrying the rebuild."
        : "Existing records are preserved.";
    Console.Error.WriteLine($"Fixture operation failed ({exception.GetType().Name}). Check local configuration and applied migrations. {recovery}");
    return 1;
}
