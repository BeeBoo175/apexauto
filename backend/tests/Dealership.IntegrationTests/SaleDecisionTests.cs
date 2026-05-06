using Dealership.Application.Sales;
using Dealership.Application.Vehicles;
using Dealership.Domain.Sales;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Dealership.IntegrationTests;

public sealed class SaleDecisionTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task SubmissionApprovalRejectionAndCancellationFollowTheDiscountAuthority()
    {
        var world = await SaleWorld.SharedAsync(fixture);
        var held = await world.HoldAsync();
        await world.Handling.ClaimAsync(world.Agent, held.Id, new(Guid.NewGuid(), 1), default);
        await world.Terms.SaveTermsAsync(world.Agent, held.Id, new(Guid.NewGuid(), 2, null, 900m, null, 0m), default);

        var submitted = await world.Terms.SubmitAsync(world.Agent, held.Id, new(Guid.NewGuid(), 3), default);
        Assert.Equal("PendingApproval", submitted.Receipt!.Status);
        Assert.Equal(SaleStatus.Forbidden, (await world.Terms.ApproveAsync(world.Agent, held.Id,
            new(Guid.NewGuid(), 4), default)).Status);
        Assert.Equal(SaleStatus.Invalid, (await world.Terms.RejectAsync(world.Manager, held.Id,
            new(Guid.NewGuid(), 4, " "), default)).Status);
        var rejected = await world.Terms.RejectAsync(world.Manager, held.Id,
            new(Guid.NewGuid(), 4, "Margin too thin"), default);
        Assert.Equal("Handling", rejected.Receipt!.Status);

        var resubmitted = await world.Terms.SubmitAsync(world.Agent, held.Id, new(Guid.NewGuid(), 5), default);
        var approved = await world.Terms.ApproveAsync(world.Manager, held.Id, new(Guid.NewGuid(), 6), default);
        Assert.Equal(("PendingApproval", "Approved"), (resubmitted.Receipt!.Status, approved.Receipt!.Status));
        var number = (await world.Reads.GetAsync(world.Manager, held.Id, default)).Value!.Sale.OrderNumber;
        Assert.Matches("^VPO-[0-9]{4}-[0-9]{6}$", number);

        // An agent cannot cancel an approved sale; a manager can, which refunds the deposit and frees the car.
        Assert.Equal(SaleStatus.Forbidden, (await world.Terms.CancelAsync(world.Agent, held.Id,
            new(Guid.NewGuid(), 7, "Customer withdrew"), default)).Status);
        var cancel = new SaleReasonRequest(Guid.NewGuid(), 7, "Customer withdrew");
        Assert.Equal(SaleStatus.Succeeded, (await world.Terms.CancelAsync(world.Manager, held.Id, cancel, default)).Status);
        Assert.Equal(SaleStatus.Succeeded, (await world.Terms.CancelAsync(world.Manager, held.Id, cancel, default)).Status);

        await using var database = NewContext();
        var release = await database.ReservationReleases.AsNoTracking().SingleAsync(item => item.ReservationId == held.Id);
        var closing = await database.SalesOrderEvents.AsNoTracking()
            .SingleAsync(item => item.SalesOrderId == held.Id && item.Sequence == 8);
        Assert.Equal((ReservationReleaseKind.DealCancelled, 500m), (release.Kind, release.Refund));
        Assert.Equal((SalesOrderEventKind.Cancelled, release.Id), (closing.Kind, closing.ReleaseId));
        Assert.False((await database.Reservations.AsNoTracking().SingleAsync(item => item.Id == held.Id)).IsActive);
    }

    [Fact]
    public async Task OpenSalesExpireWithTheirRefundAndHandledSalesCannotBeCancelledOnline()
    {
        var world = await SaleWorld.SharedAsync(fixture);
        var expiring = await world.HoldAsync();
        var handled = await world.HoldAsync();
        await world.Handling.ClaimAsync(world.Agent, handled.Id, new(Guid.NewGuid(), 1), default);

        // The customer sees that an agent has the hold instead of a countdown.
        var account = (await world.Services.GetRequiredService<IReservationReadStore>().AccountAsync(handled.CustomerId, 1,
            default)).History!.Items.Single(item => item.Receipt.Id == handled.Id);
        Assert.Equal((true, null), (account.IsHandled, account.DueAt));

        var cancellations = world.Services.GetRequiredService<ReservationCancellationService>();
        Assert.Equal(ReservationStatus.Conflict, (await cancellations.CancelAsync(handled.CustomerId, handled.Id,
            new(Guid.NewGuid(), null), default)).Status);

        // Maintenance alone moves the open sale's deadline into the past; reads then expire it in the database.
        await using (var maintenance = new NpgsqlConnection(fixture.Database.ConnectionString))
        {
            await maintenance.OpenAsync();
            await using var command = new NpgsqlCommand("""
                SET session_replication_role = replica;
                UPDATE "SalesOrders" SET "DueAt" = clock_timestamp() - interval '1 minute' WHERE "Id" = @id;
                SET session_replication_role = DEFAULT;
                """, maintenance);
            command.Parameters.AddWithValue("id", expiring.Id);
            await command.ExecuteNonQueryAsync();
        }

        var table = (await world.Lists.TableAsync(world.Manager,
            new SaleTableQuery(1, 50, "History", null, null, false), default)).Value!;
        var row = Assert.Single(table.Items, item => item.Id == expiring.Id);
        Assert.Equal(("Expired", "Expired"), (row.Status, row.Outcome));
        var history = (await world.Reads.HistoryAsync(world.Manager, expiring.Id, 1, 10, default)).Value!;
        Assert.Equal(("Expired", "System", 500m), (history.Items[0].Kind, history.Items[0].Actor,
            history.Items[0].Refund));
        var version = (await world.Reads.VersionAsync(world.Manager, expiring.Id, 2, default)).Value!;
        Assert.Equal(SalesOrderStatus.Expired, version.Snapshot.Status);
    }

    private DealershipDbContext NewContext() => new(new DbContextOptionsBuilder<DealershipDbContext>()
        .UseNpgsql(fixture.Database.RuntimeConnectionString).Options);
}
