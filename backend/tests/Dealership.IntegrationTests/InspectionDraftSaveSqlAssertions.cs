using Dealership.Application.Inspections;
using Dealership.Domain.Inspections;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace Dealership.IntegrationTests;

internal static class InspectionDraftSaveSqlAssertions
{
    public static async Task VerifyAsync(RegistrationDatabase fixture, InspectionDraftSaveReceipt current,
        Guid branch, Guid otherTechnician, Guid manager)
    {
        const string insert = """
            INSERT INTO "InspectionDraftSaves" ("Id", "InspectionId", "BranchId", "ActorId", "AssignmentVersion",
                "ExpectedVersion", "Version", "CatalogVersion", "EntriesJson", "RecordedAt")
            VALUES (@operation, @inspection, @branch, @actor, 1, 2, 3, 'apex-inspection-1', @entries, clock_timestamp());
            """;
        const string validEntry = """
            [{"pointCode":"IE-040","kind":1,"severity":null,"estimatedRepairCost":0,"notes":"No powered roof fitted"}]
            """;
        var attacks = new[]
        {
            (insert.Replace(", 1, 2, 3,", ", 2, 2, 3,", StringComparison.Ordinal), validEntry, current.ActorId, "CK_InspectionDraftSaves_Assignment"),
            (insert.Replace(", 1, 2, 3,", ", 1, 0, 1,", StringComparison.Ordinal), validEntry, current.ActorId, "CK_InspectionDraftSaves_Version"),
            (insert.Replace("clock_timestamp()", "'infinity'::timestamptz", StringComparison.Ordinal), validEntry, current.ActorId, "CK_InspectionDraftSaves_Version"),
            (insert, validEntry, otherTechnician, "CK_InspectionDraftSaves_Assignment"),
            (insert, validEntry, manager, "CK_InspectionDraftSaves_Eligibility"),
            (insert, "{}", current.ActorId, "CK_InspectionDraftSaves_Entries"),
            (insert, "[null]", current.ActorId, "CK_InspectionDraftSaves_Entries"),
            (insert, validEntry.Replace("IE-040", "IE-041", StringComparison.Ordinal), current.ActorId, "CK_InspectionDraftSaves_Entries"),
            (insert, validEntry.Replace("IE-040", "FC-000", StringComparison.Ordinal), current.ActorId, "CK_InspectionDraftSaves_Entries"),
            (insert, validEntry.Replace("\"kind\":1", "\"kind\":99", StringComparison.Ordinal), current.ActorId, "CK_InspectionDraftSaves_Entries"),
            (insert, validEntry.Replace("\"severity\":null", "\"severity\":0", StringComparison.Ordinal), current.ActorId, "CK_InspectionDraftSaves_Entries"),
            (insert, validEntry.Replace("\"kind\":1", "\"kind\":0", StringComparison.Ordinal), current.ActorId, "CK_InspectionDraftSaves_Entries"),
            (insert, validEntry.Replace("\"estimatedRepairCost\":0", "\"estimatedRepairCost\":0.001", StringComparison.Ordinal), current.ActorId, "CK_InspectionDraftSaves_Entries"),
            (insert, validEntry.Replace("No powered roof fitted", " ", StringComparison.Ordinal), current.ActorId, "CK_InspectionDraftSaves_Entries"),
            (insert, validEntry.Replace("No powered roof fitted", "\u00a0\u202f", StringComparison.Ordinal), current.ActorId, "CK_InspectionDraftSaves_Entries"),
            (insert, validEntry.Replace("No powered roof fitted", "reason\u0081", StringComparison.Ordinal), current.ActorId, "CK_InspectionDraftSaves_Entries"),
            (insert, validEntry.Replace("IE-040", "FC-001", StringComparison.Ordinal), current.ActorId, "CK_InspectionDraftSaves_Entries"),
            (insert, validEntry.Replace("No powered roof fitted", string.Concat(Enumerable.Repeat("\U0001F600", 501)), StringComparison.Ordinal), current.ActorId, "CK_InspectionDraftSaves_Entries"),
            (insert, """
                [{"pointCode":"FC-001","kind":0,"severity":0,"estimatedRepairCost":79228162514264337593543950334,"notes":"Observed"},
                 {"pointCode":"FC-002","kind":0,"severity":0,"estimatedRepairCost":0.01,"notes":"Observed"},
                 {"pointCode":"FC-003","kind":0,"severity":0,"estimatedRepairCost":0.99,"notes":"Observed"}]
                """, current.ActorId, "CK_InspectionDraftSaves_Entries"),
            (insert, "[" + validEntry[1..^1] + "," + validEntry[1..^1] + "]", current.ActorId, "CK_InspectionDraftSaves_Entries"),
            ("UPDATE \"InspectionDraftSaves\" SET \"EntriesJson\" = '[]' WHERE \"Id\" = @existing;", validEntry, current.ActorId, "CK_InspectionDraftSaves_Immutable"),
            ("UPDATE \"AspNetUsers\" SET \"IsActive\" = false WHERE \"Id\" = @actor;" + insert, validEntry, current.ActorId, "CK_InspectionDraftSaves_Eligibility"),
            ("UPDATE \"Branches\" SET \"IsActive\" = false WHERE \"Id\" = @branch;" + insert, validEntry, current.ActorId, "CK_InspectionDraftSaves_Eligibility")
        };
        foreach (var (sql, entries, actor, constraint) in attacks)
        {
            await using var connection = new NpgsqlConnection(fixture.RuntimeConnectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("operation", Guid.NewGuid());
            command.Parameters.AddWithValue("inspection", current.InspectionId);
            command.Parameters.AddWithValue("branch", branch);
            command.Parameters.AddWithValue("actor", actor);
            command.Parameters.AddWithValue("existing", current.OperationId);
            command.Parameters.AddWithValue("entries", NpgsqlDbType.Jsonb, entries);
            var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
            Assert.Equal(constraint, error.ConstraintName);
        }

        foreach (var (connectionString, sqlState) in new[]
        {
            (fixture.RuntimeConnectionString, PostgresErrorCodes.InsufficientPrivilege),
            (fixture.ConnectionString, PostgresErrorCodes.CheckViolation)
        })
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("DELETE FROM \"InspectionDraftSaves\" WHERE \"Id\" = @id", connection);
            command.Parameters.AddWithValue("id", current.OperationId);
            var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal(sqlState, error.SqlState);
        }

        await using var schema = new NpgsqlConnection(fixture.RuntimeConnectionString);
        await schema.OpenAsync();
        await using var catalog = new NpgsqlCommand("SELECT valid_inspection_draft_entries(@entries)", schema);
        var parameter = catalog.Parameters.Add("entries", NpgsqlDbType.Jsonb);
        foreach (var point in InspectionCatalog.Points)
        {
            parameter.Value = JsonSerializer.Serialize(new[]
            {
                new InspectionDraftEntryInput(point.Code, InspectionEntryKind.NotFitted, null, 0, "Optional equipment absent")
            }, JsonSerializerOptions.Web);
            Assert.Equal(point.Description.Contains("where fitted", StringComparison.Ordinal), await catalog.ExecuteScalarAsync());
        }

        // JSONB spacing and UTF-8 expansion must fit every domain-valid complete exception set.
        var large = InspectionCatalog.Points.Select(point => new InspectionDraftEntryInput(point.Code,
            InspectionEntryKind.Finding, InspectionSeverity.Advisory, 0, new string('\u8f66', 1000))).ToArray();
        parameter.Value = JsonSerializer.Serialize(large, JsonSerializerOptions.Web);
        Assert.Equal(true, await catalog.ExecuteScalarAsync());
        await using var keys = new NpgsqlCommand("""
            SELECT count(*) FROM pg_constraint WHERE contype = 'f' AND confdeltype = 'r'
                AND conrelid = '"InspectionDraftSaves"'::regclass
            """, schema);
        Assert.Equal(2L, await keys.ExecuteScalarAsync());
    }
}
