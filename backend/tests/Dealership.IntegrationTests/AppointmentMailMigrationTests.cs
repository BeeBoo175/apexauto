using Dealership.Application.Intake;
using Dealership.Infrastructure.Intake;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Dealership.IntegrationTests;

public sealed class AppointmentMailMigrationTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    private const string DeliveryColumns = """
        "RecipientEmail", "DeliveryAttempts", "NextDeliveryAt", "DeliveryClaimId", "DeliveryClaimExpiresAt", "DeliveredAt"
        """;

    [Fact]
    public async Task UpgradeAndRollbackPreserveEveryPurposeDeliveryStatePermissionsAndIntegrity()
    {
        using var data = await CheckInTestData.CreateAsync(fixture);
        var checkedIn = await data.BookAsync();
        await data.SaveBundleAsync(checkedIn.Id);
        var pending = await data.BookAsync(dateOffset: 1);
        var cancelled = await data.BookAsync(slot: 1, dateOffset: 1);
        using var scope = data.Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var cancellation = await scope.ServiceProvider.GetRequiredService<AppointmentCancellationService>()
            .CancelAsync(data.OwnerId, cancelled.Id, CancellationToken.None);
        Assert.Equal(CancellationStatus.Succeeded, cancellation.Status);

        await PrepareDeliveryStatesAsync(database, checkedIn.Id);
        var original = await ReadStateAsync(legacy: false);
        var maintenanceOptions = new DbContextOptionsBuilder<DealershipDbContext>()
            .UseNpgsql(fixture.Database.ConnectionString).Options;
        await using var maintenance = new DealershipDbContext(maintenanceOptions);

        var checkInGuard = await ConsolidationMigrationRange.DowngradeAsync(maintenance, "20261020201746_NoShowAttendanceCorrection");
        Assert.Equal(original, await ReadStateAsync(legacy: true));
        await AssertLegacyGuardAsync(checkedIn.Id);
        await ConsolidationMigrationRange.RestoreAsync(maintenance, "20261020201746_NoShowAttendanceCorrection", checkInGuard);
        Assert.Equal(original, await ReadStateAsync(legacy: false));
        await AssertQueueGuardsAsync(pending.Id, checkedIn.Id);

        // A second round proves newly consolidated rows remain reversible without resetting claim receipts.
        checkInGuard = await ConsolidationMigrationRange.DowngradeAsync(maintenance, "20261020201746_NoShowAttendanceCorrection");
        Assert.Equal(original, await ReadStateAsync(legacy: true));
        await ConsolidationMigrationRange.RestoreAsync(maintenance, "20261020201746_NoShowAttendanceCorrection", checkInGuard);
        Assert.Equal(original, await ReadStateAsync(legacy: false));
    }

    private static async Task PrepareDeliveryStatesAsync(DealershipDbContext database, Guid checkedInId)
    {
        var messages = await database.AppointmentMailOutbox.ToArrayAsync();
        Assert.Equal(5, messages.Length);
        var now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        foreach (var message in messages)
        {
            if (message.Kind == AppointmentMailKind.Confirmation && message.AppointmentId != checkedInId)
            {
                continue;
            }

            message.DeliveryAttempts = 3;
            message.DeliveryClaimId = Guid.NewGuid();
            message.DeliveryClaimExpiresAt = now.AddMinutes(1);
            message.NextDeliveryAt = now.AddMinutes(2);
            if (message.Kind == AppointmentMailKind.Confirmation)
            {
                message.DeliveredAt = now;
            }
        }

        await database.SaveChangesAsync();
        database.ChangeTracker.Clear();
    }

    private async Task<string> ReadStateAsync(bool legacy)
    {
        var projection = $"SELECT \"AppointmentId\", \"Kind\", {DeliveryColumns} FROM \"AppointmentMailOutbox\"";
        if (legacy)
        {
            projection = $"""
                SELECT "Id" AS "AppointmentId", 'Confirmation' AS "Kind", {DeliveryColumns} FROM "AppointmentConfirmations"
                UNION ALL SELECT "Id", 'Cancellation', {DeliveryColumns} FROM "AppointmentCancellationMails"
                UNION ALL SELECT "Id", 'CheckIn', {DeliveryColumns} FROM "AppointmentCheckInMails"
                """;
        }

        await using var connection = new NpgsqlConnection(fixture.Database.RuntimeConnectionString);
        await connection.OpenAsync();
        await using var query = new NpgsqlCommand($"""
            SELECT jsonb_agg(to_jsonb(m) ORDER BY m."AppointmentId", m."Kind")::text FROM ({projection}) m
            """, connection);
        return Assert.IsType<string>(await query.ExecuteScalarAsync());
    }

    private async Task AssertLegacyGuardAsync(Guid appointmentId)
    {
        await using var connection = new NpgsqlConnection(fixture.Database.RuntimeConnectionString);
        await connection.OpenAsync();
        await using var attack = new NpgsqlCommand("""
            UPDATE "AppointmentCheckInMails" SET "RecipientEmail" = 'changed@example.invalid' WHERE "Id" = @appointment
            """, connection);
        attack.Parameters.AddWithValue("appointment", appointmentId);
        var error = await Assert.ThrowsAsync<PostgresException>(() => attack.ExecuteNonQueryAsync());
        Assert.Equal("CK_AppointmentConfirmations_Immutable", error.ConstraintName);
    }

    private async Task AssertQueueGuardsAsync(Guid pendingId, Guid checkedInId)
    {
        const string insert = """
            INSERT INTO "AppointmentMailOutbox" ("Id", "AppointmentId", "Kind", "RecipientEmail", "DeliveryAttempts", "NextDeliveryAt")
            VALUES (@id, @appointment, @kind, 'customer@example.invalid', 0, now())
            """;
        await RejectAsync(insert, pendingId, "Cancellation", PostgresErrorCodes.CheckViolation, "CK_AppointmentMailOutbox_Cancelled");
        await RejectAsync(insert, pendingId, "CheckIn", PostgresErrorCodes.ForeignKeyViolation);
        await RejectAsync(insert, pendingId, "Unknown", PostgresErrorCodes.CheckViolation);
        await RejectAsync(insert, pendingId, "Confirmation", PostgresErrorCodes.UniqueViolation);
        await RejectAsync(insert, Guid.NewGuid(), "Confirmation", PostgresErrorCodes.ForeignKeyViolation);

        var mutations = new[]
        {
            "\"Id\" = @id",
            "\"AppointmentId\" = @id",
            "\"Kind\" = 'Cancellation'",
            "\"RecipientEmail\" = 'changed@example.invalid'",
            "\"DeliveredAt\" = NULL"
        };
        foreach (var mutation in mutations)
        {
            var sql = $"UPDATE \"AppointmentMailOutbox\" SET {mutation} WHERE \"AppointmentId\" = @appointment AND \"Kind\" = 'Confirmation'";
            await RejectAsync(sql, checkedInId, "Confirmation", PostgresErrorCodes.CheckViolation,
                "CK_AppointmentMailOutbox_Immutable");
        }

        await using var maintenance = new NpgsqlConnection(fixture.Database.ConnectionString);
        await maintenance.OpenAsync();
        await using var delete = new NpgsqlCommand("""
            DELETE FROM "AppointmentMailOutbox" WHERE "AppointmentId" = @appointment
            """, maintenance);
        delete.Parameters.AddWithValue("appointment", checkedInId);
        var deletion = await Assert.ThrowsAsync<PostgresException>(() => delete.ExecuteNonQueryAsync());
        Assert.Equal("CK_AppointmentMailOutbox_Immutable", deletion.ConstraintName);
    }

    private async Task RejectAsync(string sql, Guid appointmentId, string kind, string sqlState, string? constraint = null)
    {
        await using var connection = new NpgsqlConnection(fixture.Database.RuntimeConnectionString);
        await connection.OpenAsync();
        await using var attack = new NpgsqlCommand(sql, connection);
        attack.Parameters.AddWithValue("id", Guid.NewGuid());
        attack.Parameters.AddWithValue("appointment", appointmentId);
        attack.Parameters.AddWithValue("kind", kind);
        var error = await Assert.ThrowsAsync<PostgresException>(() => attack.ExecuteNonQueryAsync());
        Assert.Equal(sqlState, error.SqlState);
        if (constraint is not null)
        {
            Assert.Equal(constraint, error.ConstraintName);
        }
    }
}
