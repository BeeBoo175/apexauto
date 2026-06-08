using Dealership.Domain.Common;
using Dealership.Infrastructure.Intake;
using Dealership.Application.Accounts;
using Dealership.Application.Intake;
using Dealership.Application.Branches;
using Dealership.Domain.Intake;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Dealership.IntegrationTests;

public sealed class IntakeCheckInSchemaTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    private const string InsertVehicle = """
        INSERT INTO "Vehicles" ("Id", "IntakeAppointmentId", "IntakeOwnerId", "BranchId", "Vin", "RegistrationPlate",
            "RegistrationIdentity", "Make", "Model", "Trim", "ManufactureYear", "MileageKilometres", "Status", "CreatedAt")
        SELECT @vehicle, a."Id", a."OwnerId", a."BranchId", @vin, a."RegistrationPlate", @registration,
            a."ValuationMake", a."ValuationModel", a."ValuationTrim", a."ValuationManufactureYear", 92000, 'PendingInspection', @at
        FROM "IntakeAppointments" a
        WHERE a."Id" = @appointment;
        """;

    private const string InsertCheckIn = """
        INSERT INTO "AppointmentCheckIns" ("AppointmentId", "OwnerId", "BranchId", "VehicleId", "StaffId", "CheckedInAt",
            "Vin", "MileageKilometres", "CustomerPresent", "IdentityConfirmed", "OwnershipConfirmed", "VehicleConfirmed", "Notes")
        VALUES (@appointment, @owner, @branch, @vehicle, @staff, @at, @vin, 92000, true, true, true, true, NULL);
        """;

    private const string InsertMail = """
        INSERT INTO "AppointmentMailOutbox" ("Id", "AppointmentId", "Kind", "RecipientEmail", "DeliveryAttempts", "NextDeliveryAt")
        VALUES (gen_random_uuid(), @appointment, 'CheckIn', @email, 0, @at);
        """;

    [Fact]
    public async Task RestrictedSqlRequiresScopedEligibleImmutableCheckInAndOneMatchingDurableBundle()
    {
        await VerifyHandlerMigrationAsync();
        using var data = await CheckInTestData.CreateAsync(fixture);
        var appointment = await data.BookAsync();
        await RejectAsync(data, appointment.Id, InsertCheckIn + InsertMail,
            "CK_AppointmentCheckIns_Handler", "unclaimed appointment");
        await RejectAsync(data, appointment.Id,
            "UPDATE \"AspNetUsers\" SET \"Role\" = 'BranchManager' WHERE \"Id\" = @staff;" + InsertCheckIn + InsertMail,
            "CK_AppointmentCheckIns_Handler", "unclaimed manager");
        await data.ClaimAsync(appointment.Id);
        var managerId = Guid.NewGuid();
        using (var branchScope = data.Factory.Services.CreateScope())
        {
            await branchScope.ServiceProvider.GetRequiredService<BranchProvisioningService>().ProvisionBranchAsync(
                DevelopmentFixtureManifest.Branches[0] with { Id = Guid.NewGuid(), Name = "ApexAuto Petaling Jaya" }, CancellationToken.None);
            await branchScope.ServiceProvider.GetRequiredService<StaffProvisioningService>().ProvisionStaffAsync(
                new(managerId, $"farid.{managerId:N}@example.invalid", "Password123!", AccountRoles.BranchManager, data.BranchId),
                CancellationToken.None);
        }

        var attacks = new[]
        {
            ("vehicle without check-in", "", "CK_AppointmentCheckIns_Complete"),
            ("check-in without mail", InsertCheckIn, "CK_AppointmentCheckIns_Complete"),
            ("wrong mail recipient", InsertCheckIn + InsertMail.Replace("@email", "'other@example.invalid'", StringComparison.Ordinal), "CK_AppointmentCheckIns_Complete"),
            ("mismatched vehicle mileage", InsertCheckIn.Replace("92000", "91999", StringComparison.Ordinal) + InsertMail, "CK_AppointmentCheckIns_Complete"),
            ("wrong owner", InsertCheckIn.Replace("@owner", "@other", StringComparison.Ordinal), "foreign-key"),
            ("wrong branch", InsertCheckIn.Replace("@branch", "@other", StringComparison.Ordinal), "foreign-key"),
            ("wrong vehicle", InsertCheckIn.Replace("@vehicle", "@other", StringComparison.Ordinal), "foreign-key"),
            ("missing physical verification", InsertCheckIn.Replace("true, true, true, true", "true, false, true, true", StringComparison.Ordinal), "CK_AppointmentCheckIns_Physical"),
            ("customer absent", InsertCheckIn.Replace("true, true, true, true", "false, true, true, true", StringComparison.Ordinal), "CK_AppointmentCheckIns_Physical"),
            ("ownership unconfirmed", InsertCheckIn.Replace("true, true, true, true", "true, true, false, true", StringComparison.Ordinal), "CK_AppointmentCheckIns_Physical"),
            ("vehicle unconfirmed", InsertCheckIn.Replace("true, true, true, true", "true, true, true, false", StringComparison.Ordinal), "CK_AppointmentCheckIns_Physical"),
            ("different date", InsertCheckIn.Replace("@at", "@at - interval '1 day'", StringComparison.Ordinal), "CK_AppointmentCheckIns_Time"),
            ("unverified owner", "UPDATE \"AspNetUsers\" SET \"EmailConfirmed\" = false WHERE \"Id\" = @owner;" + InsertCheckIn, "CK_AppointmentCheckIns_Eligibility"),
            ("inactive owner", "UPDATE \"AspNetUsers\" SET \"IsActive\" = false WHERE \"Id\" = @owner;" + InsertCheckIn, "CK_AppointmentCheckIns_Eligibility"),
            ("inactive staff", "UPDATE \"AspNetUsers\" SET \"IsActive\" = false WHERE \"Id\" = @staff;" + InsertCheckIn, "CK_AppointmentCheckIns_Eligibility"),
            ("reassigned staff", "UPDATE \"AspNetUsers\" SET \"BranchId\" = (SELECT \"Id\" FROM \"Branches\" WHERE \"Id\" <> @branch LIMIT 1) WHERE \"Id\" = @staff;" + InsertCheckIn, "CK_AppointmentCheckIns_Eligibility"),
            ("group reader cannot check in", "UPDATE \"AspNetUsers\" SET \"Role\" = 'GeneralManager', \"BranchId\" = NULL WHERE \"Id\" = @staff;" + InsertCheckIn, "CK_AppointmentCheckIns_Eligibility")
        };

        foreach (var (name, sql, constraint) in attacks)
        {
            await RejectAsync(data, appointment.Id, sql, constraint, name);
        }

        await RejectAsync(data, appointment.Id,
            InsertVehicle.Replace("a.\"ValuationModel\"", "'Different model'", StringComparison.Ordinal),
            "CK_Vehicles_Provenance", "vehicle differs from booked valuation", insertVehicle: false);

        using var scope = data.Factory.Services.CreateScope();
        var assignments = scope.ServiceProvider.GetRequiredService<AppointmentAssignmentService>();
        Assert.Equal(AppointmentAssignmentStatus.Succeeded, (await assignments.ExecuteAsync(managerId, appointment.Id,
            WorkOwnershipKind.Assigned, new(Guid.NewGuid(), 1, managerId, "Manager takes over arrival"), CancellationToken.None)).Status);
        await RejectAsync(data, appointment.Id, InsertCheckIn + InsertMail,
            "CK_AppointmentCheckIns_Handler", "former handler after reassignment");
        Assert.Equal(AppointmentAssignmentStatus.Succeeded, (await assignments.ExecuteAsync(managerId, appointment.Id,
            WorkOwnershipKind.Released, new(Guid.NewGuid(), 2), CancellationToken.None)).Status);
        await RejectAsync(data, appointment.Id, InsertCheckIn + InsertMail,
            "CK_AppointmentCheckIns_Handler", "released assignment retains earlier claim history");
        Assert.Equal(AppointmentAssignmentStatus.Succeeded, (await assignments.ExecuteAsync(data.StaffId, appointment.Id,
            WorkOwnershipKind.Claimed, new(Guid.NewGuid(), 3), CancellationToken.None)).Status);
        var result = await scope.ServiceProvider.GetRequiredService<AppointmentCheckInService>()
            .CheckInAsync(data.StaffId, appointment.Id, CheckInTestData.Evidence, CancellationToken.None);
        Assert.Equal(CheckInStatus.Succeeded, result.Status);
        var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var receipt = await database.AppointmentCheckIns.SingleAsync();
        Assert.Single(await database.Vehicles.ToArrayAsync());
        Assert.Single(await database.AppointmentCheckIns.ToArrayAsync());
        Assert.Single(await database.AppointmentMailOutbox.Where(item => item.Kind == AppointmentMailKind.CheckIn).ToArrayAsync());
        Assert.Equal(92000, receipt.MileageKilometres);
        Assert.Equal(90000, (await database.IntakeAppointments.SingleAsync()).Valuation.MileageKilometres);
        Assert.Equal(CancellationStatus.CheckedIn,
            (await scope.ServiceProvider.GetRequiredService<AppointmentCancellationService>()
                .CancelAsync(data.OwnerId, appointment.Id, CancellationToken.None)).Status);

        var retainedAttacks = new[]
        {
            ("UPDATE \"AppointmentCheckIns\" SET \"MileageKilometres\" = 1 WHERE \"AppointmentId\" = @appointment", "CK_IntakeAppointments_Immutable"),
            ("UPDATE \"Vehicles\" SET \"Vin\" = 'PL1BT65R84B123457' WHERE \"IntakeAppointmentId\" = @appointment", "CK_Vehicles_Immutable"),
            ("UPDATE \"AppointmentMailOutbox\" SET \"RecipientEmail\" = 'other@example.invalid' WHERE \"AppointmentId\" = @appointment", "CK_AppointmentMailOutbox_Immutable"),
            ("UPDATE \"IntakeAppointments\" SET \"CancelledAt\" = @at, \"CancellationPhotosExpireAt\" = @at + interval '30 days' WHERE \"Id\" = @appointment", "CK_IntakeAppointments_CheckedIn"),
            ("INSERT INTO \"PhotoRemovals\" (\"PhotoId\", \"Reason\", \"EligibleAt\", \"RequestedAt\", \"Attempts\", \"NextAttemptAt\") SELECT \"PhotoId\", 'CancelledRetentionExpired', @at, @at, 0, @at FROM \"AppointmentPhotos\" WHERE \"AppointmentId\" = @appointment LIMIT 1", "CK_PhotoRemovals_CheckedIn")
        };

        foreach (var (sql, constraint) in retainedAttacks)
        {
            await RejectAsync(data, appointment.Id, sql, constraint, constraint, insertVehicle: false);
        }

        await using var maintenance = new NpgsqlConnection(fixture.Database.ConnectionString);
        await maintenance.OpenAsync();
        await using var deleteActor = new NpgsqlCommand("DELETE FROM \"AspNetUsers\" WHERE \"Id\" = @staff", maintenance);
        deleteActor.Parameters.AddWithValue("staff", data.StaffId);
        var error = await Assert.ThrowsAsync<PostgresException>(() => deleteActor.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
        var sameVin = await data.BookAsync(slot: 1, plate: "BPP 1234");
        await RejectAsync(data, sameVin.Id, "", "IX_Vehicles_Vin", "duplicate VIN", registration: "BPP1234");
        var sameRegistration = await data.BookAsync(slot: 2, plate: "WYY8821");
        await RejectAsync(data, sameRegistration.Id, "", "IX_Vehicles_RegistrationIdentity", "duplicate registration",
            vin: "PL1BT65R84B123457");
    }

    private async Task VerifyHandlerMigrationAsync()
    {
        var options = new DbContextOptionsBuilder<DealershipDbContext>().UseNpgsql(fixture.Database.ConnectionString).Options;
        await using var database = new DealershipDbContext(options);
        const string definition = "SELECT pg_get_functiondef('public.validate_appointment_checkin()'::regprocedure) AS \"Value\"";
        var original = await database.Database.SqlQueryRaw<string>(definition).SingleAsync();
        var migration = new RequireCheckInHandler();
        var down = Assert.IsType<SqlOperation>(Assert.Single(migration.DownOperations));
        var up = Assert.IsType<SqlOperation>(Assert.Single(migration.UpOperations));
        await database.Database.ExecuteSqlRawAsync(down.Sql);
        var previous = await database.Database.SqlQueryRaw<string>(definition).SingleAsync();
        Assert.DoesNotContain("CK_AppointmentCheckIns_Handler", previous, StringComparison.Ordinal);
        await database.Database.ExecuteSqlRawAsync(up.Sql);
        Assert.Equal(original, await database.Database.SqlQueryRaw<string>(definition).SingleAsync());
    }

    private async Task RejectAsync(CheckInTestData data, Guid appointmentId, string sql, string constraint,
        string scenario, bool insertVehicle = true, string vin = "PL1BT65R84B123456", string registration = "WYY8821")
    {
        await using var connection = new NpgsqlConnection(fixture.Database.RuntimeConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var command = new NpgsqlCommand((insertVehicle ? InsertVehicle : "") + sql + "; SET CONSTRAINTS ALL IMMEDIATE;",
            connection, transaction);
        command.Parameters.AddWithValue("appointment", appointmentId);
        command.Parameters.AddWithValue("vehicle", Guid.NewGuid());
        command.Parameters.AddWithValue("other", Guid.NewGuid());
        command.Parameters.AddWithValue("owner", data.OwnerId);
        command.Parameters.AddWithValue("branch", data.BranchId);
        command.Parameters.AddWithValue("staff", data.StaffId);
        command.Parameters.AddWithValue("email", data.CustomerEmail);
        command.Parameters.AddWithValue("vin", vin);
        command.Parameters.AddWithValue("registration", registration);
        command.Parameters.AddWithValue("at", DateTimeOffset.UtcNow);
        var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());

        if (constraint == "foreign-key")
        {
            Assert.True(error.SqlState == PostgresErrorCodes.ForeignKeyViolation, $"{scenario}: {error.SqlState}/{error.ConstraintName}");
            return;
        }

        Assert.True(error.ConstraintName == constraint, $"{scenario}: {error.SqlState}/{error.ConstraintName}");
    }
}
