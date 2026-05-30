using System.Text.Json;
using Dealership.Application.Vehicles;
using Dealership.Domain.TestDrives;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.TestDrives;
using Dealership.Infrastructure.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static Dealership.IntegrationTests.ReservationTestData;

namespace Dealership.IntegrationTests;

internal static class TestDriveReservationCancellationAssertions
{
    internal static async Task<ReservationResult> VerifyAsync(CheckInTestData data, IntakePhotoFixture fixture,
        ListingRecord listing, ReservationRequest request, MailDeliverySignal mail, ReservationExpirySignal expiry)
    {
        await using var database = NewContext();
        var vehicleId = listing.Revision.VehicleId;
        var original = JsonSerializer.Serialize(await database.TestDriveCancellations.AsNoTracking()
            .OrderBy(item => item.Id).ToArrayAsync());
        var outstanding = await database.TestDriveBookings.AsNoTracking().Where(item => item.VehicleId == vehicleId &&
            !database.TestDriveCancellations.Any(cancelled => cancelled.BookingId == item.Id)).ToArrayAsync();
        Assert.NotEmpty(outstanding);
        var booking = outstanding[0];
        var forged = await Assert.ThrowsAsync<PostgresException>(() => database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "TestDriveCancellations"
                ("Id", "BookingId", "CustomerId", "Kind", "ReservationId", "Reason", "RecordedAt")
            VALUES ({Guid.NewGuid()}, {booking.Id}, {booking.CustomerId}, 'VehicleReserved', {Guid.NewGuid()},
                'Vehicle reserved', clock_timestamp())
            """));
        Assert.Equal("CK_TestDriveCancellations_Reservation", forged.ConstraintName);

        // A valid direct reservation without its required sale must roll back cancellations and mail too.
        await using (var failed = NewContext())
        {
            await using var transaction = await new ReservationStore(failed, mail, expiry)
                .LockAsync(data.OwnerId, vehicleId, request, default);
            var context = transaction.Context;
            var reservation = new Reservation(request.OperationId, context.Profile!, context.Customer!.Email!,
                context.Publication!, context.Listing!, context.Settings!, null, context.Now);
            reservation.AssignNumber("VRS-2026-999999");
            failed.Reservations.Add(reservation);
            await failed.SaveChangesAsync();
            Assert.Equal(outstanding.Length, await failed.TestDriveCancellations.CountAsync(item =>
                item.ReservationId == request.OperationId));
            var missingSale = await Assert.ThrowsAsync<PostgresException>(() =>
                failed.Database.CurrentTransaction!.CommitAsync());
            Assert.Equal("CK_Reservations_Sale", missingSale.ConstraintName);
        }

        Assert.Equal(original, JsonSerializer.Serialize(await database.TestDriveCancellations.AsNoTracking()
            .OrderBy(item => item.Id).ToArrayAsync()));
        var results = await Task.WhenAll(ReserveAsync(), ReserveAsync());
        Assert.All(results, result => Assert.Equal(ReservationStatus.Succeeded, result.Status));
        Assert.Equal(results[0].Reservation!.Id, results[1].Reservation!.Id);
        var cancellations = await database.TestDriveCancellations.AsNoTracking()
            .Where(item => item.ReservationId == request.OperationId).ToArrayAsync();
        Assert.Equal(outstanding.Select(item => item.Id).Order(), cancellations.Select(item => item.BookingId).Order());
        Assert.All(cancellations, item =>
        {
            Assert.Equal(TestDriveCancellationKind.VehicleReserved, item.Kind);
            Assert.Null(item.StaffId);
            Assert.Equal("Vehicle reserved", item.Reason);
            Assert.Equal(outstanding.Single(booking => booking.Id == item.BookingId).CustomerId, item.CustomerId);
        });
        Assert.Equal(original, JsonSerializer.Serialize(await database.TestDriveCancellations.AsNoTracking()
            .Where(item => item.ReservationId == null).OrderBy(item => item.Id).ToArrayAsync()));

        await using var reconstructed = NewContext();
        var source = new TestDriveMailSource(reconstructed, TimeProvider.System);
        var bookingIds = outstanding.Select(item => item.Id).ToArray();
        var queued = await database.TestDriveMailOutbox.Where(item =>
            item.Kind == TestDriveMailKind.Cancellation && bookingIds.Contains(item.BookingId))
            .Select(item => item.Id).ToArrayAsync();
        var confirmations = await database.TestDriveMailOutbox.Where(item =>
            item.Kind == TestDriveMailKind.Confirmation && bookingIds.Contains(item.BookingId))
            .Select(item => item.Id).ToArrayAsync();
        var delivered = new List<Guid>();
        while (await source.ClaimNextAsync(default) is { } message)
        {
            Assert.DoesNotContain(message.Id, confirmations);
            if (queued.Contains(message.Id))
            {
                Assert.Contains("Reason: Vehicle reserved", message.Message.Body);
                delivered.Add(message.Id);
            }

            await source.MarkDeliveredAsync(message.Id, message.ClaimId, default);
        }

        Assert.Equal(queued.Order(), delivered.Order());
        var immutable = await Assert.ThrowsAsync<PostgresException>(() => database.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"TestDriveCancellations\" SET \"ReservationId\" = NULL WHERE \"Id\" = {cancellations[0].Id}"));
        Assert.Equal("CK_TestDriveCancellations_Immutable", immutable.ConstraintName);
        await using var maintenance = new DealershipDbContext(new DbContextOptionsBuilder<DealershipDbContext>()
            .UseNpgsql(fixture.Database.ConnectionString).Options);
        var rollback = await Assert.ThrowsAsync<PostgresException>(() => maintenance.GetService<IMigrator>()
            .MigrateAsync("20261103102951_TestDriveStaffCancellations"));
        Assert.Contains("Retained reservation cancellations", rollback.MessageText);
        return results[0];

        async Task<ReservationResult> ReserveAsync()
        {
            using var scope = data.Factory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ReservationService>()
                .ReserveAsync(data.OwnerId, vehicleId, request, default);
        }

        DealershipDbContext NewContext() => new(new DbContextOptionsBuilder<DealershipDbContext>()
            .UseNpgsql(fixture.Database.RuntimeConnectionString).Options);
    }
}
