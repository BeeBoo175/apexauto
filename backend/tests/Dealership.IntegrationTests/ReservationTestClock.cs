using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dealership.IntegrationTests;

internal sealed class ReservationTestClock(RegistrationDatabase database) : IAsyncDisposable
{
    public DealershipDbContext Maintenance { get; } = new(new DbContextOptionsBuilder<DealershipDbContext>()
        .UseNpgsql(database.ConnectionString).Options);

    public async Task InitializeAsync(DateTimeOffset now)
    {
        // Only disposable test connections resolve this clock; retained receipts are never edited.
        await Maintenance.Database.ExecuteSqlRawAsync("""
            CREATE SCHEMA reservation_test_clock;
            CREATE TABLE reservation_test_clock.time (at timestamptz NOT NULL);
            CREATE FUNCTION reservation_test_clock.clock_timestamp() RETURNS timestamptz LANGUAGE sql AS
                'SELECT at FROM reservation_test_clock.time';
            GRANT USAGE ON SCHEMA reservation_test_clock TO PUBLIC;
            GRANT SELECT ON reservation_test_clock.time TO PUBLIC;
            """);
        await Maintenance.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO reservation_test_clock.time VALUES ({now})");
    }

    public DealershipDbContext CreateContext()
    {
        var connection = new NpgsqlConnectionStringBuilder(database.RuntimeConnectionString)
        {
            SearchPath = "reservation_test_clock,public,pg_catalog"
        };
        return new(new DbContextOptionsBuilder<DealershipDbContext>().UseNpgsql(connection.ConnectionString).Options);
    }

    public async Task AdvanceAsync(DateTimeOffset now)
    {
        await Maintenance.Database.ExecuteSqlInterpolatedAsync($"UPDATE reservation_test_clock.time SET at = {now}");
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await Maintenance.Database.ExecuteSqlRawAsync("DROP SCHEMA IF EXISTS reservation_test_clock CASCADE");
        }
        finally
        {
            await Maintenance.DisposeAsync();
        }
    }
}
