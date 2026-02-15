using Npgsql;

namespace Dealership.IntegrationTests;

internal static class CancellationPersistenceAssertions
{
    public static async Task VerifyAsync(NpgsqlConnection connection, Guid cancelledId, Guid bookedId)
    {
        var immutableChanges = new[]
        {
            "\"OwnerId\" = @other",
            "\"BranchId\" = @other",
            "\"RegistrationPlate\" = 'CHANGED'",
            "\"StartsAt\" = \"StartsAt\" + interval '1 day'",
            "\"EndsAt\" = \"EndsAt\" + interval '1 day'",
            "\"CreatedAt\" = \"CreatedAt\" - interval '1 day'",
            "\"ValuationMinimum\" = \"ValuationMinimum\" + 1",
            "\"CancelledAt\" = NULL, \"CancellationPhotosExpireAt\" = NULL",
            "\"CancellationPhotosExpireAt\" = \"CancellationPhotosExpireAt\" + interval '1 day'"
        };
        foreach (var change in immutableChanges)
        {
            await RejectAsync(connection, cancelledId,
                $"UPDATE \"IntakeAppointments\" SET {change} WHERE \"Id\" = @id", "CK_IntakeAppointments_Immutable");
        }

        var invalidCancellations = new[]
        {
            (Change: "\"CancellationPhotosExpireAt\" = clock_timestamp()", Constraint: "CK_IntakeAppointments_Cancellation"),
            (Change: "\"CancelledAt\" = clock_timestamp()", Constraint: "CK_IntakeAppointments_Cancellation"),
            (Change: "\"CancelledAt\" = clock_timestamp(), \"CancellationPhotosExpireAt\" = '2000-01-01'", Constraint: "CK_IntakeAppointments_Cancellation"),
            (Change: "\"CancelledAt\" = \"StartsAt\", \"CancellationPhotosExpireAt\" = \"EndsAt\"", Constraint: "CK_IntakeAppointments_CancellationTime"),
            (Change: "\"CancelledAt\" = \"CreatedAt\" - interval '1 second', \"CancellationPhotosExpireAt\" = \"EndsAt\"", Constraint: "CK_IntakeAppointments_CancellationTime")
        };
        foreach (var (change, constraint) in invalidCancellations)
        {
            await RejectAsync(connection, bookedId,
                $"UPDATE \"IntakeAppointments\" SET {change} WHERE \"Id\" = @id", constraint);
        }

        const string insertMail = """
            INSERT INTO "AppointmentMailOutbox" ("Id", "AppointmentId", "Kind", "RecipientEmail", "DeliveryAttempts", "NextDeliveryAt")
            VALUES (@other, @id, 'Cancellation', 'customer@example.invalid', 0, clock_timestamp())
            """;
        await RejectAsync(connection, bookedId, insertMail, "CK_AppointmentMailOutbox_Cancelled");

        // The deferred parent guard permits EF's mail-first ordering within one atomic transaction.
        await using var transaction = await connection.BeginTransactionAsync();
        await using var bundle = new NpgsqlCommand(insertMail + ";" + """
            UPDATE "IntakeAppointments" SET "CancelledAt" = clock_timestamp(),
                "CancellationPhotosExpireAt" = clock_timestamp() + interval '30 days' WHERE "Id" = @id;
            """, connection, transaction);
        bundle.Parameters.AddWithValue("id", bookedId);
        bundle.Parameters.AddWithValue("other", Guid.NewGuid());
        Assert.Equal(2, await bundle.ExecuteNonQueryAsync());
        await transaction.CommitAsync();
    }

    private static async Task RejectAsync(NpgsqlConnection connection, Guid appointmentId, string sql, string constraint)
    {
        await using var mutation = new NpgsqlCommand(sql, connection);
        mutation.Parameters.AddWithValue("id", appointmentId);
        mutation.Parameters.AddWithValue("other", Guid.NewGuid());
        var error = await Assert.ThrowsAsync<PostgresException>(() => mutation.ExecuteNonQueryAsync());
        Assert.Equal(constraint, error.ConstraintName);
    }
}
