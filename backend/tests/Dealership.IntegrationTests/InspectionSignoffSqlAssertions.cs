using System.Text.Json;
using Dealership.Application.Inspections;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Dealership.IntegrationTests;

internal static class InspectionSignoffSqlAssertions
{
    private const string CloneSave = """
        INSERT INTO "InspectionDraftSaves" ("Id", "InspectionId", "BranchId", "ActorId", "AssignmentVersion",
            "ExpectedVersion", "Version", "CatalogVersion", "EntriesJson", "PhotoIdsJson", "ExaminedPointCodesJson", "RecordedAt")
        SELECT gen_random_uuid(), "InspectionId", "BranchId", "ActorId", "AssignmentVersion", "Version", "Version" + 1,
            "CatalogVersion", "EntriesJson", "PhotoIdsJson", "ExaminedPointCodesJson", clock_timestamp()
        FROM "InspectionDraftSaves" WHERE "InspectionId" = @inspection ORDER BY "Version" DESC LIMIT 1;
        """;

    public static async Task VerifyUnsignedAttacksAsync(RegistrationDatabase database, Guid inspection,
        InspectionSignRequest request, Guid saveId)
    {
        await using var context = new DealershipDbContext(new DbContextOptionsBuilder<DealershipDbContext>()
            .UseNpgsql(database.RuntimeConnectionString).Options);
        var draft = await context.InspectionDrafts.SingleAsync(item => item.Id == inspection);
        var vehicle = await context.Vehicles.SingleAsync(item => item.Id == draft.VehicleId);
        var actor = await context.Users.SingleAsync(item => item.Id == draft.TechnicianId);
        var saved = await context.InspectionDraftSaves.SingleAsync(item => item.Id == saveId);
        var photos = await context.InspectionDraftPhotos.Where(item => item.SaveId == saveId)
            .Join(context.InspectionPhotos, link => link.PhotoId, photo => photo.Id, (_, photo) => photo).ToArrayAsync();
        var now = await context.Database.SqlQuery<DateTimeOffset>($"SELECT clock_timestamp() AS \"Value\"").SingleAsync();
        var report = InspectionReport.Sign(request.OperationId, 1, draft, vehicle, actor.UserName!, saved.Receipt(), photos, now);
        var attacks = new[]
        {
            (report with { Vehicle = report.Vehicle with { MileageKilometres = 1 } }, "CK_InspectionSignedRevisions_Report"),
            (report with { TechnicianIdentity = "Rewritten identity" }, "CK_InspectionSignedRevisions_Report"),
            (report with { Assessment = new(500m, Dealership.Domain.Inspections.InspectionGrade.A, false) }, "CK_InspectionSignedRevisions_Report"),
            (report with { Checklist = [report.Checklist[0] with { Description = "Rewritten definition" }, .. report.Checklist.Skip(1)] }, "CK_InspectionSignedRevisions_Report"),
            (report with { Checklist = [.. report.Checklist.Skip(1)] }, "CK_InspectionSignedRevisions_Report"),
            (report with { SaveVersion = 1 }, "CK_InspectionSignedRevisions_Save"),
            (report with { Revision = 2 }, "CK_InspectionSignedRevisions_Save"),
            (report with { BranchId = Guid.NewGuid() }, "CK_InspectionSignedRevisions_Eligibility")
        };
        foreach (var (candidate, constraint) in attacks)
        {
            await using var connection = new NpgsqlConnection(database.RuntimeConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("""
                INSERT INTO "InspectionSignedRevisions" ("Id", "InspectionId", "BranchId", "SaveId", "TechnicianId",
                    "AssignmentVersion", "SaveVersion", "Revision", "SignedAt", "ReportJson")
                VALUES (@id, @inspection, @branch, @save, @technician, @assignment, @version, @revision, @at, @report);
                """, connection);
            command.Parameters.AddWithValue("id", candidate.OperationId);
            command.Parameters.AddWithValue("inspection", inspection);
            command.Parameters.AddWithValue("branch", candidate.BranchId);
            command.Parameters.AddWithValue("save", candidate.SaveId);
            command.Parameters.AddWithValue("technician", candidate.TechnicianId);
            command.Parameters.AddWithValue("assignment", candidate.AssignmentVersion);
            command.Parameters.AddWithValue("version", candidate.SaveVersion);
            command.Parameters.AddWithValue("revision", candidate.Revision);
            command.Parameters.AddWithValue("at", candidate.SignedAt);
            command.Parameters.AddWithValue("report", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(candidate, JsonSerializerOptions.Web));
            var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal(constraint, error.ConstraintName);
        }

        await AssertRejectedAsync(database.RuntimeConnectionString, inspection,
            "UPDATE \"InspectionDrafts\" SET \"IsSigned\" = true WHERE \"Id\" = @inspection; SET CONSTRAINTS ALL IMMEDIATE;",
            "CK_InspectionDrafts_Seal");
    }

    public static async Task VerifyOldSnapshotAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid inspection)
    {
        await using var command = new NpgsqlCommand(CloneSave, connection, transaction);
        command.Parameters.AddWithValue("inspection", inspection);
        var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.SerializationFailure, error.SqlState);
    }

    public static async Task VerifySignedAttacksAsync(RegistrationDatabase database, InspectionReport report)
    {
        foreach (var (sql, constraint) in new[]
        {
            ("UPDATE \"InspectionSignedRevisions\" SET \"ReportJson\" = '{}' WHERE \"InspectionId\" = @inspection", "CK_InspectionSignedRevisions_Immutable"),
            ("UPDATE \"InspectionDrafts\" SET \"IsSigned\" = false WHERE \"Id\" = @inspection", "CK_InspectionDrafts_Immutable"),
            ("UPDATE \"InspectionDrafts\" SET \"Revision\" = 2 WHERE \"Id\" = @inspection", "CK_InspectionDrafts_Immutable"),
            (CloneSave, "CK_InspectionDrafts_Signed"),
            ("UPDATE \"InspectionPhotos\" SET \"AvailableAt\" = clock_timestamp() WHERE \"InspectionId\" = @inspection AND \"AvailableAt\" IS NULL", "CK_InspectionDrafts_Signed")
        })
        {
            await AssertRejectedAsync(database.RuntimeConnectionString, report.InspectionId, sql, constraint);
        }

        const string delete = "DELETE FROM \"InspectionSignedRevisions\" WHERE \"InspectionId\" = @inspection";
        await using var runtime = new NpgsqlConnection(database.RuntimeConnectionString);
        await runtime.OpenAsync();
        await using var denied = new NpgsqlCommand(delete, runtime);
        denied.Parameters.AddWithValue("inspection", report.InspectionId);
        var privilege = await Assert.ThrowsAsync<PostgresException>(() => denied.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, privilege.SqlState);
        await AssertRejectedAsync(database.ConnectionString, report.InspectionId, delete, "CK_InspectionSignedRevisions_Immutable");

        await using var relationships = new NpgsqlCommand("""
            SELECT count(*) FROM pg_constraint WHERE conrelid = '"InspectionSignedRevisions"'::regclass
                AND contype = 'f' AND confdeltype = 'r';
            """, runtime);
        Assert.Equal(3L, await relationships.ExecuteScalarAsync());
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
