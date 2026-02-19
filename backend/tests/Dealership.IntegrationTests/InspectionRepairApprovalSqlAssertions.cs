using Dealership.Application.Inspections;
using Npgsql;

namespace Dealership.IntegrationTests;

internal static class InspectionRepairApprovalSqlAssertions
{
    public static async Task VerifyBeforeAsync(RegistrationDatabase database, InspectionRepairReceipt request, Guid manager,
        Guid technician, Guid group, Guid outside)
    {
        var receipt = new InspectionRepairApprovalReceipt(Guid.NewGuid(), request.InspectionId, request.BranchId, manager,
            request.OperationId, request.AssignmentVersion, request.Revision, request.Version, DateTimeOffset.UtcNow);
        foreach (var (candidate, constraint) in new[]
        {
            (receipt with { ActorId = technician }, "CK_InspectionRepairApprovals_Eligibility"),
            (receipt with { ActorId = group }, "CK_InspectionRepairApprovals_Eligibility"),
            (receipt with { ActorId = outside }, "CK_InspectionRepairApprovals_Eligibility"),
            (receipt with { BranchId = Guid.NewGuid() }, "CK_InspectionRepairApprovals_Eligibility"),
            (receipt with { RequestId = Guid.NewGuid() }, "CK_InspectionRepairApprovals_Request"),
            (receipt with { AssignmentVersion = 2 }, "CK_InspectionRepairApprovals_Request"),
            (receipt with { Revision = 2 }, "CK_InspectionRepairApprovals_Request"),
            (receipt with { RepairVersion = 2 }, "CK_InspectionRepairApprovals_Request"),
            (receipt with { ApprovedAt = request.RequestedAt.AddSeconds(-1) }, "CK_InspectionRepairApprovals_Request"),
            (receipt with { ApprovedAt = DateTimeOffset.UtcNow.AddDays(1) }, "CK_InspectionRepairApprovals_Request"),
            (receipt with { OperationId = Guid.Empty }, "CK_InspectionRepairApprovals_Shape")
        })
        {
            await using var connection = new NpgsqlConnection(database.RuntimeConnectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            await using var command = Insert(connection, transaction, candidate);
            var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
            Assert.Equal(constraint, error.ConstraintName);
        }
    }

    public static async Task VerifyStaleSnapshotAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        InspectionRepairApprovalReceipt receipt)
    {
        await using var command = Insert(connection, transaction, receipt);
        var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.SerializationFailure, error.SqlState);
    }

    public static async Task VerifyAfterAsync(RegistrationDatabase database, InspectionRepairApprovalReceipt receipt)
    {
        await using var connection = new NpgsqlConnection(database.RuntimeConnectionString);
        await connection.OpenAsync();
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await using var command = Insert(connection, transaction, receipt with { OperationId = Guid.NewGuid() });
            var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
            await transaction.RollbackAsync();
        }
        await using (var command = new NpgsqlCommand("UPDATE \"InspectionRepairApprovals\" SET \"RepairVersion\" = 2 WHERE \"Id\" = @id", connection))
        {
            command.Parameters.AddWithValue("id", receipt.OperationId);
            var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal("CK_InspectionRepairApprovals_Immutable", error.ConstraintName);
        }
        await using (var command = new NpgsqlCommand("DELETE FROM \"InspectionRepairApprovals\" WHERE \"Id\" = @id", connection))
        {
            command.Parameters.AddWithValue("id", receipt.OperationId);
            var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
        }
        await using var maintenance = new NpgsqlConnection(database.ConnectionString);
        await maintenance.OpenAsync();
        await using (var command = new NpgsqlCommand("DELETE FROM \"InspectionRepairApprovals\" WHERE \"Id\" = @id", maintenance))
        {
            command.Parameters.AddWithValue("id", receipt.OperationId);
            var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal("CK_InspectionRepairApprovals_Immutable", error.ConstraintName);
        }
        await using var relationships = new NpgsqlCommand("""
            SELECT count(*) FROM pg_constraint WHERE conrelid = '"InspectionRepairApprovals"'::regclass
                AND contype = 'f' AND confdeltype = 'r';
            """, connection);
        Assert.Equal(3L, await relationships.ExecuteScalarAsync());
    }

    private static NpgsqlCommand Insert(NpgsqlConnection connection, NpgsqlTransaction transaction,
        InspectionRepairApprovalReceipt receipt)
    {
        var command = new NpgsqlCommand("""
            INSERT INTO "InspectionRepairApprovals" ("Id", "InspectionId", "BranchId", "ActorId", "RequestId",
                "AssignmentVersion", "Revision", "RepairVersion", "ApprovedAt")
            VALUES (@id, @inspection, @branch, @actor, @request, @assignment, @revision, @version, @at);
            """, connection, transaction);
        command.Parameters.AddWithValue("id", receipt.OperationId);
        command.Parameters.AddWithValue("inspection", receipt.InspectionId);
        command.Parameters.AddWithValue("branch", receipt.BranchId);
        command.Parameters.AddWithValue("actor", receipt.ActorId);
        command.Parameters.AddWithValue("request", receipt.RequestId);
        command.Parameters.AddWithValue("assignment", receipt.AssignmentVersion);
        command.Parameters.AddWithValue("revision", receipt.Revision);
        command.Parameters.AddWithValue("version", receipt.RepairVersion);
        command.Parameters.AddWithValue("at", receipt.ApprovedAt);
        return command;
    }
}
