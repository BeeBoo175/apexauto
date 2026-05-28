using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Dealership.IntegrationTests;

internal static class ConsolidationMigrationRange
{
    private const string Consolidated = "20261021192806_EmbedAppointmentCancellation";

    public static async Task<string> DowngradeAsync(DealershipDbContext database, string target)
    {
        var guard = await database.Database.SqlQueryRaw<string>(
            "SELECT pg_get_functiondef('public.validate_appointment_checkin()'::regprocedure) AS \"Value\"").SingleAsync();
        await ApplyAsync(database, Consolidated, target);
        return guard;
    }

    public static async Task RestoreAsync(DealershipDbContext database, string source, string checkInGuard)
    {
        await ApplyAsync(database, source, Consolidated);
        // Historical migrations replace this function; retain the live guard from before the probe.
        await database.Database.ExecuteSqlRawAsync(checkInGuard);
    }

    private static Task<int> ApplyAsync(DealershipDbContext database, string source, string target)
    {
        // Exercise only this historical range; later inspection data belongs to the shared fixture.
        var script = database.GetService<IMigrator>().GenerateScript(source, target);
        return database.Database.ExecuteSqlRawAsync(script);
    }
}
