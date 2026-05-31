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

internal static class TestDriveInspectionReopeningAssertions
{
    internal static async Task VerifyAsync(CheckInTestData data, IntakePhotoFixture fixture,
        ListingRecord listing, Guid manager, Guid technician)
    {
        using var scope = data.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        await using var database = NewContext();
        var vehicleId = listing.Revision.VehicleId;
        var current = await database.TestDriveBookings.AsNoTracking().Where(item => item.VehicleId == vehicleId &&
            !database.TestDriveCancellations.Any(cancellation => cancellation.BookingId == item.Id)).SingleAsync();
        var secondCustomer = await database.TestDriveBookings.Where(item => item.VehicleId == vehicleId &&
            item.CustomerId != current.CustomerId).Select(item => item.CustomerId).FirstAsync();
        var second = await services.GetRequiredService<TestDriveBookingService>().BookAsync(secondCustomer, vehicleId,
            new(Guid.NewGuid(), current.PublicationId, current.StartsAt.AddMinutes(30)), default);
        Assert.Equal(TestDriveBookingStatus.Succeeded, second.Status);
        var bookings = new[] { current, second.Booking! };
        var ids = bookings.Select(item => item.Id).ToArray();
        var original = await SnapshotAsync();
        var draft = await database.InspectionDrafts.AsNoTracking().SingleAsync(item => item.Id == listing.InspectionId);
        var signed = await database.InspectionSignedRevisions.AsNoTracking().SingleAsync(item =>
            item.InspectionId == draft.Id && item.Revision == draft.Revision);
        var saveVersion = await database.InspectionDraftSaves.Where(item => item.InspectionId == draft.Id)
            .MaxAsync(item => item.Version);
        var request = new InspectionReopenRequest(Guid.NewGuid(), draft.AssignmentVersion, draft.Revision,
            "Check vehicle condition before the next demonstration");
        var signal = new MailDeliverySignal();

        Assert.True(await database.Database.SqlQueryRaw<bool>("""
            SELECT EXISTS (SELECT 1 FROM pg_constraint
                WHERE conrelid = '"TestDriveCancellations"'::regclass AND contype = 'f' AND
                    confrelid = '"InspectionReopenings"'::regclass AND confdeltype = 'r') AS "Value"
            """).SingleAsync());

        await AssertCauseDeniedAsync(current, Guid.NewGuid());
        await using (var pending = NewContext())
        {
            await using var transaction = await pending.Database.BeginTransactionAsync();
            pending.InspectionReopenings.Add(new(new(request.OperationId, draft.Id, draft.BranchId, manager,
                signed.Id, draft.AssignmentVersion, draft.Revision, draft.Revision + 1, saveVersion,
                request.Reason!, DateTimeOffset.UtcNow)));
            await pending.SaveChangesAsync();
            Assert.Equal(2, await pending.TestDriveCancellations.CountAsync(item => item.InspectionReopeningId == request.OperationId));
            Assert.Equal(2, await pending.TestDriveMailOutbox.CountAsync(item =>
                ids.Contains(item.BookingId) && item.Kind == TestDriveMailKind.Cancellation));
            Assert.Equal(VehicleStatus.PendingInspection, await pending.Vehicles.Where(item => item.Id == vehicleId)
                .Select(item => item.Status).SingleAsync());
            await transaction.RollbackAsync();
        }

        Assert.Equal(original, await SnapshotAsync());
        Assert.True(await database.InspectionDrafts.Where(item => item.Id == draft.Id).Select(item => item.IsSigned).SingleAsync());
        Assert.False(await database.InspectionReopenings.AnyAsync(item => item.Id == request.OperationId));
        var results = await Task.WhenAll(ReopenAsync(), ReopenAsync());
        Assert.All(results, result => Assert.Equal(InspectionAssignmentStatus.Succeeded, result.Status));
        Assert.Equal(JsonSerializer.Serialize(results[0].Receipt), JsonSerializer.Serialize(results[1].Receipt));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        Assert.True(await signal.WaitAsync(deadline.Token));
        var cancelled = await database.TestDriveCancellations.AsNoTracking()
            .Where(item => item.InspectionReopeningId == request.OperationId).ToArrayAsync();
        Assert.Equal(ids.Order(), cancelled.Select(item => item.BookingId).Order());
        Assert.All(cancelled, item =>
        {
            Assert.Equal(TestDriveCancellationKind.InspectionReopened, item.Kind);
            Assert.Equal("Vehicle requires reinspection", item.Reason);
            Assert.Null(item.StaffId);
            Assert.Null(item.ReservationId);
            Assert.Null(item.ListingWithdrawalId);
            Assert.Equal(bookings.Single(booking => booking.Id == item.BookingId).CustomerId, item.CustomerId);
        });
        Assert.Equal(original, await SnapshotAsync());
        var other = await database.TestDriveBookings.AsNoTracking().SingleAsync(item => item.VehicleId != vehicleId &&
            !database.TestDriveCancellations.Any(cancellation => cancellation.BookingId == item.Id));
        await AssertCauseDeniedAsync(other, request.OperationId);
        Assert.False(await database.TestDriveCancellations.AnyAsync(item => item.BookingId == other.Id));
        Assert.Equal(TestDriveBookingStatus.VehicleUnavailable, (await services.GetRequiredService<TestDriveBookingService>()
            .BookAsync(current.CustomerId, vehicleId,
                new(Guid.NewGuid(), current.PublicationId, current.StartsAt), default)).Status);
        var receiptSnapshot = JsonSerializer.Serialize(cancelled.OrderBy(item => item.Id));

        await using (var restarted = NewContext())
        {
            var mail = new TestDriveMailSource(restarted, TimeProvider.System);
            var delivered = new List<Guid>();
            while (await mail.ClaimNextAsync(default) is { } message)
            {
                var intent = await database.TestDriveMailOutbox.AsNoTracking().SingleAsync(item => item.Id == message.Id);
                if (ids.Contains(intent.BookingId))
                {
                    Assert.Equal(TestDriveMailKind.Cancellation, intent.Kind);
                    Assert.Contains("Reason: Vehicle requires reinspection", message.Message.Body);
                    delivered.Add(intent.BookingId);
                }

                await mail.MarkDeliveredAsync(message.Id, message.ClaimId, default);
            }

            Assert.Equal(ids.Order(), delivered.Order());
            var replay = await new InspectionReopeningService(new InspectionReopeningStore(restarted, signal))
                .ReopenAsync(manager, draft.Id, request, default);
            Assert.Equal(JsonSerializer.Serialize(results[0].Receipt), JsonSerializer.Serialize(replay.Receipt));
        }

        var immutable = await Assert.ThrowsAsync<PostgresException>(() => database.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"TestDriveCancellations\" SET \"InspectionReopeningId\" = NULL WHERE \"Id\" = {cancelled[0].Id}"));
        Assert.Equal("CK_TestDriveCancellations_Immutable", immutable.ConstraintName);
        var immutableCause = await Assert.ThrowsAsync<PostgresException>(() => database.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"InspectionReopenings\" SET \"Reason\" = {"Changed condition"} WHERE \"Id\" = {request.OperationId}"));
        Assert.Equal("CK_InspectionReopenings_Immutable", immutableCause.ConstraintName);
        await using var maintenance = new DealershipDbContext(new DbContextOptionsBuilder<DealershipDbContext>()
            .UseNpgsql(fixture.Database.ConnectionString).Options);
        var rollback = await Assert.ThrowsAsync<PostgresException>(() => maintenance.GetService<IMigrator>()
            .MigrateAsync("20261103113429_TestDriveListingWithdrawalCancellations"));
        Assert.Contains("Retained inspection reopening cancellations", rollback.MessageText);

        var saved = await services.GetRequiredService<InspectionDraftSaveService>().SaveAsync(technician, draft.Id,
            new(Guid.NewGuid(), draft.AssignmentVersion, saveVersion, [])
            { ExaminedPointCodes = [.. InspectionCatalog.Points.Select(point => point.Code)] }, default);
        Assert.Equal(InspectionAssignmentStatus.Succeeded, saved.Status);
        var resigned = await services.GetRequiredService<InspectionSignoffService>().SignAsync(technician, draft.Id,
            new(Guid.NewGuid(), draft.AssignmentVersion, saveVersion + 1, draft.Revision + 1), default);
        Assert.Equal(InspectionAssignmentStatus.Succeeded, resigned.Status);
        var listings = services.GetRequiredService<ListingService>();
        var latest = await database.ListingRevisions.AsNoTracking().Where(item => item.VehicleId == vehicleId)
            .OrderByDescending(item => item.Version).FirstAsync();
        var listingSave = await listings.SaveAsync(manager, vehicleId,
            new(Guid.NewGuid(), latest.Version, latest.VehiclePrice, latest.RoadTax, latest.Transmission, latest.Fuel,
                latest.Description)
            { PhotoIds = latest.PhotoIds }, default);
        Assert.Equal(ListingStatus.Succeeded, listingSave.Status);
        var publication = await listings.PublishAsync(manager, vehicleId,
            new(Guid.NewGuid(), listingSave.Draft!.Id, resigned.Report!.OperationId), default);
        Assert.Equal(ListingStatus.Succeeded, publication.Status);
        var next = await services.GetRequiredService<TestDriveBookingService>().BookAsync(current.CustomerId, vehicleId,
            new(Guid.NewGuid(), publication.Publication!.Id, current.StartsAt), default);
        Assert.Equal(TestDriveBookingStatus.Succeeded, next.Status);
        await AssertCauseDeniedAsync(next.Booking!, request.OperationId);
        Assert.False(await database.TestDriveCancellations.AnyAsync(item => item.BookingId == next.Booking!.Id));
        Assert.Equal(receiptSnapshot, JsonSerializer.Serialize(await database.TestDriveCancellations.AsNoTracking()
            .Where(item => item.InspectionReopeningId == request.OperationId).OrderBy(item => item.Id).ToArrayAsync()));

        var competingReopen = request with { OperationId = Guid.NewGuid(), Revision = draft.Revision + 1 };
        var competingBooking = BookCompetingAsync();
        var reopenedAgain = await services.GetRequiredService<InspectionReopeningService>()
            .ReopenAsync(manager, draft.Id, competingReopen, default);
        var bookedAgain = await competingBooking;
        Assert.Equal(InspectionAssignmentStatus.Succeeded, reopenedAgain.Status);
        Assert.Contains(bookedAgain.Status, new[] { TestDriveBookingStatus.Succeeded, TestDriveBookingStatus.VehicleUnavailable });
        Assert.False(await database.TestDriveBookings.AnyAsync(item => item.VehicleId == vehicleId &&
            !database.TestDriveCancellations.Any(cancellation => cancellation.BookingId == item.Id)));
        if (bookedAgain.Booking is not null)
        {
            Assert.Equal(competingReopen.OperationId, (await database.TestDriveCancellations.AsNoTracking()
                .SingleAsync(item => item.BookingId == bookedAgain.Booking.Id)).InspectionReopeningId);
        }

        async Task<TestDriveBookingResult> BookCompetingAsync()
        {
            using var commandScope = data.Factory.Services.CreateScope();
            return await commandScope.ServiceProvider.GetRequiredService<TestDriveBookingService>()
                .BookAsync(secondCustomer, vehicleId,
                    new(Guid.NewGuid(), publication.Publication!.Id, current.StartsAt.AddMinutes(30)), default);
        }

        async Task<InspectionReopenResult> ReopenAsync()
        {
            await using var writer = NewContext();
            return await new InspectionReopeningService(new InspectionReopeningStore(writer, signal))
                .ReopenAsync(manager, draft.Id, request, default);
        }

        async Task AssertCauseDeniedAsync(TestDriveBooking booking, Guid causeId)
        {
            var failure = await Assert.ThrowsAsync<PostgresException>(() => database.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "TestDriveCancellations"
                    ("Id", "BookingId", "CustomerId", "Kind", "InspectionReopeningId", "Reason", "RecordedAt")
                VALUES ({Guid.NewGuid()}, {booking.Id}, {booking.CustomerId}, 'InspectionReopened', {causeId},
                    'Vehicle requires reinspection', clock_timestamp())
                """));
            Assert.Equal("CK_TestDriveCancellations_Reopening", failure.ConstraintName);
        }

        async Task<string> SnapshotAsync()
        {
            return JsonSerializer.Serialize(await database.TestDriveCancellations.AsNoTracking()
                .Where(item => item.InspectionReopeningId == null).OrderBy(item => item.Id).ToArrayAsync());
        }

        DealershipDbContext NewContext() => new(new DbContextOptionsBuilder<DealershipDbContext>()
            .UseNpgsql(fixture.Database.RuntimeConnectionString).Options);
    }
}
