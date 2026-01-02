using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dealership.Infrastructure.Development.Maintenance;

public sealed class DevelopmentDatabaseRebuilder(DealershipDbContext database)
{
    public async Task RebuildAsync(DevelopmentFixtureTarget target, string? confirmation,
        CancellationToken cancellationToken)
    {
        target.EnsureMatches(database.Database.GetConnectionString());
        var databaseName = new NpgsqlConnectionStringBuilder(target.ConnectionString).Database;

        if (confirmation != $"rebuild {databaseName}")
        {
            throw new InvalidOperationException("Database rebuild requires the exact typed target confirmation.");
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(2));
        await using var lockConnection = await DevelopmentFixtureLock.AcquireAsync(target, deadline.Token);
        await using var permissions = new NpgsqlCommand(
            "SELECT rolsuper OR rolcreatedb FROM pg_roles WHERE rolname = current_user", lockConnection);

        if (await permissions.ExecuteScalarAsync(deadline.Token) is not true)
        {
            throw new InvalidOperationException("Database rebuild requires maintenance credentials able to recreate the database.");
        }

        // The lock lives in postgres so it survives removal and recreation of the target database.
        await database.Database.EnsureDeletedAsync(deadline.Token);
        await database.Database.MigrateAsync(deadline.Token);
        database.ChangeTracker.Clear();
    }
}
