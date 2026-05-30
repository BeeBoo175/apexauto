using System.Text.Json;
using Dealership.Application.TestDrives;
using Dealership.Domain.TestDrives;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.TestDrives;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Dealership.IntegrationTests;

internal static class TestDriveCancellationAssertions
{
    internal static async Task VerifyAsync(WebApplicationFactory<Program> factory, IntakePhotoFixture fixture,
        DealershipDbContext database, TestDriveBooking booking, TestDriveBookingRequest request, Guid other, Guid staff)
    {
        var operationId = Guid.NewGuid();
        Assert.Equal(TestDriveCancellationStatus.Invalid, (await CancelAsync(booking.CustomerId, booking.Id, Guid.Empty)).Status);
        Assert.Equal(TestDriveCancellationStatus.Forbidden, (await CancelAsync(staff, booking.Id, operationId)).Status);
        Assert.Equal(TestDriveCancellationStatus.NotFound, (await CancelAsync(other, booking.Id, operationId)).Status);
        await database.Users.Where(item => item.Id == booking.CustomerId)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.IsActive, false));
        Assert.Equal(TestDriveCancellationStatus.Forbidden, (await CancelAsync(booking.CustomerId, booking.Id, operationId)).Status);
        await database.Users.Where(item => item.Id == booking.CustomerId)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.IsActive, true).SetProperty(item => item.EmailConfirmed, false));

        var missingMail = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(Guid.NewGuid(), booking.CustomerId, DateTimeOffset.UtcNow));
        Assert.Equal("CK_TestDriveCancellations_Mail", missingMail.ConstraintName);
        Assert.False(await database.TestDriveCancellations.AnyAsync());
        var orphanMail = await Assert.ThrowsAsync<PostgresException>(() => database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "TestDriveMailOutbox" ("Id", "BookingId", "Kind", "RecipientEmail", "DeliveryAttempts", "NextDeliveryAt")
            SELECT {Guid.NewGuid()}, "BookingId", 'Cancellation', "RecipientEmail", 0, clock_timestamp()
            FROM "TestDriveMailOutbox" WHERE "BookingId" = {booking.Id}
            """));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, orphanMail.SqlState);
        var foreignOwner = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(Guid.NewGuid(), other, DateTimeOffset.UtcNow));
        Assert.Equal("CK_TestDriveCancellations_Owner", foreignOwner.ConstraintName);
        var late = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(Guid.NewGuid(), booking.CustomerId, booking.StartsAt));
        Assert.Equal("CK_TestDriveCancellations_Time", late.ConstraintName);

        // An unverified customer can still cancel their retained booking.
        var repeats = await Task.WhenAll(CancelAsync(booking.CustomerId, booking.Id, operationId),
            CancelAsync(booking.CustomerId, booking.Id, operationId));
        Assert.All(repeats, result => Assert.Equal(TestDriveCancellationStatus.Succeeded, result.Status));
        var receipt = repeats[0].Cancellation!;
        Assert.Equal(JsonSerializer.Serialize(receipt), JsonSerializer.Serialize(repeats[1].Cancellation));
        Assert.Single(await database.TestDriveCancellations.ToArrayAsync());
        Assert.Equal(TestDriveCancellationStatus.Conflict, (await CancelAsync(other, booking.Id, operationId)).Status);
        Assert.Equal(TestDriveCancellationStatus.Conflict, (await CancelAsync(booking.CustomerId, Guid.NewGuid(), operationId)).Status);
        Assert.Equal(TestDriveCancellationStatus.Conflict, (await CancelAsync(booking.CustomerId, booking.Id, Guid.NewGuid())).Status);
        var immutable = await Assert.ThrowsAsync<PostgresException>(() => database.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"TestDriveCancellations\" SET \"RecordedAt\" = \"RecordedAt\" WHERE \"Id\" = {operationId}"));
        Assert.Equal("CK_TestDriveCancellations_Immutable", immutable.ConstraintName);
        var cancellationMail = await database.TestDriveMailOutbox.AsNoTracking()
            .SingleAsync(item => item.BookingId == booking.Id && item.Kind == TestDriveMailKind.Cancellation);
        Assert.Equal(booking.Id, cancellationMail.CancellationBookingId);
        var changedKind = await Assert.ThrowsAsync<PostgresException>(() => database.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"TestDriveMailOutbox\" SET \"Kind\" = 'Confirmation' WHERE \"Id\" = {cancellationMail.Id}"));
        Assert.Equal("CK_TestDriveMailOutbox_Immutable", changedKind.ConstraintName);

        var source = new TestDriveMailSource(database, TimeProvider.System);
        var messages = new List<Guid>();
        while (await source.ClaimNextAsync(default) is { } claimed)
        {
            messages.Add(claimed.Id);
            if (claimed.Id == cancellationMail.Id)
            {
                Assert.Contains("cancelled", claimed.Message.Subject);
                await source.MarkDeliveredAsync(claimed.Id, Guid.NewGuid(), default);
                Assert.Null(await database.TestDriveMailOutbox.Where(item => item.Id == claimed.Id)
                    .Select(item => item.DeliveredAt).SingleAsync());
            }

            await source.MarkDeliveredAsync(claimed.Id, claimed.ClaimId, default);
        }

        Assert.Contains(cancellationMail.Id, messages);
        using var restarted = fixture.CreateFactory();
        using var restartedScope = restarted.Services.CreateScope();
        var replay = await restartedScope.ServiceProvider.GetRequiredService<TestDriveCancellationService>()
            .CancelAsync(booking.CustomerId, booking.Id, operationId, default);
        Assert.Equal(TestDriveCancellationStatus.Succeeded, replay.Status);
        Assert.Equal(JsonSerializer.Serialize(receipt), JsonSerializer.Serialize(replay.Cancellation));
        await database.Users.Where(item => item.Id == booking.CustomerId)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.EmailConfirmed, true));
        using var bookingScope = factory.Services.CreateScope();
        var replacement = await bookingScope.ServiceProvider.GetRequiredService<TestDriveBookingService>()
            .BookAsync(booking.CustomerId, booking.VehicleId, request with { OperationId = Guid.NewGuid() }, default);
        Assert.Equal(TestDriveBookingStatus.Succeeded, replacement.Status);
        Assert.Equal(2, await database.TestDriveMailOutbox.CountAsync(item => item.BookingId == booking.Id));
        Assert.Equal(TestDriveCancellationStatus.Succeeded,
            (await CancelAsync(booking.CustomerId, replacement.Booking!.Id, Guid.NewGuid())).Status);
        var cancellationOnly = await source.ClaimNextAsync(default);
        Assert.NotNull(cancellationOnly);
        Assert.Contains("cancelled", cancellationOnly.Message.Subject);
        await source.MarkDeliveredAsync(cancellationOnly.Id, cancellationOnly.ClaimId, default);
        Assert.Null(await source.ClaimNextAsync(default));

        var staffBooking = await bookingScope.ServiceProvider.GetRequiredService<TestDriveBookingService>()
            .BookAsync(booking.CustomerId, booking.VehicleId, request with { OperationId = Guid.NewGuid() }, default);
        Assert.Equal(TestDriveBookingStatus.Succeeded, staffBooking.Status);
        await TestDriveStaffCancellationAssertions.VerifyAsync(factory, fixture, database, staffBooking.Booking!, staff);

        async Task<TestDriveCancellationResult> CancelAsync(Guid customer, Guid target, Guid operation)
        {
            using var scope = factory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<TestDriveCancellationService>()
                .CancelAsync(customer, target, operation, default);
        }

        Task<int> InsertAsync(Guid operation, Guid customer, DateTimeOffset recordedAt)
        {
            return database.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "TestDriveCancellations" ("Id", "BookingId", "CustomerId", "Kind", "RecordedAt")
                VALUES ({operation}, {booking.Id}, {customer}, 'CustomerCancelled', {recordedAt})
                """);
        }
    }
}
