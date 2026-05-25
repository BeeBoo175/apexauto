using Dealership.Application.Intake;
using Dealership.Domain.Common;
using Dealership.Domain.Intake;
using Dealership.Infrastructure.Intake;
using Dealership.Infrastructure.Photos;
using Dealership.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Dealership.IntegrationTests;

public sealed class StaffIntakeRaceTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task OrderedContendersCancelWaitersAndRetainCheckedInPhotosAcrossCleanupAndRestart()
    {
        var schedule = new InspectionSchedule(TimeOnly.MinValue, new TimeOnly(23, 59), 1, 30, 0,
            Enum.GetValues<DayOfWeek>());
        var malaysia = TimeZoneInfo.FindSystemTimeZoneById(DealershipLocale.MalaysiaTimeZoneId);
        var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, malaysia);
        // The booking helper's clock is midnight; expiry must still be in the real database's future at attachment.
        var lifetime = now.TimeOfDay + TimeSpan.FromSeconds(20);
        using var data = await CheckInTestData.CreateAsync(fixture, schedule, lifetime.TotalHours);
        using var factory = fixture.CreateFactory();
        var target = now.AddMinutes(10);
        var dateOffset = target.Date == now.Date && target.Hour * 60 + target.Minute < 1437 ? 0 : 1;
        var slot = dateOffset == 0 ? target.Hour * 60 + target.Minute : 10;
        var checkInFirst = await data.BookAsync(slot, "VCA 6271", dateOffset);
        var cancelFirst = await data.BookAsync(slot + 1, "VCA 6272", dateOffset);
        await data.ClaimAsync(checkInFirst.Id);
        await data.ClaimAsync(cancelFirst.Id);
        await AssertOrderedRaceAsync(factory, data, checkInFirst.Id, checkInFirst: true, sameDay: dateOffset == 0);
        await AssertOrderedRaceAsync(factory, data, cancelFirst.Id, checkInFirst: false, sameDay: dateOffset == 0);

        var retained = await data.BookAsync(1, "VCA 6273");
        await data.ClaimAsync(retained.Id);
        await AssertCancelledWaiterAsync(factory, data, retained.Id);
        var evidence = CheckInTestData.Evidence with { Vin = "PL1BT65R84B123458" };
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var photoIds = await database.AppointmentPhotos.Where(link => link.AppointmentId == retained.Id)
            .OrderBy(link => link.PhotoId).Select(link => link.PhotoId).ToArrayAsync();
        await using (var blocker = new NpgsqlConnection(fixture.Database.RuntimeConnectionString))
        {
            await blocker.OpenAsync();
            await using var transaction = await blocker.BeginTransactionAsync();
            await using var hold = new NpgsqlCommand("SELECT 1 FROM \"IntakePhotos\" WHERE \"Id\" = @id FOR UPDATE", blocker, transaction);
            hold.Parameters.AddWithValue("id", photoIds[0]);
            await hold.ExecuteScalarAsync();
            var checkIn = CheckInAsync(factory, data, retained.Id, evidence);
            await WaitForWaitersAsync(1);
            var cleanup = ClaimAsync(factory, photoIds[0]);
            await WaitForWaitersAsync(2);
            await transaction.CommitAsync();
            Assert.Equal(CheckInStatus.Succeeded, (await checkIn).Status);
            Assert.Null(await cleanup);
        }

        var expires = await database.IntakePhotos.Where(photo => photoIds.Contains(photo.Id)).MaxAsync(photo => photo.ExpiresAt);
        var delay = expires - DateTimeOffset.UtcNow + TimeSpan.FromMilliseconds(25);

        if (delay > TimeSpan.Zero)
        {
            Assert.True(delay < TimeSpan.FromSeconds(21));
            await Task.Delay(delay);
        }

        using var restarted = fixture.CreateFactory();
        await restarted.Services.GetServices<IHostedService>().OfType<PhotoCleanupWorker>().Single()
            .RunBatchAsync(CancellationToken.None);
        var replay = await CheckInAsync(restarted, data, retained.Id, evidence);
        Assert.Equal(CheckInStatus.Succeeded, replay.Status);
        Assert.Equal(evidence, replay.Receipt!.Evidence);
        Assert.False(await database.PhotoRemovals.AnyAsync(removal => photoIds.Contains(removal.PhotoId)));

        foreach (var id in photoIds)
        {
            Assert.Null(await ClaimAsync(restarted, id));
            Assert.True(await fixture.OwnedObjectExistsAsync(id));
            Assert.Equal(PhotoStatus.Available,
                (await scope.ServiceProvider.GetRequiredService<IntakePhotoService>()
                    .GetReceiptAsync(data.OwnerId, id, CancellationToken.None)).Status);
        }
    }

    private async Task AssertOrderedRaceAsync(WebApplicationFactory<Program> factory, CheckInTestData data,
        Guid appointmentId, bool checkInFirst, bool sameDay)
    {
        await using var blocker = new NpgsqlConnection(fixture.Database.RuntimeConnectionString);
        await blocker.OpenAsync();
        await using var transaction = await blocker.BeginTransactionAsync();
        await HoldAppointmentAsync(blocker, transaction, appointmentId);
        Task<CheckInResult> checkIn;
        Task<CancellationResult> cancellation;

        if (checkInFirst)
        {
            checkIn = CheckInAsync(factory, data, appointmentId, CheckInTestData.Evidence);
            await WaitForWaitersAsync(1);
            cancellation = CancelAsync(factory, data, appointmentId);
        }
        else
        {
            cancellation = CancelAsync(factory, data, appointmentId);
            await WaitForWaitersAsync(1);
            checkIn = CheckInAsync(factory, data, appointmentId, CheckInTestData.Evidence);
        }

        await WaitForWaitersAsync(2);
        await transaction.CommitAsync();
        var checkedIn = await checkIn;
        var cancelled = await cancellation;
        Assert.Equal(checkInFirst && sameDay ? CheckInStatus.Succeeded : CheckInStatus.Unavailable, checkedIn.Status);
        Assert.Equal(checkInFirst && sameDay ? CancellationStatus.CheckedIn : CancellationStatus.Succeeded, cancelled.Status);
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        Assert.Equal(checkInFirst && sameDay,
            await database.Vehicles.AnyAsync(vehicle => vehicle.IntakeAppointmentId == appointmentId));
        Assert.Equal(checkInFirst && sameDay,
            await database.AppointmentMailOutbox.AnyAsync(item => item.Kind == AppointmentMailKind.CheckIn && item.AppointmentId == appointmentId));
        Assert.Equal(!(checkInFirst && sameDay),
            await database.IntakeAppointments.AnyAsync(item => item.Id == appointmentId && item.CancelledAt != null));
    }

    private async Task AssertCancelledWaiterAsync(WebApplicationFactory<Program> factory, CheckInTestData data, Guid id)
    {
        await using var blocker = new NpgsqlConnection(fixture.Database.RuntimeConnectionString);
        await blocker.OpenAsync();
        await using var transaction = await blocker.BeginTransactionAsync();
        await HoldAppointmentAsync(blocker, transaction, id);
        using var cancellation = new CancellationTokenSource();
        var waiting = CheckInAsync(factory, data, id, CheckInTestData.Evidence, cancellation.Token);
        await WaitForWaitersAsync(1);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        await transaction.CommitAsync();
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        Assert.False(await database.Vehicles.AnyAsync(vehicle => vehicle.IntakeAppointmentId == id));
        Assert.False(await database.AppointmentCheckIns.AnyAsync(item => item.AppointmentId == id));
        Assert.False(await database.AppointmentMailOutbox.AnyAsync(item => item.Kind == AppointmentMailKind.CheckIn && item.AppointmentId == id));
    }

    private static async Task HoldAppointmentAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid id)
    {
        await using var command = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended(@key, 0))", connection, transaction);
        command.Parameters.AddWithValue("key", "appointment:" + id);
        await command.ExecuteNonQueryAsync();
    }

    private async Task WaitForWaitersAsync(int expected)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var observer = new NpgsqlConnection(fixture.Database.RuntimeConnectionString);
        await observer.OpenAsync(deadline.Token);
        await using var command = new NpgsqlCommand("""
            SELECT count(*) FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock'
            """, observer);

        while ((long)(await command.ExecuteScalarAsync(deadline.Token))! < expected)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(20), deadline.Token);
        }
    }

    private static async Task<PhotoDeletionClaim?> ClaimAsync(WebApplicationFactory<Program> factory, Guid id)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<PhotoCleanupStore>().ClaimAsync(id, CancellationToken.None);
    }

    private static async Task<CheckInResult> CheckInAsync(WebApplicationFactory<Program> factory, CheckInTestData data,
        Guid id, CheckInEvidence evidence, CancellationToken cancellationToken = default)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppointmentCheckInService>()
            .CheckInAsync(data.StaffId, id, evidence, cancellationToken);
    }

    private static async Task<CancellationResult> CancelAsync(WebApplicationFactory<Program> factory, CheckInTestData data, Guid id)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppointmentCancellationService>()
            .CancelAsync(data.OwnerId, id, CancellationToken.None);
    }
}
