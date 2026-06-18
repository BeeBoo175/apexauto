using Dealership.Infrastructure.Photos;
using Dealership.Application;
using Dealership.Infrastructure;
using Dealership.Infrastructure.Configuration;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Development.Maintenance;
using Dealership.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

if (args.Length is < 1 or > 2 || args[0] is not ("seed" or "review" or "clear" or "seed-accounts" or "review-accounts") ||
    args.Length == 2 && (args[0] != "seed" || args[1] != "--deliver-mail"))
{
    Console.Error.WriteLine("Usage: DevelopmentFixtures seed [--deliver-mail] | review | seed-accounts | review-accounts | clear");
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
    var accountsOnly = args[0] is "seed-accounts" or "review-accounts";
    if (accountsOnly)
    {
        services.AddDevelopmentAccounts(configuration);
    }
    else
    {
        services.AddIntakeApplication();
        services.AddPhotoApplication();
        services.AddDevelopmentFixtures(configuration);
        services.AddS3Storage(configuration);
        services.AddPhotos(configuration);
        services.AddScoped<DevelopmentDatabaseRebuilder>();
    }
    await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
    {
        ValidateScopes = true,
        ValidateOnBuild = true
    });
    await using var scope = provider.CreateAsyncScope();
    using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(5));

    if (args[0] == "clear")
    {
        var databaseName = new NpgsqlConnectionStringBuilder(target.ConnectionString).Database;
        var bucketName = scope.ServiceProvider.GetRequiredService<PhotoStorageSettings>().BucketName;
        Console.WriteLine($"This erases every record in local database {databaseName}, clears local bucket {bucketName}, and reapplies migrations.");
        Console.Write($"Type rebuild {databaseName} to continue: ");
        var rebuilder = scope.ServiceProvider.GetRequiredService<DevelopmentDatabaseRebuilder>();
        await rebuilder.RebuildAsync(target, Console.ReadLine(), deadline.Token);
        Console.WriteLine("Local database and object storage rebuilt. Run seed deliberately to recreate fixtures.");
        return 0;
    }

    IReadOnlyList<DevelopmentFixtureReport> reports;
    IReadOnlyList<DevelopmentFixtureTiming> timings = [];
    if (accountsOnly)
    {
        var accounts = scope.ServiceProvider.GetRequiredService<DevelopmentAccountFixtures>();
        reports = args[0] == "seed-accounts"
            ? await accounts.SeedAsync(target, deadline.Token)
            : await accounts.ReviewAsync(target, deadline.Token);
    }
    else
    {
        var runner = scope.ServiceProvider.GetRequiredService<DevelopmentFixtureRunner>();
        reports = args[0] == "seed"
            ? await runner.SeedAsync(target, deadline.Token, deliverMail: args.Length == 2)
            : await runner.ReviewAsync(target, deadline.Token);
        timings = runner.Timings;
    }

    foreach (var report in reports)
    {
        Console.WriteLine($"{report.Fixture}: {report.State}");
    }

    foreach (var timing in timings)
    {
        Console.WriteLine($"Timing {timing.Unit}: {timing.Elapsed.TotalSeconds:0.0} s");
    }

    if (args[0] == "seed-accounts")
    {
        Console.WriteLine("Accounts and required staff branches only. Existing business records were preserved; no media or business fixtures were created.");
    }
    else if (args[0] == "seed")
    {
        Console.WriteLine(args.Length == 2
            ? "New fixture mail remains queued for delivery. Existing delivery state was preserved."
            : "New fixture mail was marked delivered without sending. Existing pending mail was preserved.");
    }

    return reports.Any(report => report.RequiresAttention) ? 2 : 0;
}
catch (Exception exception)
{
    // Provider errors can contain connection details; disclose only the failure category.
    var recovery = args[0] switch
    {
        "clear" => "The database may already have been deleted. Inspect its state before retrying the rebuild.",
        "seed-accounts" or "review-accounts" => "Committed accounts are preserved. Use review-accounts to inspect missing or edited records before retrying.",
        _ => "Intake initialization rolls back on failure. Review the target; deliberately clear then seed to rebuild a populated snapshot. Initialization requires maintenance credentials."
    };
    Console.Error.WriteLine($"Fixture operation failed ({exception.GetType().Name}). Check local configuration and applied migrations. {recovery}");
    return 1;
}
