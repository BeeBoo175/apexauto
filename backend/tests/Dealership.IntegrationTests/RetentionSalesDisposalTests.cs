using Dealership.Application.Accounts;
using Dealership.Application.Vehicles;
using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static Dealership.IntegrationTests.ReservationTestData;
using static Dealership.IntegrationTests.RetentionMinimizationTests;

namespace Dealership.IntegrationTests;

// Its own class: the check-in scenario provisions a branch with a name that is unique per class database.
public sealed class RetentionSalesDisposalTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task ReservationBuyerCopiesAreRemovedOnlyFromAFinishedOldReservation()
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
        var request = new ReservationRequest(Guid.NewGuid(), listing.Publication.Id, null);
        await using (var command = NewContext())
        {
            var service = new ReservationService(new ReservationStore(command, new MailDeliverySignal(),
                new ReservationExpirySignal()));
            Assert.Equal(ReservationStatus.Succeeded, (await service.ReserveAsync(data.OwnerId,
                listing.Revision.VehicleId, request, CancellationToken.None)).Status);
        }

        // An active reservation never loses its copies, even to a direct statement.
        Assert.Equal(PostgresErrorCodes.CheckViolation, await FailureAsync(database,
            "UPDATE \"Reservations\" SET \"BuyerEmail\" = NULL"));
        Assert.Equal(ReservationStatus.Succeeded, (await services.GetRequiredService<ReservationCancellationService>()
            .CancelAsync(data.OwnerId, request.OperationId, new(Guid.NewGuid(), null), CancellationToken.None)).Status);
        await Worker(data.Factory).RunBatchAsync(CancellationToken.None);
        Assert.NotNull(await EmailAsync(database));

        // The owner role ages the reservation, because the runtime role cannot rewrite its lifetime.
        await using var owner = new NpgsqlConnection(fixture.Database.ConnectionString);
        await owner.OpenAsync();
        await AgeAsync(owner, "6 years");
        await Worker(data.Factory).RunBatchAsync(CancellationToken.None);
        Assert.NotNull(await EmailAsync(database));

        await AgeAsync(owner, "8 years");
        // Other columns of the finished reservation stay immutable for the runtime role.
        Assert.Equal(PostgresErrorCodes.CheckViolation, await FailureAsync(database,
            "UPDATE \"Reservations\" SET \"BuyerEmail\" = NULL, \"BuyerName\" = 'Someone Else'"));
        await Worker(data.Factory).RunBatchAsync(CancellationToken.None);
        Assert.Null(await EmailAsync(database));
        var stored = await database.Reservations.AsNoTracking().SingleAsync(item => item.Id == request.OperationId);
        Assert.Null(stored.Buyer.Mobile);
        Assert.Null(stored.Buyer.Nric);
        Assert.False(string.IsNullOrWhiteSpace(stored.Buyer.LegalName));
    }

    private static async Task AgeAsync(NpgsqlConnection owner, string age)
    {
        await ExecuteAsync(owner, "ALTER TABLE \"Reservations\" DISABLE TRIGGER reservation_update_guard");
        await ExecuteAsync(owner, "UPDATE \"Reservations\" SET " +
            $"\"CreatedAt\" = now() - interval '{age}' - interval '3 days', \"ExpiresAt\" = now() - interval '{age}' - interval '1 day'");
        await ExecuteAsync(owner, "ALTER TABLE \"Reservations\" ENABLE TRIGGER reservation_update_guard");
        // The release receipt is part of the chain anchor, so it ages with the reservation.
        await ExecuteAsync(owner, "ALTER TABLE \"ReservationReleases\" DISABLE TRIGGER USER");
        await ExecuteAsync(owner, $"UPDATE \"ReservationReleases\" SET \"RecordedAt\" = now() - interval '{age}'");
        await ExecuteAsync(owner, "ALTER TABLE \"ReservationReleases\" ENABLE TRIGGER USER");
        // The sale opened with the reservation records events too; they age with it.
        await ExecuteAsync(owner, "ALTER TABLE \"SalesOrderEvents\" DISABLE TRIGGER USER");
        await ExecuteAsync(owner, $"UPDATE \"SalesOrderEvents\" SET \"RecordedAt\" = now() - interval '{age}'");
        await ExecuteAsync(owner, "ALTER TABLE \"SalesOrderEvents\" ENABLE TRIGGER USER");
    }

    private static async Task<string?> EmailAsync(DealershipDbContext database)
    {
        return await database.Database.SqlQueryRaw<string?>("SELECT \"BuyerEmail\" AS \"Value\" FROM \"Reservations\"")
            .SingleAsync();
    }

    private DealershipDbContext NewContext() => new(new DbContextOptionsBuilder<DealershipDbContext>()
        .UseNpgsql(fixture.Database.RuntimeConnectionString).Options);
}
