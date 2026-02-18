using Npgsql;

namespace Dealership.IntegrationTests;

internal static class InspectionPhotoSqlAssertions
{
    public static async Task VerifyAsync(RegistrationDatabase fixture, Guid photoId, Guid technician,
        Guid otherTechnician, Guid manager)
    {
        const string insert = """
            INSERT INTO "InspectionPhotos" ("Id", "InspectionId", "BranchId", "ActorId", "AssignmentVersion",
                "PointCode", "ObjectKey", "ContentType", "Bytes", "Sha256", "Width", "Height",
                "CreatedAt", "ExpiresAt", "AvailableAt")
            SELECT @id, "InspectionId", "BranchId", @actor, 1, 'EP-004',
                'inspection/photos/' || replace(@id::text, '-', ''), "ContentType", 100, "Sha256", "Width", "Height",
                clock_timestamp(), clock_timestamp() + interval '1 day', NULL
            FROM "InspectionPhotos" WHERE "Id" = @existing;
            """;
        var attacks = new[]
        {
            (insert.Replace("@actor, 1,", "@actor, 2,", StringComparison.Ordinal), technician, "CK_InspectionPhotos_Assignment"),
            (insert, otherTechnician, "CK_InspectionPhotos_Assignment"),
            (insert, manager, "CK_InspectionPhotos_Eligibility"),
            (insert.Replace("'EP-004',", "'EP-046',", StringComparison.Ordinal), technician, "CK_InspectionPhotos_Point"),
            (insert.Replace("'inspection/photos/'", "'intake/photos/'", StringComparison.Ordinal), technician, "CK_InspectionPhotos_Identity"),
            (insert.Replace("\"ContentType\", 100,", "\"ContentType\", 10485761,", StringComparison.Ordinal), technician, "CK_InspectionPhotos_Content"),
            (insert.Replace("interval '1 day', NULL", "interval '1 day', clock_timestamp()", StringComparison.Ordinal), technician, "CK_InspectionPhotos_Pending"),
            (insert.Replace("clock_timestamp(), clock_timestamp()", "clock_timestamp() + interval '1 hour', clock_timestamp()", StringComparison.Ordinal), technician, "CK_InspectionPhotos_Current"),
            ("UPDATE \"InspectionPhotos\" SET \"Bytes\" = \"Bytes\" + 1 WHERE \"Id\" = @existing;", technician, "CK_InspectionPhotos_Immutable"),
            ("UPDATE \"InspectionPhotos\" SET \"AvailableAt\" = NULL WHERE \"Id\" = @existing;", technician, "CK_InspectionPhotos_Immutable"),
            ("UPDATE \"InspectionPhotos\" SET \"AvailableAt\" = \"AvailableAt\" + interval '1 second' WHERE \"Id\" = @existing;", technician, "CK_InspectionPhotos_Immutable"),
            ("UPDATE \"AspNetUsers\" SET \"IsActive\" = false WHERE \"Id\" = @actor;" + insert, technician, "CK_InspectionPhotos_Eligibility"),
            ("UPDATE \"Branches\" SET \"IsActive\" = false WHERE \"Id\" = (SELECT \"BranchId\" FROM \"InspectionPhotos\" WHERE \"Id\" = @existing);" + insert, technician, "CK_InspectionPhotos_Eligibility")
        };
        foreach (var (sql, actor, constraint) in attacks)
        {
            await using var connection = new NpgsqlConnection(fixture.RuntimeConnectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("id", Guid.NewGuid());
            command.Parameters.AddWithValue("existing", photoId);
            command.Parameters.AddWithValue("actor", actor);
            var failure = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
            Assert.Equal(constraint, failure.ConstraintName);
        }

        await AssertDeleteAsync(fixture.RuntimeConnectionString, photoId, PostgresErrorCodes.InsufficientPrivilege);
        await AssertDeleteAsync(fixture.ConnectionString, photoId, PostgresErrorCodes.CheckViolation);
        await using var schema = new NpgsqlConnection(fixture.RuntimeConnectionString);
        await schema.OpenAsync();
        await using var keys = new NpgsqlCommand("""
            SELECT count(*) FROM pg_constraint WHERE contype = 'f' AND confdeltype = 'r'
                AND conrelid = '"InspectionPhotos"'::regclass;
            """, schema);
        Assert.Equal(2L, await keys.ExecuteScalarAsync());
    }

    public static async Task VerifyStaleConfirmationAsync(RegistrationDatabase fixture, Guid photoId)
    {
        await using var connection = new NpgsqlConnection(fixture.RuntimeConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "UPDATE \"InspectionPhotos\" SET \"AvailableAt\" = clock_timestamp() WHERE \"Id\" = @id", connection);
        command.Parameters.AddWithValue("id", photoId);
        var failure = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal("CK_InspectionPhotos_Assignment", failure.ConstraintName);
    }

    private static async Task AssertDeleteAsync(string connectionString, Guid photoId, string expectedState)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("DELETE FROM \"InspectionPhotos\" WHERE \"Id\" = @id", connection);
        command.Parameters.AddWithValue("id", photoId);
        Assert.Equal(expectedState, (await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync())).SqlState);
    }
}
