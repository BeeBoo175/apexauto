using System.Text.Json;
using Dealership.Application.Inspections;
using Npgsql;
using NpgsqlTypes;

namespace Dealership.IntegrationTests;

internal static class InspectionPhotoAttachmentSqlAssertions
{
    public static async Task VerifyAsync(RegistrationDatabase fixture, InspectionDraftSaveReceipt saved, Guid extraPhoto)
    {
        const string incomplete = """
            INSERT INTO "InspectionDraftSaves" ("Id", "InspectionId", "BranchId", "ActorId", "AssignmentVersion",
                "ExpectedVersion", "Version", "CatalogVersion", "EntriesJson", "PhotoIdsJson", "RecordedAt")
            SELECT @id, "InspectionId", "BranchId", "ActorId", 1, 2, 3, "CatalogVersion", "EntriesJson", @photos,
                clock_timestamp() FROM "InspectionDraftSaves" WHERE "Id" = @save;
            SET CONSTRAINTS ALL IMMEDIATE;
            """;
        foreach (var photos in new[] { JsonSerializer.Serialize(new[] { extraPhoto }), "null", "[null]", "[\"invalid\"]" })
        {
            await using var connection = new NpgsqlConnection(fixture.RuntimeConnectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            await using var command = new NpgsqlCommand(incomplete, connection, transaction);
            command.Parameters.AddWithValue("id", Guid.NewGuid());
            command.Parameters.AddWithValue("save", saved.OperationId);
            command.Parameters.AddWithValue("photos", NpgsqlDbType.Jsonb, photos);
            Assert.Equal("CK_InspectionDraftPhotos_Complete",
                (await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync())).ConstraintName);
        }

        await using (var connection = new NpgsqlConnection(fixture.RuntimeConnectionString))
        {
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            var id = Guid.NewGuid();
            await using var command = new NpgsqlCommand(incomplete.Replace("SET CONSTRAINTS ALL IMMEDIATE;", "", StringComparison.Ordinal) + """
                INSERT INTO "InspectionDraftPhotos" ("SaveId", "PhotoId", "InspectionId", "PointCode")
                SELECT @id, "Id", "InspectionId", "PointCode" FROM "InspectionPhotos"
                WHERE "Id" = ANY(@ids) ORDER BY "Id";
                """, connection, transaction);
            var ids = saved.PhotoIds.Append(extraPhoto).ToArray();
            command.Parameters.AddWithValue("id", id);
            command.Parameters.AddWithValue("save", saved.OperationId);
            command.Parameters.AddWithValue("photos", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(ids));
            command.Parameters.AddWithValue("ids", ids);
            Assert.Equal("CK_InspectionDraftPhotos_Finding",
                (await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync())).ConstraintName);
        }

        await using (var connection = new NpgsqlConnection(fixture.RuntimeConnectionString))
        {
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            await using var command = new NpgsqlCommand(incomplete.Replace("SET CONSTRAINTS ALL IMMEDIATE;", "", StringComparison.Ordinal) + """
                INSERT INTO "InspectionDraftPhotos" ("SaveId", "PhotoId", "InspectionId", "PointCode")
                VALUES (@id, @extra, @wrongInspection, 'EP-004');
                """, connection, transaction);
            command.Parameters.AddWithValue("id", Guid.NewGuid());
            command.Parameters.AddWithValue("save", saved.OperationId);
            command.Parameters.AddWithValue("photos", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(new[] { extraPhoto }));
            command.Parameters.AddWithValue("extra", extraPhoto);
            command.Parameters.AddWithValue("wrongInspection", Guid.NewGuid());
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation,
                (await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync())).SqlState);
        }

        var attacks = new[]
        {
            ("UPDATE \"InspectionDraftPhotos\" SET \"PointCode\" = 'EP-005' WHERE \"SaveId\" = @save;", "CK_InspectionDraftPhotos_Immutable"),
            ("UPDATE \"InspectionDraftSaves\" SET \"PhotoIdsJson\" = '[]' WHERE \"Id\" = @save;", "CK_InspectionDraftSaves_Immutable"),
            ("""
                INSERT INTO "InspectionDraftPhotos" ("SaveId", "PhotoId", "InspectionId", "PointCode")
                VALUES (@save, @extra, @inspection, 'EP-004');
                """, "CK_InspectionDraftPhotos_Finding")
        };
        foreach (var (sql, constraint) in attacks)
        {
            await using var connection = new NpgsqlConnection(fixture.RuntimeConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("save", saved.OperationId);
            command.Parameters.AddWithValue("extra", extraPhoto);
            command.Parameters.AddWithValue("inspection", saved.InspectionId);
            Assert.Equal(constraint, (await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync())).ConstraintName);
        }

        await using var maintenance = new NpgsqlConnection(fixture.ConnectionString);
        await maintenance.OpenAsync();
        await using var delete = new NpgsqlCommand("DELETE FROM \"InspectionDraftPhotos\" WHERE \"SaveId\" = @save", maintenance);
        delete.Parameters.AddWithValue("save", saved.OperationId);
        Assert.Equal("CK_InspectionDraftPhotos_Immutable",
            (await Assert.ThrowsAsync<PostgresException>(() => delete.ExecuteNonQueryAsync())).ConstraintName);
    }

    public static async Task VerifyRemovalDenialsAsync(RegistrationDatabase fixture, Guid retained, Guid expired)
    {
        foreach (var id in new[] { retained, expired })
        {
            await using var connection = new NpgsqlConnection(fixture.RuntimeConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("""
                INSERT INTO "InspectionPhotoRemovals" ("PhotoId", "EligibleAt", "RequestedAt", "Attempts", "NextAttemptAt")
                SELECT "Id", "ExpiresAt" - @offset, clock_timestamp(), 0, clock_timestamp()
                FROM "InspectionPhotos" WHERE "Id" = @id;
                """, connection);
            command.Parameters.AddWithValue("id", id);
            command.Parameters.AddWithValue("offset", id == retained ? TimeSpan.Zero : TimeSpan.FromSeconds(1));
            Assert.Equal("CK_InspectionPhotoRemovals_Eligibility",
                (await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync())).ConstraintName);
        }
    }

    public static async Task VerifyStaleRemovalAsync(RegistrationDatabase fixture, Guid retained, Func<Task> attachAndExpire)
    {
        await using var connection = new NpgsqlConnection(fixture.RuntimeConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
        await using var snapshot = new NpgsqlCommand("SELECT count(*) FROM \"InspectionPhotos\"", connection, transaction);
        await snapshot.ExecuteScalarAsync();
        await attachAndExpire();
        await using var removal = new NpgsqlCommand("""
            INSERT INTO "InspectionPhotoRemovals" ("PhotoId", "EligibleAt", "RequestedAt", "Attempts", "NextAttemptAt")
            SELECT "Id", "ExpiresAt", clock_timestamp(), 0, clock_timestamp() FROM "InspectionPhotos" WHERE "Id" = @id;
            """, connection, transaction);
        removal.Parameters.AddWithValue("id", retained);
        Assert.Equal(PostgresErrorCodes.SerializationFailure,
            (await Assert.ThrowsAsync<PostgresException>(() => removal.ExecuteNonQueryAsync())).SqlState);
    }

    public static async Task VerifyRemovalHistoryAsync(RegistrationDatabase fixture, Guid id)
    {
        foreach (var sql in new[]
        {
            "UPDATE \"InspectionPhotoRemovals\" SET \"EligibleAt\" = \"EligibleAt\" - interval '1 second' WHERE \"PhotoId\" = @id",
            "UPDATE \"InspectionPhotoRemovals\" SET \"DeletedAt\" = NULL WHERE \"PhotoId\" = @id"
        })
        {
            await using var connection = new NpgsqlConnection(fixture.RuntimeConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("id", id);
            Assert.Equal("CK_InspectionPhotoRemovals_Immutable",
                (await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync())).ConstraintName);
        }
        await using var maintenance = new NpgsqlConnection(fixture.ConnectionString);
        await maintenance.OpenAsync();
        await using var delete = new NpgsqlCommand("DELETE FROM \"InspectionPhotoRemovals\" WHERE \"PhotoId\" = @id", maintenance);
        delete.Parameters.AddWithValue("id", id);
        Assert.Equal("CK_InspectionPhotoRemovals_Immutable",
            (await Assert.ThrowsAsync<PostgresException>(() => delete.ExecuteNonQueryAsync())).ConstraintName);
    }

    public static async Task VerifyStaleAttachmentAsync(RegistrationDatabase fixture, Guid inspection, Guid photo,
        Func<Task> claimCleanup)
    {
        await using var connection = new NpgsqlConnection(fixture.RuntimeConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
        var id = Guid.NewGuid();
        await using var save = new NpgsqlCommand("""
            INSERT INTO "InspectionDraftSaves" ("Id", "InspectionId", "BranchId", "ActorId", "AssignmentVersion",
                "ExpectedVersion", "Version", "CatalogVersion", "EntriesJson", "PhotoIdsJson", "RecordedAt")
            SELECT @id, "InspectionId", "BranchId", "ActorId", "AssignmentVersion", "Version", "Version" + 1,
                "CatalogVersion", "EntriesJson", @photos, clock_timestamp()
            FROM "InspectionDraftSaves" WHERE "InspectionId" = @inspection ORDER BY "Version" DESC LIMIT 1;
            """, connection, transaction);
        save.Parameters.AddWithValue("id", id);
        save.Parameters.AddWithValue("inspection", inspection);
        save.Parameters.AddWithValue("photos", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(new[] { photo }));
        await save.ExecuteNonQueryAsync();
        await claimCleanup();
        await using var attach = new NpgsqlCommand("""
            INSERT INTO "InspectionDraftPhotos" ("SaveId", "PhotoId", "InspectionId", "PointCode")
            VALUES (@id, @photo, @inspection, 'EP-004');
            """, connection, transaction);
        attach.Parameters.AddWithValue("id", id);
        attach.Parameters.AddWithValue("photo", photo);
        attach.Parameters.AddWithValue("inspection", inspection);
        Assert.Equal(PostgresErrorCodes.SerializationFailure,
            (await Assert.ThrowsAsync<PostgresException>(() => attach.ExecuteNonQueryAsync())).SqlState);
    }
}
