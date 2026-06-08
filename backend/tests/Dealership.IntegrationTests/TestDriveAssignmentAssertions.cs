using Dealership.Domain.Common;
using System.Text.Json;
using Dealership.Application.Accounts;
using Dealership.Application.Branches;
using Dealership.Application.Intake;
using Dealership.Application.TestDrives;
using Dealership.Domain.Intake;
using Dealership.Domain.TestDrives;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Development;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static Dealership.IntegrationTests.ReservationTestData;

namespace Dealership.IntegrationTests;

internal static class TestDriveAssignmentAssertions
{
    internal static async Task VerifyEmptyMigrationAsync(IntakePhotoFixture fixture)
    {
        await using var maintenance = new DealershipDbContext(new DbContextOptionsBuilder<DealershipDbContext>()
            .UseNpgsql(fixture.Database.ConnectionString).Options);
        var migrator = maintenance.GetService<IMigrator>();
        await migrator.MigrateAsync("20261103070027_TestDriveCustomerCancellations");
        var original = await maintenance.Database.SqlQueryRaw<string>(
            "SELECT pg_get_functiondef('public.guard_appointment_assignment()'::regprocedure) AS \"Value\"").SingleAsync();
        var originalCancellation = await maintenance.Database.SqlQueryRaw<string>(
            "SELECT pg_get_functiondef('public.guard_test_drive_cancellation()'::regprocedure) AS \"Value\"").SingleAsync();
        await migrator.MigrateAsync();
        await migrator.MigrateAsync("20261103070027_TestDriveCustomerCancellations");
        var restored = await maintenance.Database.SqlQueryRaw<string>(
            "SELECT pg_get_functiondef('public.guard_appointment_assignment()'::regprocedure) AS \"Value\"").SingleAsync();
        Assert.Equal(original, restored);
        var restoredCancellation = await maintenance.Database.SqlQueryRaw<string>(
            "SELECT pg_get_functiondef('public.guard_test_drive_cancellation()'::regprocedure) AS \"Value\"").SingleAsync();
        Assert.Equal(originalCancellation, restoredCancellation);
        await migrator.MigrateAsync();
        var role = new NpgsqlConnectionStringBuilder(fixture.Database.RuntimeConnectionString).Username!;
        Assert.Matches("^aa02_runtime_[0-9a-f]{32}$", role);
        await maintenance.Database.OpenConnectionAsync();
        await using var grant = new NpgsqlCommand(
            $"GRANT SELECT, INSERT, UPDATE ON \"TestDriveAssignments\" TO \"{role}\"",
            (NpgsqlConnection)maintenance.Database.GetDbConnection());
        await grant.ExecuteNonQueryAsync();
    }

    internal static async Task VerifyAsync(CheckInTestData data, IntakePhotoFixture fixture,
        DealershipDbContext database, TestDriveBooking booking, Guid manager, Guid technician)
    {
        using var setup = data.Factory.Services.CreateScope();
        var other = await ProvisionAsync(setup.ServiceProvider, data.BranchId, AccountRoles.SalesAgent);
        var gm = await ProvisionAsync(setup.ServiceProvider, null, AccountRoles.GeneralManager);
        var admin = await ProvisionAsync(setup.ServiceProvider, null, AccountRoles.Administrator);
        var outsideBranch = Guid.NewGuid();
        await setup.ServiceProvider.GetRequiredService<BranchProvisioningService>().ProvisionBranchAsync(
            DevelopmentFixtureManifest.Branches[0] with { Id = outsideBranch, Name = "ApexAuto Subang" }, default);
        var outside = await ProvisionAsync(setup.ServiceProvider, outsideBranch, AccountRoles.SalesAgent);
        var claim = new TestDriveAssignmentRequest(Guid.NewGuid(), 0);
        Assert.Equal(TestDriveAssignmentStatus.Invalid, (await ExecuteAsync(data.StaffId,
            WorkOwnershipKind.Claimed, claim with { ExpectedVersion = -1 })).Status);
        foreach (var denied in new[] { booking.CustomerId, technician, gm, admin })
        {
            Assert.Equal(TestDriveAssignmentStatus.Forbidden,
                (await ExecuteAsync(denied, WorkOwnershipKind.Claimed, claim)).Status);
        }

        Assert.Equal(TestDriveAssignmentStatus.Forbidden, (await ExecuteAsync(data.StaffId,
            WorkOwnershipKind.Assigned, claim with { HandlerId = other })).Status);
        Assert.Equal(TestDriveAssignmentStatus.NotFound,
            (await ExecuteAsync(outside, WorkOwnershipKind.Claimed, claim)).Status);
        Assert.Equal(TestDriveAssignmentStatus.Unavailable, (await ExecuteAsync(manager,
            WorkOwnershipKind.Assigned, claim with { HandlerId = outside })).Status);
        var race = await Task.WhenAll(ExecuteAsync(data.StaffId, WorkOwnershipKind.Claimed, claim),
            ExecuteAsync(other, WorkOwnershipKind.Claimed, claim with { OperationId = Guid.NewGuid() }));
        var winner = Assert.Single(race, result => result.Status == TestDriveAssignmentStatus.Succeeded).Receipt!;
        var loser = Assert.Single(race, result => result.Status == TestDriveAssignmentStatus.Conflict);
        Assert.Equal(winner.Id, loser.Current!.Id);
        Assert.Null(loser.Receipt);
        var oldRequest = new TestDriveAssignmentRequest(winner.Id, 0);
        Assert.Equal(TestDriveAssignmentStatus.Conflict, (await ExecuteAsync(manager,
            WorkOwnershipKind.Claimed, oldRequest)).Status);
        Assert.Equal(TestDriveAssignmentStatus.Invalid, (await ExecuteAsync(manager,
            WorkOwnershipKind.Assigned, new(Guid.NewGuid(), 1, manager))).Status);
        var assignedRequest = new TestDriveAssignmentRequest(Guid.NewGuid(), 1, manager, " Cover customer arrival ");
        var assigned = await ExecuteAsync(manager, WorkOwnershipKind.Assigned, assignedRequest);
        Assert.Equal(TestDriveAssignmentStatus.Succeeded, assigned.Status);
        Assert.Equal(winner.HandlerId, assigned.Receipt!.PreviousHandlerId);
        Assert.Equal("Cover customer arrival", assigned.Receipt.Reason);
        Assert.Equal(TestDriveAssignmentStatus.Forbidden, (await ExecuteAsync(winner.ActorId,
            WorkOwnershipKind.Released, new(Guid.NewGuid(), 2))).Status);

        using var restarted = fixture.CreateFactory();
        using var restartedScope = restarted.Services.CreateScope();
        var replay = await restartedScope.ServiceProvider.GetRequiredService<TestDriveAssignmentService>()
            .ExecuteAsync(winner.ActorId, booking.Id, WorkOwnershipKind.Claimed, oldRequest, default);
        Assert.Equal(TestDriveAssignmentStatus.Succeeded, replay.Status);
        Assert.Equal(JsonSerializer.Serialize(winner), JsonSerializer.Serialize(replay.Receipt));
        Assert.Equal(2, replay.Current!.Version);
        Assert.Equal(TestDriveAssignmentStatus.Conflict, (await ExecuteAsync(manager,
            WorkOwnershipKind.Assigned, assignedRequest with { Reason = "Changed request" })).Status);

        var immutable = await Assert.ThrowsAsync<PostgresException>(() => database.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"TestDriveAssignments\" SET \"Reason\" = \"Reason\" WHERE \"Id\" = {winner.Id}"));
        Assert.Equal("CK_TestDriveAssignments_Immutable", immutable.ConstraintName);
        var stale = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(data.StaffId, data.BranchId,
            "Claimed", data.StaffId, null, 0));
        Assert.Equal("CK_TestDriveAssignments_Chain", stale.ConstraintName);
        var wrongBranch = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(data.StaffId, Guid.NewGuid(),
            "Claimed", data.StaffId, null, 0));
        Assert.Equal("CK_TestDriveAssignments_Eligibility", wrongBranch.ConstraintName);

        // Both use the same booked interval while branch locking serializes the two command families.
        var dateOffset = DateOnly.FromDateTime(booking.StartsAt.AddHours(8).DateTime).DayNumber -
            DateOnly.FromDateTime(DateTimeOffset.UtcNow.AddHours(8).DateTime).DayNumber;
        var intake = await data.BookAsync(0, "WXX 6721", dateOffset);
        using var intakeScope = data.Factory.Services.CreateScope();
        var intakeService = intakeScope.ServiceProvider.GetRequiredService<AppointmentAssignmentService>();
        Assert.Equal(AppointmentAssignmentStatus.ScheduleConflict, (await intakeService.ExecuteAsync(manager,
            intake.Id, WorkOwnershipKind.Claimed, new(Guid.NewGuid(), 0), default)).Status);
        var sqlOverlap = await Assert.ThrowsAsync<PostgresException>(() => database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "AppointmentAssignments" ("Id", "AppointmentId", "BranchId", "ActorId", "Kind", "HandlerId",
                "ExpectedVersion", "Version", "RecordedAt")
            VALUES ({Guid.NewGuid()}, {intake.Id}, {data.BranchId}, {manager}, 'Claimed', {manager}, 0, 1, clock_timestamp())
            """));
        Assert.Equal("CK_AppointmentAssignments_Overlap", sqlOverlap.ConstraintName);
        Assert.Equal(TestDriveAssignmentStatus.Succeeded, (await ExecuteAsync(manager,
            WorkOwnershipKind.Released, new(Guid.NewGuid(), 2))).Status);
        Assert.Equal(AppointmentAssignmentStatus.Succeeded, (await intakeService.ExecuteAsync(manager,
            intake.Id, WorkOwnershipKind.Claimed, new(Guid.NewGuid(), 0), default)).Status);
        Assert.Equal(TestDriveAssignmentStatus.ScheduleConflict, (await ExecuteAsync(manager,
            WorkOwnershipKind.Claimed, new(Guid.NewGuid(), 3))).Status);
        var driveOverlap = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(manager, data.BranchId,
            "Claimed", manager, null, 3));
        Assert.Equal("CK_TestDriveAssignments_Overlap", driveOverlap.ConstraintName);
        Assert.Equal(AppointmentAssignmentStatus.Succeeded, (await intakeService.ExecuteAsync(manager,
            intake.Id, WorkOwnershipKind.Released, new(Guid.NewGuid(), 1), default)).Status);
        Assert.Equal(TestDriveAssignmentStatus.Succeeded, (await ExecuteAsync(manager,
            WorkOwnershipKind.Claimed, new(Guid.NewGuid(), 3))).Status);
        await database.Users.Where(item => item.Id == manager)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.IsActive, false));
        Assert.Equal(TestDriveAssignmentStatus.Forbidden, (await ExecuteAsync(manager,
            WorkOwnershipKind.Released, new(Guid.NewGuid(), 4))).Status);
        Assert.Equal(manager, await database.TestDriveAssignments.Where(item => item.BookingId == booking.Id)
            .OrderByDescending(item => item.Version).Select(item => item.HandlerId).FirstAsync());
        await database.Users.Where(item => item.Id == manager)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.IsActive, true));
        Assert.Equal(TestDriveAssignmentStatus.Succeeded, (await ExecuteAsync(manager,
            WorkOwnershipKind.Released, new(Guid.NewGuid(), 4))).Status);
        var intakeRace = intakeService.ExecuteAsync(manager, intake.Id, WorkOwnershipKind.Claimed,
            new(Guid.NewGuid(), 2), default);
        var driveRace = ExecuteAsync(manager, WorkOwnershipKind.Claimed, new(Guid.NewGuid(), 5));
        await Task.WhenAll(intakeRace, driveRace);
        Assert.True(intakeRace.Result.Status == AppointmentAssignmentStatus.Succeeded &&
            driveRace.Result.Status == TestDriveAssignmentStatus.ScheduleConflict ||
            intakeRace.Result.Status == AppointmentAssignmentStatus.ScheduleConflict &&
            driveRace.Result.Status == TestDriveAssignmentStatus.Succeeded);

        async Task<TestDriveAssignmentResult> ExecuteAsync(Guid actorId, string kind, TestDriveAssignmentRequest request)
        {
            using var commandScope = data.Factory.Services.CreateScope();
            return await commandScope.ServiceProvider.GetRequiredService<TestDriveAssignmentService>()
                .ExecuteAsync(actorId, booking.Id, kind, request, default);
        }

        Task<int> InsertAsync(Guid actorId, Guid branchId, string kind, Guid? handlerId, Guid? previous, long expected)
        {
            return database.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "TestDriveAssignments" ("Id", "BookingId", "BranchId", "ActorId", "Kind", "HandlerId",
                    "PreviousHandlerId", "ExpectedVersion", "Version", "RecordedAt")
                VALUES ({Guid.NewGuid()}, {booking.Id}, {branchId}, {actorId}, {kind}, {handlerId},
                    {previous}, {expected}, {expected + 1}, clock_timestamp())
                """);
        }
    }
}
