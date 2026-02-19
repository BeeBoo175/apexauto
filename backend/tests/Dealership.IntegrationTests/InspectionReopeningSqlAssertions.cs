using System.Text.Json;
using Dealership.Application.Inspections;
using Npgsql;
using NpgsqlTypes;

namespace Dealership.IntegrationTests;

internal static class InspectionReopeningSqlAssertions
{
    public static async Task VerifyBeforeAsync(RegistrationDatabase database, InspectionReport report, Guid manager,
        Guid technician, Guid outside)
    {
        var receipt = new InspectionReopenReceipt(Guid.NewGuid(), report.InspectionId, report.BranchId, manager,
            report.OperationId, 1, 1, 2, 1, "Manager requests fresh examination", DateTimeOffset.UtcNow);
        foreach (var (candidate, constraint) in new[]
        {
            (receipt with { ActorId = technician }, "CK_InspectionReopenings_Eligibility"),
            (receipt with { ActorId = outside }, "CK_InspectionReopenings_Eligibility"),
            (receipt with { AssignmentVersion = 2 }, "CK_InspectionReopenings_Revision"),
            (receipt with { ExpectedRevision = 2, Revision = 3 }, "CK_InspectionReopenings_Revision"),
            (receipt with { SignedRevisionId = Guid.NewGuid() }, "CK_InspectionReopenings_Revision"),
            (receipt with { AfterSaveVersion = 2 }, "CK_InspectionReopenings_Revision"),
            (receipt with { RecordedAt = DateTimeOffset.UtcNow.AddDays(1) }, "CK_InspectionReopenings_Revision"),
            (receipt with { Reason = " " }, "CK_InspectionReopenings_Reason"),
            (receipt with { Reason = "line\nbreak" }, "CK_InspectionReopenings_Reason")
        })
        {
            await AssertInsertRejectedAsync(database.RuntimeConnectionString, candidate, constraint);
        }

        await AssertRejectedAsync(database.RuntimeConnectionString, report.InspectionId, """
            UPDATE "InspectionDrafts" SET "IsSigned" = false, "Revision" = 2, "ReopenedAfterSaveVersion" = 1
                WHERE "Id" = @inspection;
            SET CONSTRAINTS ALL IMMEDIATE;
            """, "CK_InspectionDrafts_Reopening");
    }

    public static async Task VerifyAfterAsync(RegistrationDatabase database, InspectionReport report, InspectionReopenReceipt receipt)
    {
        await AssertRejectedAsync(database.RuntimeConnectionString, report.InspectionId,
            "UPDATE \"InspectionReopenings\" SET \"Reason\" = 'Changed reason' WHERE \"InspectionId\" = @inspection",
            "CK_InspectionReopenings_Immutable");
        await AssertRejectedAsync(database.RuntimeConnectionString, report.InspectionId,
            "UPDATE \"InspectionDrafts\" SET \"ReopenedAfterSaveVersion\" = 0 WHERE \"Id\" = @inspection",
            "CK_InspectionDrafts_Immutable");
        await AssertRejectedAsync(database.ConnectionString, report.InspectionId,
            "DELETE FROM \"InspectionReopenings\" WHERE \"InspectionId\" = @inspection", "CK_InspectionReopenings_Immutable");
        await AssertInsertRejectedAsync(database.RuntimeConnectionString, receipt with { OperationId = Guid.NewGuid() },
            "CK_InspectionReopenings_Revision");

        await using var connection = new NpgsqlConnection(database.RuntimeConnectionString);
        await connection.OpenAsync();
        await using (var command = new NpgsqlCommand("""
            INSERT INTO "InspectionSignedRevisions" ("Id", "InspectionId", "BranchId", "SaveId", "TechnicianId",
                "AssignmentVersion", "SaveVersion", "Revision", "SignedAt", "ReportJson")
            VALUES (@id, @inspection, @branch, @save, @technician, 1, 1, 2, @at, @report);
            """, connection))
        {
            var candidate = report with { OperationId = Guid.NewGuid(), Revision = 2 };
            command.Parameters.AddWithValue("id", candidate.OperationId);
            command.Parameters.AddWithValue("inspection", report.InspectionId);
            command.Parameters.AddWithValue("branch", report.BranchId);
            command.Parameters.AddWithValue("save", report.SaveId);
            command.Parameters.AddWithValue("technician", report.TechnicianId);
            command.Parameters.AddWithValue("at", report.SignedAt);
            command.Parameters.AddWithValue("report", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(candidate, JsonSerializerOptions.Web));
            var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal("CK_InspectionSignedRevisions_FreshSave", error.ConstraintName);
        }
        await using (var command = new NpgsqlCommand("DELETE FROM \"InspectionReopenings\" WHERE \"InspectionId\" = @inspection", connection))
        {
            command.Parameters.AddWithValue("inspection", report.InspectionId);
            var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
        }
        await using var relationships = new NpgsqlCommand("""
            SELECT count(*) FROM pg_constraint WHERE conrelid = '"InspectionReopenings"'::regclass
                AND contype = 'f' AND confdeltype = 'r';
            """, connection);
        Assert.Equal(3L, await relationships.ExecuteScalarAsync());
    }

    public static async Task VerifyOldSnapshotAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid inspection)
    {
        await using var command = new NpgsqlCommand("""
            SELECT "IsSigned" FROM "InspectionDrafts" WHERE "Id" = @inspection FOR UPDATE;
            """, connection, transaction);
        command.Parameters.AddWithValue("inspection", inspection);
        var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.SerializationFailure, error.SqlState);
    }

    private static async Task AssertInsertRejectedAsync(string connectionString, InspectionReopenReceipt receipt, string constraint)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var command = new NpgsqlCommand("""
            INSERT INTO "InspectionReopenings" ("Id", "InspectionId", "BranchId", "ActorId", "SignedRevisionId",
                "AssignmentVersion", "ExpectedRevision", "Revision", "AfterSaveVersion", "Reason", "RecordedAt")
            VALUES (@id, @inspection, @branch, @actor, @signed, @assignment, @expected, @revision, @version, @reason, @at);
            """, connection, transaction);
        command.Parameters.AddWithValue("id", receipt.OperationId);
        command.Parameters.AddWithValue("inspection", receipt.InspectionId);
        command.Parameters.AddWithValue("branch", receipt.BranchId);
        command.Parameters.AddWithValue("actor", receipt.ActorId);
        command.Parameters.AddWithValue("signed", receipt.SignedRevisionId);
        command.Parameters.AddWithValue("assignment", receipt.AssignmentVersion);
        command.Parameters.AddWithValue("expected", receipt.ExpectedRevision);
        command.Parameters.AddWithValue("revision", receipt.Revision);
        command.Parameters.AddWithValue("version", receipt.AfterSaveVersion);
        command.Parameters.AddWithValue("reason", receipt.Reason);
        command.Parameters.AddWithValue("at", receipt.RecordedAt);
        var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal(constraint, error.ConstraintName);
    }

    private static async Task AssertRejectedAsync(string connectionString, Guid inspection, string sql, string constraint)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("inspection", inspection);
        var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal(constraint, error.ConstraintName);
    }
}
