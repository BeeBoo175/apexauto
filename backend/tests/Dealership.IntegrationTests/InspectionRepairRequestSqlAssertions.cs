using System.Text.Json;
using Dealership.Application.Inspections;
using Dealership.Domain.Inspections;
using Npgsql;
using NpgsqlTypes;

namespace Dealership.IntegrationTests;

internal static class InspectionRepairRequestSqlAssertions
{
    public static async Task VerifyBeforeAsync(RegistrationDatabase database, InspectionReport report, Guid manager,
        Guid colleague, Guid outside)
    {
        var receipt = Candidate(report, 0);
        foreach (var (candidate, constraint) in new[]
        {
            (receipt with { ActorId = manager }, "CK_InspectionRepairRequests_Eligibility"),
            (receipt with { ActorId = outside }, "CK_InspectionRepairRequests_Eligibility"),
            (receipt with { ActorId = colleague }, "CK_InspectionRepairRequests_Revision"),
            (receipt with { BranchId = Guid.NewGuid() }, "CK_InspectionRepairRequests_Eligibility"),
            (receipt with { AssignmentVersion = 2 }, "CK_InspectionRepairRequests_Revision"),
            (receipt with { Revision = 2 }, "CK_InspectionRepairRequests_Revision"),
            (receipt with { SignedRevisionId = Guid.NewGuid() }, "CK_InspectionRepairRequests_Revision"),
            (receipt with { ExpectedVersion = 1, Version = 2 }, "CK_InspectionRepairRequests_Revision"),
            (receipt with { RequestedAt = DateTimeOffset.UtcNow.AddDays(1) }, "CK_InspectionRepairRequests_Revision"),
            (receipt with { Budget = -1 }, "CK_InspectionRepairRequests_Shape"),
            (receipt with { Budget = 0.001m }, "CK_InspectionRepairRequests_Budget"),
            (receipt with { Budget = 10000000000000000m }, "CK_InspectionRepairRequests_Budget"),
            (receipt with { Items = [] }, "CK_InspectionRepairRequests_Items"),
            (receipt with { Items = [new("EP-004", " ")] }, "CK_InspectionRepairRequests_Items"),
            (receipt with { Items = [new("EP-004", "\u2003")] }, "CK_InspectionRepairRequests_Items"),
            (receipt with { Items = [new("EP-004", "line\nbreak")] }, "CK_InspectionRepairRequests_Items"),
            (receipt with { Items = [new("EP-004", string.Concat(Enumerable.Repeat("😀", 501)))] }, "CK_InspectionRepairRequests_Items"),
            (receipt with { Items = [new("EP-004", "Refinish"), new("EP-004", "Replace")] }, "CK_InspectionRepairRequests_Items"),
            (receipt with { Items = [new("IE-040", "Repair absent roof")] }, "CK_InspectionRepairRequests_Findings")
        })
        {
            await AssertInsertRejectedAsync(database.RuntimeConnectionString, candidate, constraint);
        }

        await AssertRejectedAsync(database.RuntimeConnectionString, report.InspectionId, """
            UPDATE "InspectionDrafts" SET "RepairVersion" = 1 WHERE "Id" = @inspection;
            SET CONSTRAINTS ALL IMMEDIATE;
            """, "CK_InspectionDrafts_RepairRequest");
    }

    public static Task VerifyRejectedRevisionAsync(RegistrationDatabase database, InspectionReport report, long expectedVersion) =>
        AssertInsertRejectedAsync(database.RuntimeConnectionString, Candidate(report, expectedVersion) with
        {
            Items = [new("FC-042", "Dry cabin")]
        }, "CK_InspectionRepairRequests_Vehicle");

    public static async Task VerifyAfterAsync(RegistrationDatabase database, InspectionRepairReceipt receipt)
    {
        await AssertRejectedAsync(database.RuntimeConnectionString, receipt.InspectionId,
            "UPDATE \"InspectionRepairRequests\" SET \"Budget\" = 1 WHERE \"InspectionId\" = @inspection", "CK_InspectionRepairRequests_Immutable");
        await AssertRejectedAsync(database.ConnectionString, receipt.InspectionId,
            "DELETE FROM \"InspectionRepairRequests\" WHERE \"InspectionId\" = @inspection", "CK_InspectionRepairRequests_Immutable");
        await AssertRejectedAsync(database.RuntimeConnectionString, receipt.InspectionId,
            "UPDATE \"InspectionDrafts\" SET \"RepairVersion\" = 0 WHERE \"Id\" = @inspection", "CK_InspectionDrafts_Immutable");
        await AssertInsertRejectedAsync(database.RuntimeConnectionString, receipt with { OperationId = Guid.NewGuid() },
            "CK_InspectionRepairRequests_Revision");
        await using var connection = new NpgsqlConnection(database.RuntimeConnectionString);
        await connection.OpenAsync();
        await using var relationships = new NpgsqlCommand("""
            SELECT count(*) FROM pg_constraint WHERE conrelid = '"InspectionRepairRequests"'::regclass
                AND contype = 'f' AND confdeltype = 'r';
            """, connection);
        Assert.Equal(3L, await relationships.ExecuteScalarAsync());
        await using var command = new NpgsqlCommand("DELETE FROM \"InspectionRepairRequests\" WHERE \"InspectionId\" = @inspection", connection);
        command.Parameters.AddWithValue("inspection", receipt.InspectionId);
        var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
    }

    private static InspectionRepairReceipt Candidate(InspectionReport report, long expectedVersion) => new(Guid.NewGuid(),
        report.InspectionId, report.BranchId, report.TechnicianId, report.OperationId, report.AssignmentVersion,
        report.Revision, expectedVersion, expectedVersion + 1, [new("EP-004", "Refinish bumper")], 250m, DateTimeOffset.UtcNow);

    private static async Task AssertInsertRejectedAsync(string connectionString, InspectionRepairReceipt receipt, string constraint)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var command = new NpgsqlCommand("""
            INSERT INTO "InspectionRepairRequests" ("Id", "InspectionId", "BranchId", "ActorId", "SignedRevisionId",
                "AssignmentVersion", "Revision", "ExpectedVersion", "Version", "ItemsJson", "Budget", "RequestedAt")
            VALUES (@id, @inspection, @branch, @actor, @signed, @assignment, @revision, @expected, @version, @items, @budget, @at);
            """, connection, transaction);
        command.Parameters.AddWithValue("id", receipt.OperationId);
        command.Parameters.AddWithValue("inspection", receipt.InspectionId);
        command.Parameters.AddWithValue("branch", receipt.BranchId);
        command.Parameters.AddWithValue("actor", receipt.ActorId);
        command.Parameters.AddWithValue("signed", receipt.SignedRevisionId);
        command.Parameters.AddWithValue("assignment", receipt.AssignmentVersion);
        command.Parameters.AddWithValue("revision", receipt.Revision);
        command.Parameters.AddWithValue("expected", receipt.ExpectedVersion);
        command.Parameters.AddWithValue("version", receipt.Version);
        command.Parameters.AddWithValue("items", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(receipt.Items, JsonSerializerOptions.Web));
        command.Parameters.AddWithValue("budget", receipt.Budget);
        command.Parameters.AddWithValue("at", receipt.RequestedAt);
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
