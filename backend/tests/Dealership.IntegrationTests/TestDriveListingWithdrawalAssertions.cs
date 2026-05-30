using System.Text.Json;
using Dealership.Application.TestDrives;
using Dealership.Application.Vehicles;
using Dealership.Domain.TestDrives;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.TestDrives;
using Dealership.Infrastructure.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static Dealership.IntegrationTests.ReservationTestData;

namespace Dealership.IntegrationTests;

internal static class TestDriveListingWithdrawalAssertions
{
    internal static async Task VerifyAsync(CheckInTestData data, IntakePhotoFixture fixture,
        ListingRecord listing, Guid manager, Guid technician, Guid reservationId)
    {
        using var scope = data.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        Assert.Equal(ReservationStatus.Succeeded, (await services.GetRequiredService<ReservationCancellationService>()
            .CancelAsync(data.OwnerId, reservationId, new(Guid.NewGuid(), null), default)).Status);
        await using var database = NewContext();
        var original = await SnapshotAsync();
        var previous = await database.TestDriveBookings.AsNoTracking()
            .Where(item => item.VehicleId == listing.Revision.VehicleId).OrderBy(item => item.StartsAt).ToArrayAsync();
        var bookings = new List<TestDriveBooking>();
        foreach (var source in previous.DistinctBy(item => item.CustomerId).Take(2))
        {
            var result = await services.GetRequiredService<TestDriveBookingService>().BookAsync(source.CustomerId,
                source.VehicleId, new(Guid.NewGuid(), source.PublicationId, source.StartsAt), default);
            Assert.Equal(TestDriveBookingStatus.Succeeded, result.Status);
            bookings.Add(result.Booking!);
        }

        Assert.Equal(2, bookings.Count);
        var ids = bookings.Select(item => item.Id).ToArray();
        var first = bookings[0];
        var forged = await Assert.ThrowsAsync<PostgresException>(() => database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "TestDriveCancellations"
                ("Id", "BookingId", "CustomerId", "Kind", "ListingWithdrawalId", "Reason", "RecordedAt")
            VALUES ({Guid.NewGuid()}, {first.Id}, {first.CustomerId}, 'ListingWithdrawn', {Guid.NewGuid()},
                'Listing withdrawn', clock_timestamp())
            """));
        Assert.Equal("CK_TestDriveCancellations_Withdrawal", forged.ConstraintName);
        var otherListing = await CreateListingAsync(data, services, manager, technician, 1);
        var otherCustomer = await RegisterCustomerAsync(services, database);
        var otherBooking = await services.GetRequiredService<TestDriveBookingService>().BookAsync(otherCustomer,
            otherListing.Revision.VehicleId, new(Guid.NewGuid(), otherListing.Publication.Id, first.StartsAt.AddHours(1)), default);
        Assert.Equal(TestDriveBookingStatus.Succeeded, otherBooking.Status);
        const string reason = "Vehicle removed from sale";
        var request = new ListingWithdrawRequest(Guid.NewGuid(), listing.Publication.Id, reason);
        var signal = new MailDeliverySignal();
        await using (var pending = NewContext())
        {
            await using var transaction = await pending.Database.BeginTransactionAsync();
            pending.ListingWithdrawals.Add(new ListingWithdrawal(request.OperationId, listing.Publication,
                manager, reason, DateTimeOffset.UtcNow));
            await pending.SaveChangesAsync();
            Assert.Equal(bookings.Count, await pending.TestDriveCancellations.CountAsync(item =>
                item.ListingWithdrawalId == request.OperationId));
            Assert.Equal(bookings.Count, await pending.TestDriveMailOutbox.CountAsync(item =>
                item.Kind == TestDriveMailKind.Cancellation && ids.Contains(item.BookingId)));
            await transaction.RollbackAsync();
        }

        Assert.Equal(original, await SnapshotAsync());
        var results = await Task.WhenAll(WithdrawAsync(), WithdrawAsync());
        Assert.All(results, result => Assert.Equal(ListingStatus.Succeeded, result.Status));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        Assert.True(await signal.WaitAsync(deadline.Token));
        Assert.Equal(JsonSerializer.Serialize(results[0].Withdrawal), JsonSerializer.Serialize(results[1].Withdrawal));
        var cancelled = await database.TestDriveCancellations.AsNoTracking()
            .Where(item => item.ListingWithdrawalId == request.OperationId).ToArrayAsync();
        Assert.Equal(bookings.Select(item => item.Id).Order(), cancelled.Select(item => item.BookingId).Order());
        Assert.All(cancelled, item =>
        {
            Assert.Equal(TestDriveCancellationKind.ListingWithdrawn, item.Kind);
            Assert.Null(item.StaffId);
            Assert.Null(item.ReservationId);
            Assert.Equal("Listing withdrawn", item.Reason);
            Assert.Equal(bookings.Single(booking => booking.Id == item.BookingId).CustomerId, item.CustomerId);
        });
        Assert.Equal(original, await SnapshotAsync());

        var wrongVehicle = await Assert.ThrowsAsync<PostgresException>(() => database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "TestDriveCancellations"
                ("Id", "BookingId", "CustomerId", "Kind", "ListingWithdrawalId", "Reason", "RecordedAt")
            VALUES ({Guid.NewGuid()}, {otherBooking.Booking!.Id}, {otherCustomer}, 'ListingWithdrawn', {request.OperationId},
                'Listing withdrawn', clock_timestamp())
            """));
        Assert.Equal("CK_TestDriveCancellations_Withdrawal", wrongVehicle.ConstraintName);
        Assert.False(await database.TestDriveCancellations.AnyAsync(item => item.BookingId == otherBooking.Booking!.Id));

        await using var restarted = NewContext();
        var mail = new TestDriveMailSource(restarted, TimeProvider.System);
        var delivered = new List<Guid>();
        while (await mail.ClaimNextAsync(default) is { } message)
        {
            var intent = await database.TestDriveMailOutbox.AsNoTracking().SingleAsync(item => item.Id == message.Id);
            if (ids.Contains(intent.BookingId))
            {
                Assert.Equal(TestDriveMailKind.Cancellation, intent.Kind);
                Assert.Contains("Reason: Listing withdrawn", message.Message.Body);
                delivered.Add(intent.BookingId);
            }

            await mail.MarkDeliveredAsync(message.Id, message.ClaimId, default);
        }

        Assert.Equal(ids.Order(), delivered.Order());
        var immutable = await Assert.ThrowsAsync<PostgresException>(() => database.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"TestDriveCancellations\" SET \"ListingWithdrawalId\" = NULL WHERE \"Id\" = {cancelled[0].Id}"));
        Assert.Equal("CK_TestDriveCancellations_Immutable", immutable.ConstraintName);
        await using var maintenance = new DealershipDbContext(new DbContextOptionsBuilder<DealershipDbContext>()
            .UseNpgsql(fixture.Database.ConnectionString).Options);
        var rollback = await Assert.ThrowsAsync<PostgresException>(() => maintenance.GetService<IMigrator>()
            .MigrateAsync("20261103110638_TestDriveReservationCancellations"));
        Assert.Contains("Retained listing withdrawal cancellations", rollback.MessageText);

        var listingService = services.GetRequiredService<ListingService>();
        var revision = listing.Revision;
        var saved = await listingService.SaveAsync(manager, revision.VehicleId,
            new(Guid.NewGuid(), revision.Version, revision.VehiclePrice, revision.RoadTax,
                revision.Transmission, revision.Fuel, revision.Description)
            { PhotoIds = revision.PhotoIds }, default);
        Assert.Equal(ListingStatus.Succeeded, saved.Status);
        var republished = await listingService.PublishAsync(manager, revision.VehicleId,
            new(Guid.NewGuid(), saved.Draft!.Id, listing.Publication.SignedRevisionId), default);
        Assert.Equal(ListingStatus.Succeeded, republished.Status);
        Assert.Equal(bookings.Count, await database.TestDriveCancellations.CountAsync(item =>
            item.ListingWithdrawalId == request.OperationId));
        var next = await services.GetRequiredService<TestDriveBookingService>().BookAsync(first.CustomerId,
            first.VehicleId, new(Guid.NewGuid(), republished.Publication!.Id, first.StartsAt), default);
        Assert.Equal(TestDriveBookingStatus.Succeeded, next.Status);
        var oldCause = await Assert.ThrowsAsync<PostgresException>(() => database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "TestDriveCancellations"
                ("Id", "BookingId", "CustomerId", "Kind", "ListingWithdrawalId", "Reason", "RecordedAt")
            VALUES ({Guid.NewGuid()}, {next.Booking!.Id}, {first.CustomerId}, 'ListingWithdrawn', {request.OperationId},
                'Listing withdrawn', clock_timestamp())
            """));
        Assert.Equal("CK_TestDriveCancellations_Withdrawal", oldCause.ConstraintName);
        Assert.False(await database.TestDriveCancellations.AnyAsync(item => item.BookingId == next.Booking!.Id));

        async Task<ListingResult> WithdrawAsync()
        {
            await using var writer = NewContext();
            return await new ListingService(new ListingStore(writer, signal))
                .WithdrawAsync(manager, listing.Revision.VehicleId, request, default);
        }

        async Task<string> SnapshotAsync()
        {
            return JsonSerializer.Serialize(await database.TestDriveCancellations.AsNoTracking()
                .Where(item => item.ListingWithdrawalId == null).OrderBy(item => item.Id).ToArrayAsync());
        }

        DealershipDbContext NewContext() => new(new DbContextOptionsBuilder<DealershipDbContext>()
            .UseNpgsql(fixture.Database.RuntimeConnectionString).Options);
    }
}
