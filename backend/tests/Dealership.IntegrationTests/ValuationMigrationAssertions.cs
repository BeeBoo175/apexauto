using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dealership.IntegrationTests;

internal static class ValuationMigrationAssertions
{
    public static async Task VerifyAsync(RegistrationDatabase fixture)
    {
        var original = await ReadAsync(fixture.RuntimeConnectionString, embedded: true);
        var options = new DbContextOptionsBuilder<DealershipDbContext>().UseNpgsql(fixture.ConnectionString).Options;
        await using var database = new DealershipDbContext(options);

        await ConsolidationMigrationRange.DowngradeAsync(database, "20261021184456_ConsolidateAppointmentMail");
        Assert.Equal(original, await ReadAsync(fixture.RuntimeConnectionString, embedded: false));
        await AssertLegacyGuardsAsync(fixture.RuntimeConnectionString);
        await ConsolidationMigrationRange.RestoreAsync(database, "20261021184456_ConsolidateAppointmentMail");
        Assert.Equal(original, await ReadAsync(fixture.RuntimeConnectionString, embedded: true));

        var appointments = await database.IntakeAppointments.AsNoTracking().ToArrayAsync();
        Assert.Equal(132, appointments.Length);
        Assert.All(appointments, appointment => Assert.NotNull(appointment.Valuation));
        await using var connection = new NpgsqlConnection(fixture.RuntimeConnectionString);
        await connection.OpenAsync();
        await using var retired = new NpgsqlCommand("SELECT to_regclass('public.\"SellerValuations\"') IS NULL", connection);
        Assert.True(Assert.IsType<bool>(await retired.ExecuteScalarAsync()));
        await using var mutation = new NpgsqlCommand("""
            UPDATE "IntakeAppointments" SET "ValuationMinimum" = 0 WHERE "Id" = @appointment
            """, connection);
        mutation.Parameters.AddWithValue("appointment", appointments[0].Id);
        var error = await Assert.ThrowsAsync<PostgresException>(() => mutation.ExecuteNonQueryAsync());
        Assert.Equal("CK_IntakeAppointments_Immutable", error.ConstraintName);
    }

    private static async Task<string> ReadAsync(string connectionString, bool embedded)
    {
        const string legacy = """
            SELECT "AppointmentId", "OwnerId", "Make", "Model", "Trim", "ManufactureYear", "MileageKilometres",
                "Minimum", "Maximum", "Currency", "ValuationYear" FROM "SellerValuations"
            """;
        const string current = """
            SELECT "Id" AS "AppointmentId", "OwnerId", "ValuationMake" AS "Make", "ValuationModel" AS "Model",
                "ValuationTrim" AS "Trim", "ValuationManufactureYear" AS "ManufactureYear",
                "ValuationMileageKilometres" AS "MileageKilometres", "ValuationMinimum" AS "Minimum",
                "ValuationMaximum" AS "Maximum", "ValuationCurrency" AS "Currency", "ValuationYear"
            FROM "IntakeAppointments"
            """;
        var projection = embedded ? current : legacy;
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var query = new NpgsqlCommand($"""
            SELECT jsonb_agg(to_jsonb(v) ORDER BY v."AppointmentId")::text FROM ({projection}) v
            """, connection);
        return Assert.IsType<string>(await query.ExecuteScalarAsync());
    }

    private static async Task AssertLegacyGuardsAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var mutation = new NpgsqlCommand("""
            UPDATE "SellerValuations" SET "Minimum" = 0
            WHERE "AppointmentId" = (SELECT "AppointmentId" FROM "SellerValuations" LIMIT 1)
            """, connection);
        var error = await Assert.ThrowsAsync<PostgresException>(() => mutation.ExecuteNonQueryAsync());
        Assert.Equal("CK_IntakeAppointments_Immutable", error.ConstraintName);
    }
}
