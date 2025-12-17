using Npgsql;

namespace Dealership.Infrastructure.Development;

public sealed class DevelopmentFixtureTarget
{
    private DevelopmentFixtureTarget(string connectionString)
    {
        ConnectionString = connectionString;
    }

    public string ConnectionString { get; }

    public static DevelopmentFixtureTarget Validate(string? environment, string? configuredDatabase,
        string? connectionString)
    {
        if (environment != "Development" || string.IsNullOrWhiteSpace(configuredDatabase) ||
            string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Fixtures require explicit Development and local PostgreSQL configuration.");
        }

        var settings = new NpgsqlConnectionStringBuilder(connectionString);

        if (settings.Host is not ("localhost" or "127.0.0.1" or "::1") ||
            settings.Database != configuredDatabase ||
            settings.Database.Equals("postgres", StringComparison.OrdinalIgnoreCase) ||
            settings.Database.Equals("template0", StringComparison.OrdinalIgnoreCase) ||
            settings.Database.Equals("template1", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Fixtures require the explicitly configured loopback PostgreSQL database.");
        }

        settings.IncludeErrorDetail = false;
        settings.Pooling = false;
        return new DevelopmentFixtureTarget(settings.ConnectionString);
    }
}
