using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Dealership.IntegrationTests;

internal static class ConsolidationMigrationRange
{
    private const string Consolidated = "20261021192806_EmbedAppointmentCancellation";

    public static Task<int> DowngradeAsync(DealershipDbContext database, string target)
    {
        return ApplyAsync(database, Consolidated, target);
    }

    public static Task<int> RestoreAsync(DealershipDbContext database, string source)
    {
        return ApplyAsync(database, source, Consolidated);
    }

    private static Task<int> ApplyAsync(DealershipDbContext database, string source, string target)
    {
        // Exercise only this historical range; later inspection data belongs to the shared fixture.
        var script = database.GetService<IMigrator>().GenerateScript(source, target);
        return database.Database.ExecuteSqlRawAsync(script);
    }
}
