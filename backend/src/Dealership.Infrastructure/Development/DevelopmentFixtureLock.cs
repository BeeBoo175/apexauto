using Npgsql;

namespace Dealership.Infrastructure.Development;

internal static class DevelopmentFixtureLock
{
    public static async Task<NpgsqlConnection> AcquireAsync(DevelopmentFixtureTarget target,
        CancellationToken cancellationToken)
    {
        var settings = new NpgsqlConnectionStringBuilder(target.ConnectionString);
        var key = $"apexauto:development-fixtures:{settings.Database}";
        settings.Database = "postgres";
        var connection = new NpgsqlConnection(settings.ConnectionString);

        try
        {
            await connection.OpenAsync(cancellationToken);
            await using var acquire = new NpgsqlCommand(
                "SELECT pg_advisory_lock(hashtextextended(@key, 0))", connection)
            {
                // Competing canonical snapshots share the caller's bounded maintenance deadline.
                CommandTimeout = 120
            };
            acquire.Parameters.AddWithValue("key", key);
            await acquire.ExecuteNonQueryAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
