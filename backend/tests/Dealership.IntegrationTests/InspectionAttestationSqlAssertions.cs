using System.Text.Json;
using Dealership.Application.Inspections;
using Dealership.Domain.Inspections;
using Npgsql;
using NpgsqlTypes;

namespace Dealership.IntegrationTests;

internal static class InspectionAttestationSqlAssertions
{
    public static async Task VerifyAsync(RegistrationDatabase fixture, InspectionDraftSaveReceipt current, Guid branch)
    {
        var all = InspectionCatalog.Points.Select(point => point.Code).ToArray();
        var invalid = new[]
        {
            "[]", "null", "{}", "[null]", "[1]",
            JsonSerializer.Serialize(all.SkipLast(1)),
            JsonSerializer.Serialize(all.Skip(1).Append(all[1])),
            JsonSerializer.Serialize(all.Skip(1).Append("FC-000")),
            JsonSerializer.Serialize(all.Append(all[0]))
        };
        foreach (var codes in invalid)
        {
            await using var connection = new NpgsqlConnection(fixture.RuntimeConnectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            await using var command = new NpgsqlCommand("""
                INSERT INTO "InspectionDraftSaves" ("Id", "InspectionId", "BranchId", "ActorId", "AssignmentVersion",
                    "ExpectedVersion", "Version", "CatalogVersion", "EntriesJson", "ExaminedPointCodesJson", "RecordedAt")
                VALUES (@operation, @inspection, @branch, @actor, 1, 2, 3, 'apex-inspection-1', '[]', @codes, clock_timestamp());
                """, connection, transaction);
            command.Parameters.AddWithValue("operation", Guid.NewGuid());
            command.Parameters.AddWithValue("inspection", current.InspectionId);
            command.Parameters.AddWithValue("branch", branch);
            command.Parameters.AddWithValue("actor", current.ActorId);
            command.Parameters.AddWithValue("codes", NpgsqlDbType.Jsonb, codes);
            var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
            Assert.Equal("CK_InspectionDraftSaves_Attestation", error.ConstraintName);
        }

        await using var schema = new NpgsqlConnection(fixture.RuntimeConnectionString);
        await schema.OpenAsync();
        await using var check = new NpgsqlCommand("SELECT valid_inspection_attestation(@entries, @codes)", schema);
        var entries = check.Parameters.Add("entries", NpgsqlDbType.Jsonb);
        var codesParameter = check.Parameters.Add("codes", NpgsqlDbType.Jsonb);
        foreach (var point in InspectionCatalog.Points.Where(point => point.Description.Contains("where fitted", StringComparison.Ordinal)))
        {
            entries.Value = JsonSerializer.Serialize(new[]
            {
                new InspectionDraftEntryInput(point.Code, InspectionEntryKind.NotFitted, null, 0, "Equipment absent after examination")
            }, JsonSerializerOptions.Web);
            codesParameter.Value = JsonSerializer.Serialize(all.Where(code => code != point.Code));
            Assert.Equal(true, await check.ExecuteScalarAsync());
            codesParameter.Value = JsonSerializer.Serialize(all);
            Assert.Equal(false, await check.ExecuteScalarAsync());
        }

        entries.Value = "[]";
        codesParameter.Value = JsonSerializer.Serialize(all.Reverse());
        Assert.Equal(true, await check.ExecuteScalarAsync());
        await using var update = new NpgsqlCommand("""
            UPDATE "InspectionDraftSaves" SET "ExaminedPointCodesJson" = NULL WHERE "Id" = @id
            """, schema);
        update.Parameters.AddWithValue("id", current.OperationId);
        var immutable = await Assert.ThrowsAsync<PostgresException>(() => update.ExecuteNonQueryAsync());
        Assert.Equal("CK_InspectionDraftSaves_Immutable", immutable.ConstraintName);
    }
}
