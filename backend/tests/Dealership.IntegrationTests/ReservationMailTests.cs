using Dealership.Application.Accounts;
using Dealership.Application.Vehicles;
using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static Dealership.IntegrationTests.ReservationTestData;

namespace Dealership.IntegrationTests;

public sealed class ReservationMailTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task ConfirmationAndIntentCommitTogetherAndDeliveryRetainsOriginalTermsAcrossRestart()
    {
        using var data = await CheckInTestData.CreateAsync(fixture);
        using var scope = data.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var manager = await ProvisionAsync(services, data.BranchId, AccountRoles.BranchManager);
        var technician = await ProvisionAsync(services, data.BranchId, AccountRoles.WorkshopTechnician);
        var listing = await CreateListingAsync(data, services, manager, technician, 0);
        await using var database = NewContext();
        database.PurchaseSettings.Add(new(500m, 0.08m, manager, DateTimeOffset.UtcNow));
        await database.SaveChangesAsync();
        var signal = new MailDeliverySignal();
        var request = new ReservationRequest(Guid.NewGuid(), listing.Publication.Id, null);

        await AssertAtomicFailureAsync(data, listing.Revision.VehicleId, request, signal);
        await using (var command = NewContext())
        {
            var service = new ReservationService(new ReservationStore(command, signal));
            Assert.Equal(ReservationStatus.Succeeded, (await service.ReserveAsync(data.OwnerId,
                listing.Revision.VehicleId, request, CancellationToken.None)).Status);
        }

        await signal.WaitAsync(CancellationToken.None).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        var intent = await database.ReservationMailOutbox.AsNoTracking().SingleAsync();
        Assert.Equal(request.OperationId, intent.ReservationId);
        Assert.Null(intent.DeliveredAt);
        Assert.Equal(0, intent.DeliveryAttempts);
        var reservation = await database.Reservations.AsNoTracking().SingleAsync();
        var expected = ReservationMailComposer.Confirmation(reservation);
        Assert.Equal(data.CustomerEmail, expected.Recipient);
        Assert.Contains("RM 60630.00", expected.Body);
        Assert.Contains("RM 500.00", expected.Body);
        Assert.Contains("Asia/Kuala_Lumpur", expected.Body);
        Assert.DoesNotContain(reservation.Buyer.Nric, expected.Body);

        await using (var restarted = NewContext())
        {
            var service = new ReservationService(new ReservationStore(restarted, signal));
            Assert.Equal(ReservationStatus.Succeeded, (await service.ReserveAsync(data.OwnerId,
                listing.Revision.VehicleId, request, CancellationToken.None)).Status);
            Assert.Equal(ReservationStatus.Conflict, (await service.ReserveAsync(data.OwnerId,
                listing.Revision.VehicleId, request with { OperationId = Guid.NewGuid() }, CancellationToken.None)).Status);
        }

        Assert.Equal(1, await database.ReservationMailOutbox.CountAsync());
        await AssertSqlProtectionAsync(database, intent);
        Assert.Equal(ReservationStatus.Succeeded, (await services.GetRequiredService<ReservationCancellationService>()
            .CancelAsync(data.OwnerId, reservation.Id, new(Guid.NewGuid(), null), CancellationToken.None)).Status);
        await database.Users.Where(item => item.Id == data.OwnerId).ExecuteUpdateAsync(update => update
            .SetProperty(item => item.Email, "changed@example.invalid")
            .SetProperty(item => item.NormalizedEmail, "CHANGED@EXAMPLE.INVALID")
            .SetProperty(item => item.UserName, "changed@example.invalid")
            .SetProperty(item => item.NormalizedUserName, "CHANGED@EXAMPLE.INVALID"));
        await database.PurchaseSettings.ExecuteUpdateAsync(update => update.SetProperty(item => item.ProcessingFee, 700m));

        var clock = new DeliveryClock(reservation.CreatedAt.AddSeconds(1));
        var claims = await Task.WhenAll(ClaimAsync(clock), ClaimAsync(clock));
        var original = Assert.Single(claims, item => item is not null)!;
        Assert.Single(claims, item => item is null);
        Assert.Equal(expected, original.Message);
        Assert.Equal(intent.Id, original.Id);
        clock.Now += TimeSpan.FromSeconds(61);
        var recovered = Assert.IsType<ClaimedMail>(await ClaimAsync(clock));
        Assert.NotEqual(original.ClaimId, recovered.ClaimId);
        Assert.Equal(expected, recovered.Message);
        await AcknowledgeAsync(clock, original);
        Assert.Null(await database.ReservationMailOutbox.Select(item => item.DeliveredAt).SingleAsync());
        await AcknowledgeAsync(clock, recovered);
        Assert.NotNull(await database.ReservationMailOutbox.Select(item => item.DeliveredAt).SingleAsync());
        Assert.Null(await ClaimAsync(clock));
        Assert.Contains(services.GetServices<IMailDeliverySource>(), source => source is ReservationMailSource);
    }

    private async Task AssertAtomicFailureAsync(CheckInTestData data, Guid vehicleId, ReservationRequest request,
        MailDeliverySignal signal)
    {
        await using var maintenance = new DealershipDbContext(new DbContextOptionsBuilder<DealershipDbContext>()
            .UseNpgsql(fixture.Database.ConnectionString).Options);
        await maintenance.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION reject_reservation_mail() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                RAISE EXCEPTION 'Intent unavailable';
            END $$;
            CREATE TRIGGER reject_reservation_mail BEFORE INSERT ON "ReservationMailOutbox"
                FOR EACH ROW EXECUTE FUNCTION reject_reservation_mail();
            """);
        try
        {
            await using (var failed = NewContext())
            {
                var service = new ReservationService(new ReservationStore(failed, signal));
                await Assert.ThrowsAsync<DbUpdateException>(() => service.ReserveAsync(data.OwnerId,
                    vehicleId, request, CancellationToken.None));
            }

            Assert.False(await maintenance.Reservations.AnyAsync());
            Assert.False(await maintenance.ReservationMailOutbox.AnyAsync());
            using var cancellation = new CancellationTokenSource();
            var waiting = signal.WaitAsync(cancellation.Token).AsTask();
            Assert.False(waiting.IsCompleted);
            await cancellation.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        }
        finally
        {
            await maintenance.Database.ExecuteSqlRawAsync("""
                DROP TRIGGER reject_reservation_mail ON "ReservationMailOutbox";
                DROP FUNCTION reject_reservation_mail();
                """);
        }
    }

    private static async Task AssertSqlProtectionAsync(DealershipDbContext database, ReservationMail intent)
    {
        var duplicate = await Assert.ThrowsAsync<PostgresException>(() => database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "ReservationMailOutbox" ("Id", "ReservationId", "DeliveryAttempts", "NextDeliveryAt")
            VALUES ({Guid.NewGuid()}, {intent.ReservationId}, 0, clock_timestamp())
            """));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, duplicate.SqlState);
        var orphan = await Assert.ThrowsAsync<PostgresException>(() => database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "ReservationMailOutbox" ("Id", "ReservationId", "DeliveryAttempts", "NextDeliveryAt")
            VALUES ({Guid.NewGuid()}, {Guid.NewGuid()}, 0, clock_timestamp())
            """));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, orphan.SqlState);
    }

    private async Task<ClaimedMail?> ClaimAsync(TimeProvider clock)
    {
        await using var database = NewContext();
        return await new ReservationMailSource(database, clock).ClaimNextAsync(CancellationToken.None);
    }

    private async Task AcknowledgeAsync(TimeProvider clock, ClaimedMail message)
    {
        await using var database = NewContext();
        await new ReservationMailSource(database, clock).MarkDeliveredAsync(message.Id, message.ClaimId, CancellationToken.None);
    }

    private DealershipDbContext NewContext() => new(new DbContextOptionsBuilder<DealershipDbContext>()
        .UseNpgsql(fixture.Database.RuntimeConnectionString).Options);

    private sealed class DeliveryClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
