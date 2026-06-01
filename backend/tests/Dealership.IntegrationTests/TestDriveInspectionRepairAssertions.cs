using System.Text.Json;
using Dealership.Application.Inspections;
using Dealership.Application.TestDrives;
using Dealership.Application.Vehicles;
using Dealership.Domain.Inspections;
using Dealership.Domain.TestDrives;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Inspections;
using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.TestDrives;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static Dealership.IntegrationTests.ReservationTestData;

namespace Dealership.IntegrationTests;

internal static class TestDriveInspectionRepairAssertions
{
    internal static async Task VerifyAsync(CheckInTestData data, IntakePhotoFixture fixture,
        ListingRecord listing, Guid manager, Guid technician)
    {
        using var scope = data.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        await using var database = NewContext();
        var draft = await database.InspectionDrafts.AsNoTracking().SingleAsync(item => item.Id == listing.InspectionId);
        var saveVersion = await database.InspectionDraftSaves.Where(item => item.InspectionId == draft.Id).MaxAsync(item => item.Version);
        var saved = await services.GetRequiredService<InspectionDraftSaveService>().SaveAsync(technician, draft.Id,
            new(Guid.NewGuid(), draft.AssignmentVersion, saveVersion,
                [new("EP-004", InspectionEntryKind.Finding, InspectionSeverity.Advisory, 0, "Light bumper surface mark")])
            { ExaminedPointCodes = [.. InspectionCatalog.Points.Select(point => point.Code)] }, default);
        Assert.Equal(InspectionAssignmentStatus.Succeeded, saved.Status);
        var signed = await services.GetRequiredService<InspectionSignoffService>().SignAsync(technician, draft.Id,
            new(Guid.NewGuid(), draft.AssignmentVersion, saveVersion + 1, draft.Revision), default);
        Assert.Equal(InspectionAssignmentStatus.Succeeded, signed.Status);
        var latest = await database.ListingRevisions.AsNoTracking().Where(item => item.VehicleId == draft.VehicleId)
            .OrderByDescending(item => item.Version).FirstAsync();
        var listings = services.GetRequiredService<ListingService>();
        var listingSave = await listings.SaveAsync(manager, draft.VehicleId,
            new(Guid.NewGuid(), latest.Version, latest.VehiclePrice, latest.RoadTax, latest.Transmission, latest.Fuel,
                latest.Description)
            { PhotoIds = latest.PhotoIds }, default);
        Assert.Equal(ListingStatus.Succeeded, listingSave.Status);
        var published = await listings.PublishAsync(manager, draft.VehicleId,
            new(Guid.NewGuid(), listingSave.Draft!.Id, signed.Report!.OperationId), default);
        Assert.Equal(ListingStatus.Succeeded, published.Status);
        var prior = await database.TestDriveBookings.AsNoTracking().FirstAsync(item => item.VehicleId == draft.VehicleId);
        var booked = await services.GetRequiredService<TestDriveBookingService>().BookAsync(prior.CustomerId, draft.VehicleId,
            new(Guid.NewGuid(), published.Publication!.Id, prior.StartsAt), default);
        Assert.Equal(TestDriveBookingStatus.Succeeded, booked.Status);
        var booking = booked.Booking!;
        var original = await SnapshotAsync();
        var request = new InspectionRepairRequest(Guid.NewGuid(), draft.AssignmentVersion, draft.Revision,
            draft.RepairVersion, [new("EP-004", "Polish bumper surface")], 0);
        var signal = new MailDeliverySignal();
        Assert.True(await database.Database.SqlQueryRaw<bool>("""
            SELECT EXISTS (SELECT 1 FROM pg_constraint
                WHERE conrelid = '"TestDriveCancellations"'::regclass AND contype = 'f' AND
                    confrelid = '"InspectionRepairRequests"'::regclass AND confdeltype = 'r') AS "Value"
            """).SingleAsync());
        await AssertCauseDeniedAsync(booking, Guid.NewGuid());

        await using (var pending = NewContext())
        {
            await using var transaction = await pending.Database.BeginTransactionAsync();
            pending.InspectionRepairRequests.Add(new(new(request.OperationId, draft.Id, draft.BranchId, technician,
                signed.Report.OperationId, draft.AssignmentVersion, draft.Revision, draft.RepairVersion,
                draft.RepairVersion + 1, request.Items, request.Budget, DateTimeOffset.UtcNow)));
            await pending.SaveChangesAsync();
            Assert.Equal(request.OperationId, (await pending.TestDriveCancellations.SingleAsync(item =>
                item.BookingId == booking.Id)).InspectionRepairRequestId);
            Assert.True(await pending.TestDriveMailOutbox.AnyAsync(item => item.BookingId == booking.Id &&
                item.Kind == TestDriveMailKind.Cancellation));
            Assert.Equal(VehicleStatus.PendingInspection, await pending.Vehicles.Where(item => item.Id == draft.VehicleId)
                .Select(item => item.Status).SingleAsync());
            await transaction.RollbackAsync();
        }

        Assert.False(await database.TestDriveCancellations.AnyAsync(item => item.BookingId == booking.Id));
        Assert.False(await database.InspectionRepairRequests.AnyAsync(item => item.Id == request.OperationId));
        var race = await Task.WhenAll(RequestAsync(), RequestAsync());
        Assert.All(race, result => Assert.Equal(InspectionAssignmentStatus.Succeeded, result.Status));
        Assert.Equal(JsonSerializer.Serialize(race[0].Receipt), JsonSerializer.Serialize(race[1].Receipt));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        Assert.True(await signal.WaitAsync(deadline.Token));
        var cancellation = await database.TestDriveCancellations.AsNoTracking().SingleAsync(item => item.BookingId == booking.Id);
        Assert.Equal(TestDriveCancellationKind.InspectionRepairRequested, cancellation.Kind);
        Assert.Equal(request.OperationId, cancellation.InspectionRepairRequestId);
        Assert.Equal(booking.CustomerId, cancellation.CustomerId);
        Assert.Equal("Vehicle requires repairs", cancellation.Reason);
        Assert.Null(cancellation.StaffId);
        Assert.Null(cancellation.ReservationId);
        Assert.Null(cancellation.ListingWithdrawalId);
        Assert.Null(cancellation.InspectionReopeningId);
        Assert.Equal(original, await SnapshotAsync());
        var other = await database.TestDriveBookings.AsNoTracking().SingleAsync(item => item.VehicleId != draft.VehicleId &&
            !database.TestDriveCancellations.Any(cancelled => cancelled.BookingId == item.Id));
        await AssertCauseDeniedAsync(other, request.OperationId);
        Assert.False(await database.TestDriveCancellations.AnyAsync(item => item.BookingId == other.Id));

        await using (var restarted = NewContext())
        {
            var replay = await new InspectionRepairRequestService(new InspectionRepairRequestStore(restarted, signal))
                .RequestAsync(technician, draft.Id, request, default);
            Assert.Equal(JsonSerializer.Serialize(race[0].Receipt), JsonSerializer.Serialize(replay.Receipt));
            using var noWake = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await signal.WaitAsync(noWake.Token));
            var mail = new TestDriveMailSource(restarted, TimeProvider.System);
            var delivered = 0;
            while (await mail.ClaimNextAsync(default) is { } message)
            {
                var intent = await database.TestDriveMailOutbox.AsNoTracking().SingleAsync(item => item.Id == message.Id);
                if (intent.BookingId == booking.Id)
                {
                    Assert.Equal(TestDriveMailKind.Cancellation, intent.Kind);
                    Assert.Contains("Reason: Vehicle requires repairs", message.Message.Body);
                    delivered++;
                }

                await mail.MarkDeliveredAsync(message.Id, message.ClaimId, default);
            }

            Assert.Equal(1, delivered);
        }

        var immutable = await Assert.ThrowsAsync<PostgresException>(() => database.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"TestDriveCancellations\" SET \"InspectionRepairRequestId\" = NULL WHERE \"Id\" = {cancellation.Id}"));
        Assert.Equal("CK_TestDriveCancellations_Immutable", immutable.ConstraintName);
        await using var maintenance = new DealershipDbContext(new DbContextOptionsBuilder<DealershipDbContext>()
            .UseNpgsql(fixture.Database.ConnectionString).Options);
        var rollback = await Assert.ThrowsAsync<PostgresException>(() => maintenance.GetService<IMigrator>()
            .MigrateAsync("20261103122300_TestDriveInspectionReopeningCancellations"));
        Assert.Contains("Retained inspection repair-request cancellations", rollback.MessageText);
        var completed = await services.GetRequiredService<InspectionRepairCompletionService>().CompleteAsync(technician,
            draft.Id, new(Guid.NewGuid(), request.OperationId, draft.AssignmentVersion, draft.Revision,
                draft.RepairVersion + 1, "Bumper surface polished and checked"), default);
        Assert.Equal(InspectionAssignmentStatus.Succeeded, completed.Status);
        Assert.Equal(VehicleStatus.Available, await database.Vehicles.Where(item => item.Id == draft.VehicleId)
            .Select(item => item.Status).SingleAsync());
        Assert.Equal(cancellation.Id, (await database.TestDriveCancellations.AsNoTracking()
            .SingleAsync(item => item.BookingId == booking.Id)).Id);
        var next = await services.GetRequiredService<TestDriveBookingService>().BookAsync(booking.CustomerId, draft.VehicleId,
            new(Guid.NewGuid(), published.Publication.Id, booking.StartsAt), default);
        Assert.Equal(TestDriveBookingStatus.Succeeded, next.Status);
        await AssertCauseDeniedAsync(next.Booking!, request.OperationId);
        Assert.False(await database.TestDriveCancellations.AnyAsync(item => item.BookingId == next.Booking!.Id));

        request = request with { OperationId = Guid.NewGuid(), ExpectedVersion = draft.RepairVersion + 1 };
        var competingCustomer = await database.TestDriveBookings.Where(item => item.VehicleId == draft.VehicleId &&
            item.CustomerId != booking.CustomerId).Select(item => item.CustomerId).FirstAsync();
        var competing = BookCompetingAsync();
        Assert.Equal(InspectionAssignmentStatus.Succeeded, (await RequestAsync()).Status);
        var raced = await competing;
        Assert.Contains(raced.Status, new[] { TestDriveBookingStatus.Succeeded, TestDriveBookingStatus.VehicleUnavailable });
        Assert.False(await database.TestDriveBookings.AnyAsync(item => item.VehicleId == draft.VehicleId &&
            !database.TestDriveCancellations.Any(cancelled => cancelled.BookingId == item.Id)));

        async Task<TestDriveBookingResult> BookCompetingAsync()
        {
            using var commandScope = data.Factory.Services.CreateScope();
            return await commandScope.ServiceProvider.GetRequiredService<TestDriveBookingService>()
                .BookAsync(competingCustomer, draft.VehicleId,
                    new(Guid.NewGuid(), published.Publication.Id, booking.StartsAt.AddMinutes(30)), default);
        }

        async Task<InspectionRepairResult> RequestAsync()
        {
            await using var writer = NewContext();
            return await new InspectionRepairRequestService(new InspectionRepairRequestStore(writer, signal))
                .RequestAsync(technician, draft.Id, request, default);
        }

        async Task AssertCauseDeniedAsync(TestDriveBooking target, Guid causeId)
        {
            var failure = await Assert.ThrowsAsync<PostgresException>(() => database.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "TestDriveCancellations"
                    ("Id", "BookingId", "CustomerId", "Kind", "InspectionRepairRequestId", "Reason", "RecordedAt")
                VALUES ({Guid.NewGuid()}, {target.Id}, {target.CustomerId}, 'InspectionRepairRequested', {causeId},
                    'Vehicle requires repairs', clock_timestamp())
                """));
            Assert.Equal("CK_TestDriveCancellations_RepairRequest", failure.ConstraintName);
        }

        async Task<string> SnapshotAsync()
        {
            return JsonSerializer.Serialize(await database.TestDriveCancellations.AsNoTracking()
                .Where(item => item.InspectionRepairRequestId == null).OrderBy(item => item.Id).ToArrayAsync());
        }

        DealershipDbContext NewContext() => new(new DbContextOptionsBuilder<DealershipDbContext>()
            .UseNpgsql(fixture.Database.RuntimeConnectionString).Options);
    }
}
