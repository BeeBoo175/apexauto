using Dealership.Application.Accounts;
using Dealership.Application.Vehicles;
using Dealership.Infrastructure.Inspections;
using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Sales;
using Dealership.Infrastructure.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static Dealership.IntegrationTests.ReservationTestData;

namespace Dealership.IntegrationTests;

internal static class TestDriveWriterLockAssertions
{
    internal static async Task VerifyAsync(CheckInTestData data, IntakePhotoFixture fixture,
        ListingRecord listing, Guid manager, Guid technician)
    {
        using var scope = data.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var mail = services.GetRequiredService<MailDeliverySignal>();
        var expiry = services.GetRequiredService<ReservationExpirySignal>();
        var vehicleId = listing.Revision.VehicleId;
        var reservationRequest = new ReservationRequest(Guid.NewGuid(), listing.Publication.Id, null);

        await VerifyWriterAsync(async (database, token) => await new ReservationStore(database, mail, expiry)
            .LockAsync(data.OwnerId, vehicleId, reservationRequest, token));
        await VerifyWriterAsync(async (database, token) => await new ListingStore(database)
            .LockAsync(manager, vehicleId, Guid.NewGuid(), token));
        await VerifyWriterAsync(async (database, token) => await new InspectionAssignmentStore(database)
            .LockAsync(technician, vehicleId, new(Guid.NewGuid(), technician, 0, null), token));
        await VerifyWriterAsync(async (database, token) => await new InspectionReopeningStore(database)
            .LockAsync(manager, listing.InspectionId, Guid.NewGuid(), token));

        await using var setup = NewContext();
        setup.PurchaseSettings.Add(new(500m, 0.08m, manager, DateTimeOffset.UtcNow));
        await setup.SaveChangesAsync();
        var reserved = await services.GetRequiredService<ReservationService>()
            .ReserveAsync(data.OwnerId, vehicleId, reservationRequest, default);
        Assert.Equal(ReservationStatus.Succeeded, reserved.Status);
        var group = await ProvisionAsync(services, null, AccountRoles.GeneralManager);
        foreach (var actor in new[] { manager, group })
        {
            await VerifyWriterAsync(async (database, token) => await new SaleCommandStore(database, mail)
                .LockAsync(actor, reserved.Reservation!.Id, Guid.NewGuid(), token));
        }

        async Task VerifyWriterAsync(Func<DealershipDbContext, CancellationToken, Task<IAsyncDisposable>> acquire)
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var token = deadline.Token;
            await using var blocker = NewContext();
            await using var blockingTransaction = await blocker.Database.BeginTransactionAsync(token);
            await blocker.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Vehicles\" WHERE \"Id\" = {vehicleId} FOR UPDATE", token);
            var blockerId = ((NpgsqlConnection)blocker.Database.GetDbConnection()).ProcessID;
            await using var writer = NewContext();
            await writer.Database.OpenConnectionAsync(token);
            var writerId = ((NpgsqlConnection)writer.Database.GetDbConnection()).ProcessID;
            var pending = acquire(writer, token);
            try
            {
                await using var observer = NewContext();
                while (!await observer.Database.SqlQuery<int>($"""
                    SELECT count(*)::integer AS "Value" FROM unnest(pg_blocking_pids({writerId})) AS pid
                    WHERE pid = {blockerId}
                    """).AnyAsync(count => count > 0, token))
                {
                    Assert.False(pending.IsCompleted, "Writer must wait for the occupied vehicle.");
                    await Task.Delay(20, token);
                }

                // While waiting for the vehicle, the writer must already exclude branch SHARE holders.
                var conflict = await Assert.ThrowsAsync<PostgresException>(() => observer.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT 1 FROM \"Branches\" WHERE \"Id\" = {data.BranchId} FOR SHARE NOWAIT", token));
                Assert.Equal(PostgresErrorCodes.LockNotAvailable, conflict.SqlState);
            }
            finally
            {
                await blockingTransaction.RollbackAsync(CancellationToken.None);
                await using var acquired = await pending;
            }
        }

        DealershipDbContext NewContext() => new(new DbContextOptionsBuilder<DealershipDbContext>()
            .UseNpgsql(fixture.Database.RuntimeConnectionString).Options);
    }
}
