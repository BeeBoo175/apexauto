using Dealership.Application;
using Dealership.Application.Photos;
using Dealership.Infrastructure.Photos;
using Dealership.Infrastructure;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using static Dealership.IntegrationTests.RetentionMinimizationTests;

namespace Dealership.IntegrationTests;

// The retention worker against the full canonical demo database, aged eight years through the owner connection.
[Trait("Category", "SeedPipeline")]
public sealed class RetentionDemoDatabaseTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    private const string Age = "8 years";

    private const string QueuedRemovals =
        "SELECT ((SELECT count(*) FROM \"PhotoRemovals\" WHERE \"Reason\" = 'RetentionExpired') + " +
        "(SELECT count(*) FROM \"InspectionPhotoRemovals\" WHERE \"Reason\" = 'RetentionExpired'))::int AS \"Value\"";

    [Fact]
    public async Task AgedDemoDatabaseIsDisposedOnceWithoutFailuresAndLeavesUnfinishedAndOwedRecords()
    {
        await SeedDemoAsync();
        await using var owner = new NpgsqlConnection(fixture.Database.ConnectionString);
        await owner.OpenAsync();
        await AgeAsync(owner);

        var warnings = new List<string>();
        using var factory = fixture.CreateFactory().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddLogging(logging => logging.AddProvider(new WarningCapture(warnings)))));
        using var client = factory.CreateClient();
        var worker = Worker(factory);
        for (var pass = 0; pass < 40; pass++)
        {
            await worker.RunBatchAsync(CancellationToken.None);
        }

        Assert.DoesNotContain(warnings, message => message.Contains("Retention of", StringComparison.Ordinal));
        await using var database = new DealershipDbContext(new DbContextOptionsBuilder<DealershipDbContext>()
            .UseNpgsql(fixture.Database.RuntimeConnectionString).Options);

        // Delivered sales lost their buyer copies unless money of the customer is still owed.
        var removed = await ScalarAsync(database,
            "SELECT count(*)::int AS \"Value\" FROM \"SaleDocuments\" d JOIN \"SalesOrders\" s ON s.\"Id\" = d.\"SalesOrderId\" " +
            "WHERE s.\"Status\" = 'Delivered' AND d.\"ContentsJson\"->'Review'->'BuyerName' IS NULL");
        Assert.True(removed > 0, "No delivered sale lost its buyer details.");
        var unexplained = await ScalarAsync(database,
            "SELECT count(*)::int AS \"Value\" FROM \"SaleDocuments\" d JOIN \"SalesOrders\" s ON s.\"Id\" = d.\"SalesOrderId\" " +
            "WHERE s.\"Status\" = 'Delivered' AND d.\"ContentsJson\"->'Review'->'BuyerName' IS NOT NULL AND " +
            "NOT EXISTS (SELECT 1 FROM \"ReservationRefunds\" f WHERE f.\"CustomerId\" = s.\"CustomerId\" AND f.\"State\" <> 'Completed') AND " +
            "NOT EXISTS (SELECT 1 FROM \"ReservationDepositPayments\" p JOIN \"ReservationDepositRequests\" q ON q.\"Id\" = p.\"RequestId\" " +
            "WHERE q.\"CustomerId\" = s.\"CustomerId\" AND p.\"State\" IN ('Paid', 'PaidUnmatched') AND " +
            "NOT EXISTS (SELECT 1 FROM \"ReservationRefunds\" f WHERE f.\"PaymentRequestId\" = p.\"RequestId\" AND f.\"State\" = 'Completed'))");
        Assert.Equal(0, unexplained);

        // A sale that is only Settled never loses its documents' buyer details.
        Assert.Equal(0, await ScalarAsync(database,
            "SELECT count(*)::int AS \"Value\" FROM \"SaleDocuments\" d JOIN \"SalesOrders\" s ON s.\"Id\" = d.\"SalesOrderId\" " +
            "WHERE s.\"Status\" <> 'Delivered' AND d.\"ContentsJson\"->'Review'->'BuyerName' IS NULL"));

        // Photograph removals are queued once and a repeat run changes nothing.
        var queued = await ScalarAsync(database, QueuedRemovals);
        await worker.RunBatchAsync(CancellationToken.None);
        Assert.Equal(queued, await ScalarAsync(database, QueuedRemovals));
        Assert.DoesNotContain(warnings, message => message.Contains("Retention of", StringComparison.Ordinal));
    }

    private static async Task<int> ScalarAsync(DealershipDbContext database, string sql)
    {
        return await database.Database.SqlQueryRaw<int>(sql)
            .SingleAsync();
    }

    // Every anchor the deadlines read moves back together, so each table's own time checks still hold.
    private static async Task AgeAsync(NpgsqlConnection owner)
    {
        foreach (var statement in new (string Table, string Set)[]
        {
            ("SalesOrders", string.Join(", ", new[] { "CreatedAt", "UpdatedAt", "ClaimedAt", "DueAt", "SubmittedAt",
                "DecidedAt", "ClosedAt", "SettledAt", "DeliveredAt" }.Select(Shift))),
            ("SalesOrderEvents", Shift("RecordedAt")),
            ("SaleDocumentCorrections", Shift("RecordedAt")),
            ("InspectionSignedRevisions", Shift("SignedAt")),
            ("AppointmentCheckIns", string.Join(", ", new[] { "CheckedInAt", "CorrectionRecordedAt" }.Select(Shift))),
            ("ReservationReleases", Shift("RecordedAt"))
        })
        {
            await ExecuteAsync(owner, $"ALTER TABLE \"{statement.Table}\" DISABLE TRIGGER USER");
            await ExecuteAsync(owner, $"UPDATE \"{statement.Table}\" SET {statement.Set}");
            await ExecuteAsync(owner, $"ALTER TABLE \"{statement.Table}\" ENABLE TRIGGER USER");
        }
    }

    private static string Shift(string column) => $"\"{column}\" = \"{column}\" - interval '{Age}'";

    private async Task SeedDemoAsync()
    {
        var connection = fixture.Database.ConnectionString;
        var configuration = new ConfigurationBuilder().AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"))
            .AddEnvironmentVariables().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connection,
                ["AWS:BucketName"] = fixture.BucketName,
                ["IntakePhotos:CleanupEnabled"] = "false"
            }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddIntakeApplication();
        services.AddPhotoApplication();
        services.AddInfrastructure(configuration);
        services.AddDevelopmentFixtures(configuration);
        services.AddS3Storage(configuration);
        services.AddPhotos(configuration);
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
        await using var scope = provider.CreateAsyncScope();
        var target = DevelopmentFixtureTarget.Validate("Development", new NpgsqlConnectionStringBuilder(connection).Database,
            connection);
        await scope.ServiceProvider.GetRequiredService<DevelopmentFixtureRunner>().SeedAsync(target, CancellationToken.None);
    }
}
