using Dealership.Application.Inspections;
using Npgsql;
using NpgsqlTypes;

namespace Dealership.IntegrationTests;

internal static class InspectionRepairCompletionSqlAssertions
{
    public static async Task VerifyBeforeAsync(RegistrationDatabase database, InspectionRepairCompletionReceipt receipt,
        Guid manager, Guid colleague, DateTimeOffset requestedAt)
    {
        foreach (var (candidate, constraint) in new[]
        {
            (receipt with { ActorId = manager }, "CK_InspectionRepairCompletions_Eligibility"),
            (receipt with { ActorId = colleague }, "CK_InspectionRepairCompletions_Request"),
            (receipt with { BranchId = Guid.NewGuid() }, "CK_InspectionRepairCompletions_Eligibility"),
            (receipt with { RequestId = Guid.NewGuid() }, "CK_InspectionRepairCompletions_Request"),
            (receipt with { AssignmentVersion = 2 }, "CK_InspectionRepairCompletions_Request"),
            (receipt with { Revision = 2 }, "CK_InspectionRepairCompletions_Request"),
            (receipt with { RepairVersion = 1 }, "CK_InspectionRepairCompletions_Request"),
            (receipt with { ApprovalId = null }, "CK_InspectionRepairCompletions_Approval"),
            (receipt with { ApprovalId = Guid.NewGuid() }, "CK_InspectionRepairCompletions_Approval"),
            (receipt with { CompletedAt = requestedAt.AddSeconds(-1) }, "CK_InspectionRepairCompletions_Request"),
            (receipt with { CompletedAt = DateTimeOffset.UtcNow.AddDays(1) }, "CK_InspectionRepairCompletions_Request"),
            (receipt with { OperationId = Guid.Empty }, "CK_InspectionRepairCompletions_Shape"),
            (receipt with { Notes = " " }, "CK_InspectionRepairCompletions_Notes")
        })
        {
            await ExpectAsync(database, candidate, constraint);
        }
    }

    public static async Task ExpectAsync(RegistrationDatabase database, InspectionRepairCompletionReceipt receipt, string constraint)
    {
        await using var connection = new NpgsqlConnection(database.RuntimeConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var command = Insert(connection, transaction, receipt);
        var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal(constraint, error.ConstraintName);
    }

    public static async Task VerifyStaleAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        InspectionRepairCompletionReceipt receipt)
    {
        await using var command = Insert(connection, transaction, receipt);
        var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.SerializationFailure, error.SqlState);
    }

    public static async Task VerifyAfterAsync(RegistrationDatabase database, InspectionRepairCompletionReceipt receipt)
    {
        await using var connection = new NpgsqlConnection(database.RuntimeConnectionString);
        await connection.OpenAsync();
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await using var command = Insert(connection, transaction, receipt with { OperationId = Guid.NewGuid(), CompletedAt = DateTimeOffset.UtcNow });
            var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
            await transaction.RollbackAsync();
        }
        await using (var command = new NpgsqlCommand("UPDATE \"InspectionRepairCompletions\" SET \"Notes\" = 'Changed' WHERE \"Id\" = @id", connection))
        {
            command.Parameters.AddWithValue("id", receipt.OperationId);
            var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal("CK_InspectionRepairCompletions_Immutable", error.ConstraintName);
        }
        await using (var command = new NpgsqlCommand("DELETE FROM \"InspectionRepairCompletions\" WHERE \"Id\" = @id", connection))
        {
            command.Parameters.AddWithValue("id", receipt.OperationId);
            var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
        }
        await using var maintenance = new NpgsqlConnection(database.ConnectionString);
        await maintenance.OpenAsync();
        await using (var command = new NpgsqlCommand("DELETE FROM \"InspectionRepairCompletions\" WHERE \"Id\" = @id", maintenance))
        {
            command.Parameters.AddWithValue("id", receipt.OperationId);
            var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal("CK_InspectionRepairCompletions_Immutable", error.ConstraintName);
        }
        await using var relationships = new NpgsqlCommand("""
            SELECT count(*) FROM pg_constraint WHERE conrelid = '"InspectionRepairCompletions"'::regclass
                AND contype = 'f' AND confdeltype = 'r';
            """, connection);
        Assert.Equal(4L, await relationships.ExecuteScalarAsync());
    }

    private static NpgsqlCommand Insert(NpgsqlConnection connection, NpgsqlTransaction transaction,
        InspectionRepairCompletionReceipt receipt)
    {
        var command = new NpgsqlCommand("""
            INSERT INTO "InspectionRepairCompletions" ("Id", "InspectionId", "BranchId", "ActorId", "RequestId", "ApprovalId",
                "AssignmentVersion", "Revision", "RepairVersion", "Notes", "CompletedAt")
            VALUES (@id, @inspection, @branch, @actor, @request, @approval, @assignment, @revision, @version, @notes, @at);
            """, connection, transaction);
        command.Parameters.AddWithValue("id", receipt.OperationId);
        command.Parameters.AddWithValue("inspection", receipt.InspectionId);
        command.Parameters.AddWithValue("branch", receipt.BranchId);
        command.Parameters.AddWithValue("actor", receipt.ActorId);
        command.Parameters.AddWithValue("request", receipt.RequestId);
        command.Parameters.Add("approval", NpgsqlDbType.Uuid).Value = (object?)receipt.ApprovalId ?? DBNull.Value;
        command.Parameters.AddWithValue("assignment", receipt.AssignmentVersion);
        command.Parameters.AddWithValue("revision", receipt.Revision);
        command.Parameters.AddWithValue("version", receipt.RepairVersion);
        command.Parameters.AddWithValue("notes", receipt.Notes);
        command.Parameters.AddWithValue("at", receipt.CompletedAt);
        return command;
    }
}
