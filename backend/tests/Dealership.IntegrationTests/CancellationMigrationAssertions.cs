using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dealership.IntegrationTests;

internal static class CancellationMigrationAssertions
{
    public static async Task VerifyAsync(RegistrationDatabase fixture)
    {
        var original = await ReadAsync(fixture.RuntimeConnectionString, embedded: true);
        var options = new DbContextOptionsBuilder<DealershipDbContext>().UseNpgsql(fixture.ConnectionString).Options;
        await using var database = new DealershipDbContext(options);
        await ConsolidationMigrationRange.DowngradeAsync(database, "20261021190743_EmbedAppointmentValuation");
        Assert.Equal(original, await ReadAsync(fixture.RuntimeConnectionString, embedded: false));

        await using (var connection = new NpgsqlConnection(fixture.RuntimeConnectionString))
        {
            await connection.OpenAsync();
            await using var mutation = new NpgsqlCommand("""
                UPDATE "AppointmentCancellations" SET "PhotosExpireAt" = "PhotosExpireAt" + interval '1 day'
                """, connection);
            var error = await Assert.ThrowsAsync<PostgresException>(() => mutation.ExecuteNonQueryAsync());
            Assert.Equal("CK_IntakeAppointments_Immutable", error.ConstraintName);
        }

        await ConsolidationMigrationRange.RestoreAsync(database, "20261021190743_EmbedAppointmentValuation");
        Assert.Equal(original, await ReadAsync(fixture.RuntimeConnectionString, embedded: true));
        var appointments = await database.IntakeAppointments.AsNoTracking().ToArrayAsync();
        Assert.Equal(DevelopmentIntakeManifest.Appointments.Count, appointments.Length);
        Assert.Equal(3, appointments.Count(item => item.CancelledAt is not null));
        await using var current = new NpgsqlConnection(fixture.RuntimeConnectionString);
        await current.OpenAsync();
        await using var retired = new NpgsqlCommand("SELECT to_regclass('public.\"AppointmentCancellations\"') IS NULL", current);
        Assert.True(Assert.IsType<bool>(await retired.ExecuteScalarAsync()));

        await using var maintenance = new NpgsqlConnection(fixture.ConnectionString);
        await maintenance.OpenAsync();
        await using var deletion = new NpgsqlCommand("""
            DELETE FROM "IntakeAppointments" WHERE "Id" = @id
            """, maintenance);
        deletion.Parameters.AddWithValue("id", appointments[0].Id);
        var deleted = await Assert.ThrowsAsync<PostgresException>(() => deletion.ExecuteNonQueryAsync());
        Assert.Equal("CK_IntakeAppointments_Immutable", deleted.ConstraintName);
    }

    private static async Task<string> ReadAsync(string connectionString, bool embedded)
    {
        const string legacy = """
            SELECT a."Id", a."OwnerId", c."CancelledAt", c."PhotosExpireAt"
            FROM "IntakeAppointments" a LEFT JOIN "AppointmentCancellations" c ON c."AppointmentId" = a."Id"
            """;
        const string current = """
            SELECT "Id", "OwnerId", "CancelledAt", "CancellationPhotosExpireAt" AS "PhotosExpireAt"
            FROM "IntakeAppointments"
            """;
        var projection = embedded ? current : legacy;
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var query = new NpgsqlCommand($"""
            SELECT jsonb_agg(to_jsonb(a) ORDER BY a."Id")::text FROM ({projection}) a
            """, connection);
        return Assert.IsType<string>(await query.ExecuteScalarAsync());
    }
}
