using Dealership.Application.Accounts;
using Dealership.Application.Sales;
using Dealership.Domain.Sales;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static Dealership.IntegrationTests.RetentionMinimizationTests;
using static Dealership.IntegrationTests.RetentionSql;

namespace Dealership.IntegrationTests;

// Its own class: the shared sale world provisions one branch per class database.
public sealed class RetentionSaleDocumentsTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    private const string AgeColumns = "\"CreatedAt\" = \"CreatedAt\" - interval '{0}', \"UpdatedAt\" = \"UpdatedAt\" - interval '{0}', " +
        "\"ClaimedAt\" = \"ClaimedAt\" - interval '{0}', \"DueAt\" = \"DueAt\" - interval '{0}', " +
        "\"SubmittedAt\" = \"SubmittedAt\" - interval '{0}', \"DecidedAt\" = \"DecidedAt\" - interval '{0}', " +
        "\"ClosedAt\" = \"ClosedAt\" - interval '{0}', \"SettledAt\" = \"SettledAt\" - interval '{0}', " +
        "\"DeliveredAt\" = \"DeliveredAt\" - interval '{0}'";

    [Fact]
    public async Task BuyerDetailsLeaveADeliveredSaleOnlyAfterTheDeadlineWithoutAHoldAndNeverFromASettledSale()
    {
        var world = await SaleWorld.SharedAsync(fixture);
        var settled = await PrepareAsync(world, deliver: false);
        var delivered = await PrepareAsync(world, deliver: true);
        var corrections = world.Services.GetRequiredService<SaleDocumentCorrectionService>();
        var reissue = new SaleDocumentCorrectionRequest(Guid.NewGuid(), 5, delivered.Original.Id,
            new(delivered.BuyerName, "27 Jalan Meranti, Petaling Jaya, Selangor", "Correct buyer address spelling"));
        var corrected = await corrections.ReissueAsync(world.Manager, delivered.SaleId, reissue, default);
        Assert.Equal(SaleResultStatus.Succeeded, corrected.Status);

        var administrator = await ReservationTestData.ProvisionAsync(world.Services, null, AccountRoles.Administrator);
        var holds = world.Services.GetRequiredService<SaleRetentionHoldService>();
        var until = DateTimeOffset.UtcNow.AddYears(1);
        await using var database = NewContext();
        var number = await database.Reservations.AsNoTracking().Where(item => item.Id == delivered.SaleId)
            .Select(item => item.Number).SingleAsync();
        Assert.Equal(SaleRetentionHoldStatus.Forbidden, (await holds.SetByNumberAsync(world.Manager,
            new(number, until, "Dispute under review"), default)).Status);
        foreach (var invalid in new SaleRetentionHoldRequest[]
        {
            new(DateTimeOffset.UtcNow.AddDays(-1), "Dispute under review"),
            new(DateTimeOffset.UtcNow.AddYears(11), "Dispute under review"),
            new(until, " ")
        })
        {
            Assert.Equal(SaleRetentionHoldStatus.Invalid, (await holds.SetByNumberAsync(administrator,
                new(number, invalid.Until, invalid.Reason), default)).Status);
        }

        // A hold changes only its own columns of a Delivered sale: the version stays, any other change still raises.
        var before = await database.SalesOrders.AsNoTracking().SingleAsync(item => item.Id == delivered.SaleId);
        var placed = await holds.SetByNumberAsync(administrator, new(number, until, "Dispute under review"), default);
        Assert.Equal(SaleRetentionHoldStatus.Succeeded, placed.Status);
        var held = await database.SalesOrders.AsNoTracking().SingleAsync(item => item.Id == delivered.SaleId);
        Assert.Equal(before.Version, held.Version);
        Assert.Equal(before.UpdatedAt, held.UpdatedAt);
        Assert.Equal(administrator, held.RetentionHoldById);
        Assert.Equal("CK_SalesOrders_DeliveredImmutable", await ConstraintAsync(
            "UPDATE \"SalesOrders\" SET \"ClaimedAt\" = \"ClaimedAt\" + interval '1 second' WHERE \"Id\" = '" +
            delivered.SaleId + "'"));

        // The Administrator finds a hold by the reservation number and sees it in the list.
        var listed = await holds.ListAsync(administrator, default);
        Assert.Equal(number, Assert.Single(listed.Holds!, item => item.SaleId == delivered.SaleId).ReservationNumber);
        Assert.Equal(listed.Holds!.Count, listed.Total);
        Assert.Equal(SaleRetentionHoldStatus.Forbidden, (await holds.ListAsync(world.Manager, default)).Status);
        Assert.Equal(SaleRetentionHoldStatus.NotFound, (await holds.SetByNumberAsync(administrator,
            new("RSV-0000-000000", until, "Dispute under review"), default)).Status);

        await using var owner = new NpgsqlConnection(fixture.Database.ConnectionString);
        await owner.OpenAsync();
        await AgeAsync(owner, "6 years");
        await Worker(fixture.CreateFactory()).RunBatchAsync(CancellationToken.None);
        Assert.False(await RemovedAsync(database, delivered.SaleId));

        await AgeAsync(owner, "2 years");
        // Eight years have passed, but the hold still keeps every copy.
        await Worker(fixture.CreateFactory()).RunBatchAsync(CancellationToken.None);
        Assert.False(await RemovedAsync(database, delivered.SaleId));

        Assert.Equal(SaleRetentionHoldStatus.Succeeded, (await holds.SetByNumberAsync(administrator,
            new(number, null, null), default)).Status);
        Assert.DoesNotContain((await holds.ListAsync(administrator, default)).Holds!,
            item => item.SaleId == delivered.SaleId);
        await Worker(fixture.CreateFactory()).RunBatchAsync(CancellationToken.None);
        Assert.True(await RemovedAsync(database, delivered.SaleId));

        // Vehicle facts, terms and payment evidence stay; names, NRIC and address are gone from every copy.
        foreach (var document in await database.SaleDocuments.AsNoTracking()
            .Where(item => item.SalesOrderId == delivered.SaleId).ToListAsync())
        {
            var contents = document.Contents();
            Assert.True(contents.Review.BuyerDetailsRemoved);
            Assert.Equal(delivered.Original.Contents().Review.Vehicle, contents.Review.Vehicle);
            Assert.Equal(delivered.Original.Contents().Sale.PaymentEvidence, contents.Sale.PaymentEvidence);
        }

        var correction = await database.SaleDocumentCorrections.AsNoTracking().SingleAsync(item =>
            item.SalesOrderId == delivered.SaleId);
        Assert.NotNull(correction.MinimizedAt);
        Assert.Null(correction.BuyerName);
        Assert.Null(correction.BuyerAddress);
        Assert.Null(correction.Reason);
        var settledDocument = await database.SaleDocuments.AsNoTracking().FirstAsync(item =>
            item.SalesOrderId == settled.SaleId);
        Assert.False(settledDocument.Contents().Review.BuyerDetailsRemoved);

        // A correction on a removed document and a rendered document answer plainly, never with an exception.
        var leaf = await database.SaleDocuments.AsNoTracking().SingleAsync(item =>
            item.Id == corrected.Receipt!.ReplacementDocumentId);
        Assert.Equal(SaleResultStatus.DetailsRemoved, (await corrections.ReissueAsync(world.Manager, delivered.SaleId,
            reissue with { OperationId = Guid.NewGuid(), DocumentId = leaf.Id }, default)).Status);
        var rendered = await world.Services.GetRequiredService<SaleDocumentService>().ReadAsync(world.Manager,
            delivered.SaleId, leaf.Id, default);
        Assert.Equal(SaleResultStatus.Succeeded, rendered.Status);
        Assert.NotEmpty(rendered.Value!);

        // Everything else about removed records stays immutable for the runtime role.
        Assert.Equal(PostgresErrorCodes.CheckViolation, await StateAsync(
            "UPDATE \"SaleDocuments\" SET \"Number\" = 'VPO-2099-999998' WHERE \"Id\" = '" + leaf.Id + "'"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, await StateAsync(
            "UPDATE \"SalesOrderEvents\" SET \"RecordedAt\" = now() WHERE \"SalesOrderId\" = '" + delivered.SaleId + "'"));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, await StateAsync(
            "DELETE FROM \"SalesOrderEvents\" WHERE \"SalesOrderId\" = '" + delivered.SaleId + "'"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, await StateAsync(
            "UPDATE \"SaleDocumentCorrections\" SET \"Reason\" = 'Changed reason' WHERE \"Id\" = '" + reissue.OperationId + "'"));
    }

    private async Task<(Guid SaleId, SaleDocument Original, string BuyerName)> PrepareAsync(SaleWorld world, bool deliver)
    {
        var hold = await world.HoldAsync();
        Assert.Equal(SaleResultStatus.Succeeded, (await world.Handling.ClaimAsync(world.Agent, hold.Id, new(Guid.NewGuid(), 1), default)).Status);
        Assert.Equal(SaleResultStatus.Succeeded, (await world.Terms.SubmitAsync(world.Agent, hold.Id, new(Guid.NewGuid(), 2), default)).Status);
        await using var database = NewContext();
        var sale = await database.SalesOrders.AsNoTracking().SingleAsync(item => item.Id == hold.Id);
        var profile = await database.CustomerProfiles.SingleAsync(item => item.UserId == sale.CustomerId);
        var vehicle = await database.Vehicles.SingleAsync(item => item.Id == sale.VehicleId);
        var review = SaleSettlementReview.From(profile, SaleReviewedVehicle.Of(vehicle));
        var payment = new SalePaymentEvidence(SalePaymentMethod.Cash, sale.BalancePayable, "CASH 43879",
            new DateOnly(2026, 11, 2), null, 0m, null, false, false);
        Assert.Equal(SaleResultStatus.Succeeded, (await world.Settlement.SettleAsync(world.Agent, hold.Id,
            new(Guid.NewGuid(), 3, payment, review), default)).Status);
        if (deliver)
        {
            Assert.Equal(SaleResultStatus.Succeeded, (await world.Services.GetRequiredService<SaleHandoverService>()
                .DeliverAsync(world.Agent, hold.Id, new(Guid.NewGuid(), 4,
                    new(vehicle.MileageKilometres, 2, true, true, true)), default)).Status);
        }

        var original = await database.SaleDocuments.AsNoTracking().FirstAsync(item =>
            item.SalesOrderId == hold.Id && item.Kind == SaleDocumentKind.PurchaseOrder);
        return (hold.Id, original, review.BuyerName!);
    }

    // The owner role ages every record the deadline reads, because the runtime role cannot rewrite history.
    private static async Task AgeAsync(NpgsqlConnection owner, string age)
    {
        await AgeTableAsync(owner, "SalesOrders", string.Format(AgeColumns, age));
        await AgeTableAsync(owner, "SalesOrderEvents", $"\"RecordedAt\" = \"RecordedAt\" - interval '{age}'");
        await AgeTableAsync(owner, "SaleDocumentCorrections", $"\"RecordedAt\" = \"RecordedAt\" - interval '{age}'");
    }

    private static async Task<bool> RemovedAsync(DealershipDbContext database, Guid saleId)
    {
        var reviews = await database.Database.SqlQueryRaw<string>(
            "SELECT \"SettlementReview\"::text AS \"Value\" FROM \"SalesOrderEvents\" " +
            "WHERE \"Kind\" = 'Settled' AND \"SalesOrderId\" = {0}", saleId).ToListAsync();
        return !reviews.Single().Contains("BuyerNric", StringComparison.Ordinal);
    }

    private async Task<string?> ConstraintAsync(string sql)
    {
        var failure = await FailAsync(sql);
        return failure.ConstraintName;
    }

    private async Task<string> StateAsync(string sql) => (await FailAsync(sql)).SqlState;

    private async Task<PostgresException> FailAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(fixture.Database.RuntimeConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
    }

    private DealershipDbContext NewContext() => new(new DbContextOptionsBuilder<DealershipDbContext>()
        .UseNpgsql(fixture.Database.RuntimeConnectionString).Options);
}
