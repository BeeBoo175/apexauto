using System.Text.Json;
using Dealership.Application.Accounts;
using Dealership.Application.TestDrives;
using Dealership.Domain.TestDrives;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.TestDrives;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static Dealership.IntegrationTests.ReservationTestData;

namespace Dealership.IntegrationTests;

internal static class TestDriveStaffCancellationAssertions
{
    internal static async Task VerifyAsync(WebApplicationFactory<Program> factory, IntakePhotoFixture fixture,
        DealershipDbContext database, TestDriveBooking booking, Guid manager)
    {
        await VerifyCustomerMigrationAsync(fixture, database);
        using var setup = factory.Services.CreateScope();
        var agent = await ProvisionAsync(setup.ServiceProvider, booking.BranchId, AccountRoles.SalesAgent);
        var nextAgent = await ProvisionAsync(setup.ServiceProvider, booking.BranchId, AccountRoles.SalesAgent);
        var outsideBranch = await database.Branches.Where(item => item.Id != booking.BranchId)
            .Select(item => item.Id).FirstAsync();
        var outside = await ProvisionAsync(setup.ServiceProvider, outsideBranch, AccountRoles.BranchManager);
        Assert.Equal(TestDriveCancellationStatus.NotFound,
            (await CancelAsync(outside, Guid.NewGuid(), "Customer cannot attend")).Status);
        var outsideSql = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(outside, "Customer cannot attend"));
        Assert.Equal("CK_TestDriveCancellations_Staff", outsideSql.ConstraintName);
        foreach (var role in new[] { AccountRoles.WorkshopTechnician, AccountRoles.GeneralManager, AccountRoles.Administrator })
        {
            var branch = role == AccountRoles.WorkshopTechnician ? (Guid?)booking.BranchId : null;
            var denied = await ProvisionAsync(setup.ServiceProvider, branch, role);
            Assert.Equal(TestDriveCancellationStatus.Forbidden,
                (await CancelAsync(denied, Guid.NewGuid(), "Customer cannot attend")).Status);
        }

        Assert.Equal(TestDriveCancellationStatus.Forbidden,
            (await CancelAsync(booking.CustomerId, Guid.NewGuid(), "Customer cannot attend")).Status);
        Assert.Equal(TestDriveCancellationStatus.Forbidden,
            (await CancelAsync(agent, Guid.NewGuid(), "Customer cannot attend")).Status);
        Assert.Equal(TestDriveCancellationStatus.Invalid, (await CancelAsync(manager, Guid.NewGuid(), " ")).Status);
        Assert.Equal(TestDriveCancellationStatus.Invalid, (await CancelAsync(manager, Guid.NewGuid(), "Changed\nreason")).Status);
        var deniedSql = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(agent, "Customer cannot attend"));
        Assert.Equal("CK_TestDriveCancellations_Staff", deniedSql.ConstraintName);
        var missingMail = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(manager, "Customer cannot attend"));
        Assert.Equal("CK_TestDriveCancellations_Mail", missingMail.ConstraintName);
        var invalidReason = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(manager, " "));
        Assert.Equal("CK_TestDriveCancellations_Actor", invalidReason.ConstraintName);

        await database.Branches.Where(item => item.Id == booking.BranchId)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.IsActive, false));
        Assert.Equal(TestDriveCancellationStatus.Forbidden,
            (await CancelAsync(manager, Guid.NewGuid(), "Customer cannot attend")).Status);
        var inactiveBranch = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(manager, "Customer cannot attend"));
        Assert.Equal("CK_TestDriveCancellations_Staff", inactiveBranch.ConstraintName);
        await database.Branches.Where(item => item.Id == booking.BranchId)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.IsActive, true));

        var assignments = setup.ServiceProvider.GetRequiredService<TestDriveAssignmentService>();
        Assert.Equal(TestDriveAssignmentStatus.Succeeded, (await assignments.ExecuteAsync(agent, booking.Id,
            TestDriveAssignmentKind.Claimed, new(Guid.NewGuid(), 0), default)).Status);
        Assert.Equal(TestDriveAssignmentStatus.Succeeded, (await assignments.ExecuteAsync(manager, booking.Id,
            TestDriveAssignmentKind.Assigned, new(Guid.NewGuid(), 1, nextAgent, "Cover customer arrival"), default)).Status);
        Assert.Equal(TestDriveCancellationStatus.Forbidden,
            (await CancelAsync(agent, Guid.NewGuid(), "Customer cannot attend")).Status);
        var formerHandler = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(agent, "Customer cannot attend"));
        Assert.Equal("CK_TestDriveCancellations_Staff", formerHandler.ConstraintName);

        var operationId = Guid.NewGuid();
        var repeats = await Task.WhenAll(CancelAsync(nextAgent, operationId, " Customer cannot attend "),
            CancelAsync(nextAgent, operationId, "Customer cannot attend"));
        Assert.All(repeats, result => Assert.Equal(TestDriveCancellationStatus.Succeeded, result.Status));
        var receipt = repeats[0].Cancellation!;
        Assert.Equal(JsonSerializer.Serialize(receipt), JsonSerializer.Serialize(repeats[1].Cancellation));
        Assert.Equal(nextAgent, receipt.StaffId);
        Assert.Equal(booking.CustomerId, receipt.CustomerId);
        Assert.Equal(TestDriveCancellationKind.StaffCancelled, receipt.Kind);
        Assert.Equal(TestDriveCancellationStatus.Conflict,
            (await CancelAsync(nextAgent, operationId, "Different reason")).Status);
        Assert.Equal(TestDriveCancellationStatus.Conflict,
            (await CancelAsync(manager, operationId, "Customer cannot attend")).Status);
        Assert.Equal(TestDriveCancellationStatus.Conflict,
            (await CancelAsync(nextAgent, operationId, "Customer cannot attend", Guid.NewGuid())).Status);
        var immutable = await Assert.ThrowsAsync<PostgresException>(() => database.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"TestDriveCancellations\" SET \"StaffId\" = {manager} WHERE \"Id\" = {operationId}"));
        Assert.Equal("CK_TestDriveCancellations_Immutable", immutable.ConstraintName);
        await database.Users.Where(item => item.Id == nextAgent)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.IsActive, false));
        Assert.Equal(TestDriveCancellationStatus.Forbidden,
            (await CancelAsync(nextAgent, operationId, "Customer cannot attend")).Status);
        Assert.Equal(nextAgent, await database.TestDriveCancellations.Where(item => item.Id == operationId)
            .Select(item => item.StaffId).SingleAsync());
        await database.Users.Where(item => item.Id == nextAgent)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.IsActive, true));
        using var restarted = fixture.CreateFactory();
        using var restartedScope = restarted.Services.CreateScope();
        var replay = await restartedScope.ServiceProvider.GetRequiredService<TestDriveCancellationService>()
            .CancelByStaffAsync(nextAgent, booking.Id, operationId, " Customer cannot attend ", default);
        Assert.Equal(TestDriveCancellationStatus.Succeeded, replay.Status);
        Assert.Equal(JsonSerializer.Serialize(receipt), JsonSerializer.Serialize(replay.Cancellation));

        var source = new TestDriveMailSource(database, TimeProvider.System);
        var mail = await source.ClaimNextAsync(default);
        Assert.NotNull(mail);
        Assert.Contains("Customer cannot attend", mail.Message.Body);
        await source.MarkDeliveredAsync(mail.Id, mail.ClaimId, default);
        Assert.Null(await source.ClaimNextAsync(default));

        using var bookingScope = factory.Services.CreateScope();
        var replacement = await bookingScope.ServiceProvider.GetRequiredService<TestDriveBookingService>()
            .BookAsync(booking.CustomerId, booking.VehicleId,
                new(Guid.NewGuid(), booking.PublicationId, booking.StartsAt), default);
        Assert.Equal(TestDriveBookingStatus.Succeeded, replacement.Status);
        var target = replacement.Booking!.Id;
        var competing = await Task.WhenAll(CancelAsync(manager, Guid.NewGuid(), "Customer requested branch cancellation", target),
            CancelCustomerAsync(target));
        Assert.Single(competing, result => result.Status == TestDriveCancellationStatus.Succeeded);
        Assert.Single(competing, result => result.Status == TestDriveCancellationStatus.Conflict);
        Assert.Equal(1, await database.TestDriveCancellations.CountAsync(item => item.BookingId == target));
        Assert.Equal(1, await database.TestDriveMailOutbox.CountAsync(item => item.BookingId == target && item.Kind == TestDriveMailKind.Cancellation));

        await using var maintenance = new DealershipDbContext(new DbContextOptionsBuilder<DealershipDbContext>()
            .UseNpgsql(fixture.Database.ConnectionString).Options);
        var retained = await Assert.ThrowsAsync<PostgresException>(() => maintenance.GetService<IMigrator>()
            .MigrateAsync("20261103095832_TestDriveAssignments"));
        Assert.Contains("Retained staff cancellations", retained.MessageText);

        async Task<TestDriveCancellationResult> CancelAsync(Guid actor, Guid operation, string reason, Guid? targetId = null)
        {
            using var scope = factory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<TestDriveCancellationService>()
                .CancelByStaffAsync(actor, targetId ?? booking.Id, operation, reason, default);
        }

        async Task<TestDriveCancellationResult> CancelCustomerAsync(Guid targetId)
        {
            using var scope = factory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<TestDriveCancellationService>()
                .CancelAsync(booking.CustomerId, targetId, Guid.NewGuid(), default);
        }

        Task<int> InsertAsync(Guid actor, string reason)
        {
            return database.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "TestDriveCancellations" ("Id", "BookingId", "CustomerId", "Kind", "StaffId", "Reason", "RecordedAt")
                VALUES ({Guid.NewGuid()}, {booking.Id}, {booking.CustomerId}, 'StaffCancelled', {actor}, {reason}, clock_timestamp())
                """);
        }
    }

    private static async Task VerifyCustomerMigrationAsync(IntakePhotoFixture fixture, DealershipDbContext database)
    {
        var original = JsonSerializer.Serialize(await database.TestDriveCancellations.AsNoTracking()
            .OrderBy(item => item.Id).ToArrayAsync());
        await using var maintenance = new DealershipDbContext(new DbContextOptionsBuilder<DealershipDbContext>()
            .UseNpgsql(fixture.Database.ConnectionString).Options);
        var migrator = maintenance.GetService<IMigrator>();
        await migrator.MigrateAsync("20261103095832_TestDriveAssignments");
        await migrator.MigrateAsync();
        var restored = JsonSerializer.Serialize(await database.TestDriveCancellations.AsNoTracking()
            .OrderBy(item => item.Id).ToArrayAsync());
        Assert.Equal(original, restored);
    }
}
