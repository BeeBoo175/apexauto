using Npgsql;

namespace Dealership.IntegrationTests;

internal static class BookingSqlAssertions
{
    public static async Task VerifyAsync(RegistrationDatabase fixture, Guid appointmentId, Guid otherOwner, Guid[] unusedPhotos)
    {
        await RejectAsync(fixture, "UPDATE \"SellerValuations\" SET \"Minimum\" = 0 WHERE \"AppointmentId\" = @id",
            appointmentId, otherOwner, PostgresErrorCodes.CheckViolation);
        await RejectAsync(fixture, "UPDATE \"IntakeAppointments\" SET \"RegistrationPlate\" = 'BPP 1234' WHERE \"Id\" = @id",
            appointmentId, otherOwner, PostgresErrorCodes.CheckViolation);
        await RejectAsync(fixture, "UPDATE \"AppointmentPhotos\" SET \"Angle\" = 'Rear' WHERE \"AppointmentId\" = @id",
            appointmentId, otherOwner, PostgresErrorCodes.CheckViolation);
        await RejectAsync(fixture, "DELETE FROM \"IntakeAppointments\" WHERE \"Id\" = @id",
            appointmentId, otherOwner, PostgresErrorCodes.InsufficientPrivilege);

        const string clone = """
            INSERT INTO "IntakeAppointments" ("Id", "OwnerId", "BranchId", "RegistrationPlate", "StartsAt", "EndsAt", "CreatedAt")
            SELECT @new, "OwnerId", "BranchId", "RegistrationPlate", "StartsAt" + interval '1 day',
                "EndsAt" + interval '1 day', "CreatedAt" FROM "IntakeAppointments" WHERE "Id" = @id
            """;
        await RejectAsync(fixture, clone, appointmentId, otherOwner, PostgresErrorCodes.CheckViolation);
        await RejectAsync(fixture, clone.Replace(" + interval '1 day'", string.Empty, StringComparison.Ordinal),
            appointmentId, otherOwner, PostgresErrorCodes.UniqueViolation);
        await RejectAsync(fixture, clone.Replace("interval '1 day'", "interval '15 minutes'", StringComparison.Ordinal),
            appointmentId, otherOwner, PostgresErrorCodes.ExclusionViolation);
        await RejectAsync(fixture, clone.Replace("\"OwnerId\", \"BranchId\", \"RegistrationPlate\", \"StartsAt\" +", "@other, @new, \"RegistrationPlate\", \"StartsAt\" +", StringComparison.Ordinal),
            appointmentId, otherOwner, PostgresErrorCodes.ForeignKeyViolation);

        const string wrongPhotoOwner = """
            INSERT INTO "AppointmentPhotos" ("AppointmentId", "OwnerId", "PhotoId", "Angle")
            SELECT @new, @other, "PhotoId", "Angle" FROM "AppointmentPhotos"
            WHERE "AppointmentId" = @id AND "Angle" = 'Driver'
            """;
        const string wrongAppointmentOwner = """
            INSERT INTO "AppointmentPhotos" ("AppointmentId", "OwnerId", "PhotoId", "Angle")
            SELECT @new, "OwnerId", @photo, "Angle" FROM "AppointmentPhotos"
            WHERE "AppointmentId" = @id AND "Angle" = 'Driver'
            """;
        const string wrongAngle = """
            INSERT INTO "AppointmentPhotos" ("AppointmentId", "OwnerId", "PhotoId", "Angle")
            SELECT @new, "OwnerId", "PhotoId", 'Rear' FROM "AppointmentPhotos"
            WHERE "AppointmentId" = @id AND "Angle" = 'Driver'
            """;
        const string wrongValuationOwner = """
            INSERT INTO "SellerValuations" ("AppointmentId", "OwnerId", "Make", "Model", "Trim", "ManufactureYear",
                "MileageKilometres", "Minimum", "Maximum", "Currency", "ValuationYear")
            SELECT @new, "OwnerId", "Make", "Model", "Trim", "ManufactureYear", "MileageKilometres",
                "Minimum", "Maximum", "Currency", "ValuationYear" FROM "SellerValuations" WHERE "AppointmentId" = @id
            """;
        var otherParent = clone.Replace("SELECT @new, \"OwnerId\"", "SELECT @new, @other", StringComparison.Ordinal);
        var attacks = new[]
        {
            (Parent: otherParent, Sql: wrongPhotoOwner, Constraint: "FK_AppointmentPhotos_IntakePhotos_PhotoId_OwnerId_Angle"),
            (Parent: otherParent, Sql: wrongAppointmentOwner, Constraint: "FK_AppointmentPhotos_IntakeAppointments_AppointmentId_OwnerId"),
            (Parent: clone, Sql: wrongAngle, Constraint: "FK_AppointmentPhotos_IntakePhotos_PhotoId_OwnerId_Angle"),
            (Parent: otherParent, Sql: wrongValuationOwner, Constraint: "FK_SellerValuations_IntakeAppointments_AppointmentId_OwnerId")
        };
        await using var connection = new NpgsqlConnection(fixture.RuntimeConnectionString);
        await connection.OpenAsync();

        foreach (var attempt in attacks)
        {
            await using var transaction = await connection.BeginTransactionAsync();
            var temporary = Guid.NewGuid();
            await using (var parent = Command(attempt.Parent, connection, transaction, appointmentId, otherOwner, temporary))
            {
                await parent.ExecuteNonQueryAsync();
            }

            await using var attack = Command(attempt.Sql, connection, transaction, appointmentId, otherOwner, temporary);
            attack.Parameters.AddWithValue("photo", unusedPhotos[0]);
            var error = await Assert.ThrowsAsync<PostgresException>(() => attack.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
            Assert.Equal(attempt.Constraint, error.ConstraintName);
        }

        const string photoCopy = """
            INSERT INTO "IntakePhotos" ("Id", "OwnerId", "Angle", "ObjectKey", "ContentType", "Bytes", "Sha256",
                "Width", "Height", "CreatedAt", "ExpiresAt", "AvailableAt")
            SELECT @photo, p."OwnerId", p."Angle", 'intake/photos/' || replace(@photo::text, '-', ''),
                p."ContentType", p."Bytes", p."Sha256", p."Width", p."Height", now() - interval '2 days', {0}, {1}
            FROM "IntakePhotos" p JOIN "AppointmentPhotos" a ON a."PhotoId" = p."Id"
            WHERE a."AppointmentId" = @id AND a."Angle" = 'Driver'
            """;
        var unavailable = new[]
        {
            (Expires: "now() + interval '1 day'", Available: "NULL"),
            (Expires: "now() - interval '1 day'", Available: "now() - interval '2 days'"),
            (Expires: "now() + interval '1 day'", Available: "now() + interval '1 hour'")
        };

        foreach (var candidate in unavailable)
        {
            await using var transaction = await connection.BeginTransactionAsync();
            var temporary = Guid.NewGuid();
            await using (var parent = Command(clone, connection, transaction, appointmentId, otherOwner, temporary))
            {
                await parent.ExecuteNonQueryAsync();
            }

            var sql = string.Format(System.Globalization.CultureInfo.InvariantCulture, photoCopy, candidate.Expires, candidate.Available);
            await using var photo = Command(sql, connection, transaction, appointmentId, otherOwner, temporary);
            photo.Parameters.AddWithValue("photo", Guid.NewGuid());
            await photo.ExecuteNonQueryAsync();
            await using var attach = Command("""
                INSERT INTO "AppointmentPhotos" ("AppointmentId", "OwnerId", "PhotoId", "Angle")
                SELECT @new, "OwnerId", "Id", "Angle" FROM "IntakePhotos" WHERE "Id" = @photo
                """, connection, transaction, appointmentId, otherOwner, temporary);
            attach.Parameters.AddWithValue("photo", photo.Parameters["photo"].Value!);
            var error = await Assert.ThrowsAsync<PostgresException>(() => attach.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
            Assert.Equal("CK_AppointmentPhotos_Available", error.ConstraintName);
        }

        await VerifyIncompleteBundlesAsync(connection, clone, appointmentId, otherOwner, unusedPhotos);
    }

    private static async Task VerifyIncompleteBundlesAsync(NpgsqlConnection connection, string parentSql,
        Guid id, Guid other, Guid[] unusedPhotos)
    {
        const string valuationSql = """
            INSERT INTO "SellerValuations" ("AppointmentId", "OwnerId", "Make", "Model", "Trim", "ManufactureYear",
                "MileageKilometres", "Minimum", "Maximum", "Currency", "ValuationYear")
            SELECT @new, "OwnerId", "Make", "Model", "Trim", "ManufactureYear", "MileageKilometres",
                "Minimum", "Maximum", "Currency", "ValuationYear" FROM "SellerValuations" WHERE "AppointmentId" = @id
            """;
        const string photosSql = """
            INSERT INTO "AppointmentPhotos" ("AppointmentId", "OwnerId", "PhotoId", "Angle")
            SELECT @new, "OwnerId", "Id", "Angle" FROM "IntakePhotos" WHERE "Id" = ANY (@photos)
            """;
        const string mailSql = """
            INSERT INTO "AppointmentConfirmations" ("Id", "RecipientEmail", "DeliveryAttempts", "NextDeliveryAt")
            SELECT @new, "RecipientEmail", 0, now() FROM "AppointmentConfirmations" WHERE "Id" = @id
            """;
        var cases = new[]
        {
            (Photos: 3, Valuation: true, Mail: true),
            (Photos: 4, Valuation: false, Mail: true),
            (Photos: 4, Valuation: true, Mail: false)
        };

        foreach (var candidate in cases)
        {
            await using var transaction = await connection.BeginTransactionAsync();
            var temporary = Guid.NewGuid();
            await using var parent = Command(parentSql, connection, transaction, id, other, temporary);
            await parent.ExecuteNonQueryAsync();

            if (candidate.Valuation)
            {
                await using var valuation = Command(valuationSql, connection, transaction, id, other, temporary);
                await valuation.ExecuteNonQueryAsync();
            }

            await using var photos = Command(photosSql, connection, transaction, id, other, temporary);
            photos.Parameters.AddWithValue("photos", unusedPhotos.Take(candidate.Photos).ToArray());
            await photos.ExecuteNonQueryAsync();

            if (candidate.Mail)
            {
                await using var mail = Command(mailSql, connection, transaction, id, other, temporary);
                await mail.ExecuteNonQueryAsync();
            }

            var error = await Assert.ThrowsAsync<PostgresException>(() => transaction.CommitAsync());
            Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
            Assert.Equal("CK_IntakeAppointments_Complete", error.ConstraintName);
        }
    }

    private static async Task RejectAsync(RegistrationDatabase fixture, string sql, Guid id, Guid other, string expected)
    {
        await using var connection = new NpgsqlConnection(fixture.RuntimeConnectionString);
        await connection.OpenAsync();
        await using var command = Command(sql, connection, null, id, other, Guid.NewGuid());
        var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(expected, error.SqlState);
    }

    private static NpgsqlCommand Command(string sql, NpgsqlConnection connection, NpgsqlTransaction? transaction,
        Guid id, Guid other, Guid temporary)
    {
        var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("other", other);
        command.Parameters.AddWithValue("new", temporary);
        return command;
    }
}
