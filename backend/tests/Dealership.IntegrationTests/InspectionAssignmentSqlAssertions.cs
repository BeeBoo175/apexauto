using Dealership.Application.Inspections;
using Npgsql;

namespace Dealership.IntegrationTests;

internal static class InspectionAssignmentSqlAssertions
{
    public static async Task VerifyAsync(RegistrationDatabase database, InspectionAssignmentReceipt claim,
        InspectionAssignmentReceipt current, Guid first, Guid second, Guid manager)
    {
        var next = current.TechnicianId == first ? second : first;
        var update = """
            UPDATE "InspectionDrafts" SET "TechnicianId" = @technician, "AssignmentVersion" = 3 WHERE "Id" = @inspection;
            """;
        var insert = """
            INSERT INTO "InspectionAssignments" ("Id", "InspectionId", "BranchId", "ActorId", "TechnicianId",
                "PreviousTechnicianId", "ExpectedVersion", "Version", "Reason", "RecordedAt")
            VALUES (@operation, @inspection, @branch, @manager, @technician, @previous, 2, 3, 'Workshop handover', clock_timestamp());
            """;
        var attacks = new[]
        {
            ("UPDATE \"InspectionAssignments\" SET \"Reason\" = 'Rewritten' WHERE \"Id\" = @claim;", "CK_InspectionAssignments_Immutable"),
            ("UPDATE \"InspectionDrafts\" SET \"CreatedAt\" = clock_timestamp() WHERE \"Id\" = @inspection;", "CK_InspectionDrafts_Immutable"),
            (update, "CK_InspectionDrafts_Receipt"),
            (update + insert.Replace("@manager", "@technician", StringComparison.Ordinal), "CK_InspectionAssignments_Eligibility"),
            (update + insert.Replace("@previous", "@manager", StringComparison.Ordinal), "CK_InspectionAssignments_Chain"),
            (update + insert.Replace("'Workshop handover'", "NULL", StringComparison.Ordinal), "CK_InspectionAssignments_Reason"),
            (update + insert.Replace("@branch", "@other", StringComparison.Ordinal), "CK_InspectionAssignments_Eligibility"),
            ("UPDATE \"AspNetUsers\" SET \"IsActive\" = false WHERE \"Id\" = @manager;" + update + insert, "CK_InspectionAssignments_Eligibility")
        };
        foreach (var (sql, constraint) in attacks)
        {
            await using var connection = new NpgsqlConnection(database.RuntimeConnectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            await using var command = new NpgsqlCommand(sql + " SET CONSTRAINTS ALL IMMEDIATE;", connection, transaction);
            AddParameters(command, claim, current, next, manager);
            var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
            Assert.Equal(constraint, error.ConstraintName);
        }

        await using var maintenance = new NpgsqlConnection(database.ConnectionString);
        await maintenance.OpenAsync();
        await using (var foreignKeys = new NpgsqlCommand("""
            SELECT count(*) FROM pg_constraint WHERE contype = 'f' AND confdeltype = 'r' AND
                conrelid IN ('"InspectionDrafts"'::regclass, '"InspectionAssignments"'::regclass)
            """, maintenance))
        {
            Assert.Equal(6L, await foreignKeys.ExecuteScalarAsync());
        }
        await using (var scopedKeys = new NpgsqlCommand("""
            SELECT count(*) FROM pg_constraint WHERE contype = 'f' AND cardinality(conkey) = 2 AND
                conrelid IN ('"InspectionDrafts"'::regclass, '"InspectionAssignments"'::regclass)
            """, maintenance))
        {
            Assert.Equal(2L, await scopedKeys.ExecuteScalarAsync());
        }
        foreach (var (table, id, constraint) in new[]
        {
            ("InspectionAssignments", claim.OperationId, "CK_InspectionAssignments_Immutable"),
            ("InspectionDrafts", current.InspectionId, "CK_InspectionDrafts_Immutable")
        })
        {
            var sql = $"DELETE FROM \"{table}\" WHERE \"Id\" = @id";
            await using var restricted = new NpgsqlConnection(database.RuntimeConnectionString);
            await restricted.OpenAsync();
            await using var denied = new NpgsqlCommand(sql, restricted);
            denied.Parameters.AddWithValue("id", id);
            var privilege = await Assert.ThrowsAsync<PostgresException>(() => denied.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, privilege.SqlState);
            await using var guarded = new NpgsqlCommand(sql, maintenance);
            guarded.Parameters.AddWithValue("id", id);
            var immutable = await Assert.ThrowsAsync<PostgresException>(() => guarded.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.CheckViolation, immutable.SqlState);
            Assert.Equal(constraint, immutable.ConstraintName);
        }
        foreach (var sql in new[]
        {
            "DELETE FROM \"Vehicles\" WHERE \"Id\" = @vehicle;",
            "DELETE FROM \"AspNetUsers\" WHERE \"Id\" = @technician;"
        })
        {
            await using var command = new NpgsqlCommand(sql, maintenance);
            command.Parameters.AddWithValue("vehicle", current.VehicleId);
            command.Parameters.AddWithValue("technician", current.TechnicianId);
            var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Contains(error.SqlState, new[] { PostgresErrorCodes.ForeignKeyViolation, PostgresErrorCodes.CheckViolation });
        }
    }

    private static void AddParameters(NpgsqlCommand command, InspectionAssignmentReceipt claim,
        InspectionAssignmentReceipt current, Guid technician, Guid manager)
    {
        command.Parameters.AddWithValue("operation", Guid.NewGuid());
        command.Parameters.AddWithValue("claim", claim.OperationId);
        command.Parameters.AddWithValue("inspection", current.InspectionId);
        command.Parameters.AddWithValue("branch", current.BranchId);
        command.Parameters.AddWithValue("manager", manager);
        command.Parameters.AddWithValue("technician", technician);
        command.Parameters.AddWithValue("previous", current.TechnicianId);
        command.Parameters.AddWithValue("other", Guid.NewGuid());
    }
}
