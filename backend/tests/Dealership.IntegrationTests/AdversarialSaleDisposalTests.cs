using Dealership.Application.Accounts;
using Dealership.Application.Sales;
using Dealership.Domain.Sales;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static Dealership.IntegrationTests.RetentionMinimizationTests;

namespace Dealership.IntegrationTests;

// Preservation holds and the seven-year boundary against careless input and direct statements. Its own class: the
// shared sale world provisions one branch per class database.
public sealed class AdversarialSaleDisposalTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    private const string AgeColumns = "\"CreatedAt\" = \"CreatedAt\" - ({0}), \"UpdatedAt\" = \"UpdatedAt\" - ({0}), " +
        "\"ClaimedAt\" = \"ClaimedAt\" - ({0}), \"DueAt\" = \"DueAt\" - ({0}), \"SubmittedAt\" = \"SubmittedAt\" - ({0}), " +
        "\"DecidedAt\" = \"DecidedAt\" - ({0}), \"ClosedAt\" = \"ClosedAt\" - ({0}), \"SettledAt\" = \"SettledAt\" - ({0}), " +
        "\"DeliveredAt\" = \"DeliveredAt\" - ({0})";

    [Fact]
    public async Task HoldInputsAreValidatedAndOnlyAnAdministratorCanPlaceOrReleaseOne()
    {
        // Guarantee: a hold needs a future date within ten years and a clean reason, names a real reservation, and is
        // an Administrator-only command; releasing a hold that does not exist is harmless.
        var world = await SaleWorld.SharedAsync(fixture);
        var sale = await PrepareAsync(world, deliver: true);
        var administrator = await ReservationTestData.ProvisionAsync(world.Services, null, AccountRoles.Administrator);
        var holds = world.Services.GetRequiredService<SaleRetentionHoldService>();
        await using var database = NewContext();
        var number = await database.Reservations.AsNoTracking().Where(item => item.Id == sale.SaleId)
            .Select(item => item.Number).SingleAsync();
        var future = DateTimeOffset.UtcNow.AddYears(2);

        foreach (var reason in new string?[] { null, "", " ", "\t\n", "bad\u0007reason", "line\r\nbreak", new string('r', 1001) })
        {
            Assert.Equal(SaleRetentionHoldStatus.Invalid,
                (await holds.SetByNumberAsync(administrator, new(number, future, reason), default)).Status);
        }

        foreach (var until in new[]
        {
            DateTimeOffset.UtcNow.AddSeconds(-1), DateTimeOffset.UtcNow.AddYears(-8), DateTimeOffset.MinValue,
            DateTimeOffset.UtcNow.AddYears(10).AddDays(1), DateTimeOffset.UtcNow.AddYears(500)
        })
        {
            Assert.Equal(SaleRetentionHoldStatus.Invalid,
                (await holds.SetByNumberAsync(administrator, new(number, until, "Dispute under review"), default)).Status);
        }

        foreach (var wrong in new string?[] { null, "", "   ", new string('9', 41), "RSV-0000-000000", "rsv'; DROP TABLE \"SalesOrders\"; --" })
        {
            var status = (await holds.SetByNumberAsync(administrator, new(wrong, future, "Dispute under review"), default)).Status;
            Assert.True(status is SaleRetentionHoldStatus.Invalid or SaleRetentionHoldStatus.NotFound, status.ToString());
        }

        Assert.False(await HeldAsync(database, sale.SaleId));

        // Releasing when no hold exists changes nothing and does not fail.
        var release = await holds.SetByNumberAsync(administrator, new(number, null, null), default);
        Assert.Equal(SaleRetentionHoldStatus.Succeeded, release.Status);
        Assert.False(await HeldAsync(database, sale.SaleId));

        foreach (var actor in new[] { world.Manager, world.Agent, Guid.NewGuid(), Guid.Empty })
        {
            var denied = await holds.SetByNumberAsync(actor, new(number, future, "Dispute under review"), default);
            Assert.True(denied.Status is SaleRetentionHoldStatus.Forbidden or SaleRetentionHoldStatus.Invalid, denied.Status.ToString());
            Assert.False(await HeldAsync(database, sale.SaleId));
        }

        // Control: the same request from an Administrator is accepted, and the exact ten-year edge is the limit.
        Assert.Equal(SaleRetentionHoldStatus.Succeeded,
            (await holds.SetByNumberAsync(administrator, new(number, future, "Dispute under review"), default)).Status);
        Assert.True(await HeldAsync(database, sale.SaleId));
        Assert.Equal(SaleRetentionHoldStatus.Succeeded, (await holds.SetByNumberAsync(administrator,
            new(" " + number + " ", DateTimeOffset.UtcNow.AddYears(10).AddMinutes(-5), "  Longest hold  "), default)).Status);
        Assert.Equal(SaleRetentionHoldStatus.Succeeded, (await holds.SetByNumberAsync(administrator, new(number, null, null), default)).Status);
    }

    [Fact]
    public async Task DirectSqlMayOnlyTouchTheHoldColumnsOfAFinalSaleAndNeverDeleteIt()
    {
        // Guarantee: for Delivered and Cancelled sales the database accepts a statement that changes only the hold and
        // refuses every other UPDATE, a hold without a reason, and any DELETE or TRUNCATE.
        var world = await SaleWorld.SharedAsync(fixture);
        var delivered = await PrepareAsync(world, deliver: true);
        var held = await world.HoldAsync();
        Assert.Equal(SaleResultStatus.Succeeded, (await world.Terms.CancelAsync(world.Manager, held.Id,
            new(Guid.NewGuid(), 1, "Customer withdrew"), default)).Status);
        var administrator = await ReservationTestData.ProvisionAsync(world.Services, null, AccountRoles.Administrator);

        foreach (var saleId in new[] { delivered.SaleId, held.Id })
        {
            var hold = $"\"RetentionHoldUntil\" = now() + interval '1 year', \"RetentionHoldReason\" = 'Dispute', " +
                $"\"RetentionHoldById\" = '{administrator}'";
            Assert.NotNull(await FailureAsync($"UPDATE \"SalesOrders\" SET \"ClaimedAt\" = \"ClaimedAt\" + interval '1 second' WHERE \"Id\" = '{saleId}'"));
            Assert.NotNull(await FailureAsync($"UPDATE \"SalesOrders\" SET \"Version\" = \"Version\" + 1 WHERE \"Id\" = '{saleId}'"));
            Assert.NotNull(await FailureAsync($"UPDATE \"SalesOrders\" SET \"Status\" = 'Open' WHERE \"Id\" = '{saleId}'"));
            Assert.NotNull(await FailureAsync($"UPDATE \"SalesOrders\" SET {hold}, \"CustomerId\" = '{administrator}' WHERE \"Id\" = '{saleId}'"));
            Assert.NotNull(await FailureAsync($"UPDATE \"SalesOrders\" SET {hold}, \"Version\" = \"Version\" + 1 WHERE \"Id\" = '{saleId}'"));
            Assert.NotNull(await FailureAsync($"UPDATE \"SalesOrders\" SET \"RetentionHoldUntil\" = now() + interval '1 year' WHERE \"Id\" = '{saleId}'"));
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, await FailureAsync($"DELETE FROM \"SalesOrders\" WHERE \"Id\" = '{saleId}'"));
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, await FailureAsync("TRUNCATE \"SalesOrders\""));

            // Control: a statement of the hold columns alone is the one accepted change.
            Assert.Null(await FailureAsync($"UPDATE \"SalesOrders\" SET {hold} WHERE \"Id\" = '{saleId}'"));
            Assert.Null(await FailureAsync($"UPDATE \"SalesOrders\" SET \"RetentionHoldUntil\" = NULL, \"RetentionHoldReason\" = NULL, \"RetentionHoldById\" = NULL WHERE \"Id\" = '{saleId}'"));
        }
    }

    [Fact]
    public async Task ASettledSaleOnlyEverChangesThroughItsOwnWorkflowAndIsNeverDisposed()
    {
        // Guarantee: a hold on a Settled (undelivered) sale changes only the hold, and the disposal worker never
        // removes buyer details from it, however old it is.
        var world = await SaleWorld.SharedAsync(fixture);
        var settled = await PrepareAsync(world, deliver: false);
        var administrator = await ReservationTestData.ProvisionAsync(world.Services, null, AccountRoles.Administrator);
        var holds = world.Services.GetRequiredService<SaleRetentionHoldService>();
        await using var database = NewContext();
        var before = await database.SalesOrders.AsNoTracking().SingleAsync(item => item.Id == settled.SaleId);

        var number = await database.Reservations.AsNoTracking().Where(item => item.Id == settled.SaleId)
            .Select(item => item.Number).SingleAsync();
        var placed = await holds.SetByNumberAsync(administrator,
            new(number, DateTimeOffset.UtcNow.AddYears(1), "Dispute under review"), default);
        Assert.True(placed.Status is SaleRetentionHoldStatus.Succeeded or SaleRetentionHoldStatus.Invalid, placed.Status.ToString());
        var after = await database.SalesOrders.AsNoTracking().SingleAsync(item => item.Id == settled.SaleId);
        Assert.Equal((before.Version, before.Status, before.BalancePayable), (after.Version, after.Status, after.BalancePayable));

        await using var owner = new NpgsqlConnection(fixture.Database.ConnectionString);
        await owner.OpenAsync();
        await AgeAsync(owner, "interval '20 years'");
        for (var run = 0; run < 3; run++)
        {
            await Worker(fixture.CreateFactory()).RunBatchAsync(CancellationToken.None);
        }

        var document = await database.SaleDocuments.AsNoTracking().FirstAsync(item => item.SalesOrderId == settled.SaleId);
        Assert.False(document.Contents().Review.BuyerDetailsRemoved);
    }

    [Fact]
    public async Task DisposalFollowsTheDeadlineToTheMinuteAndRepeatRunsChangeNothing()
    {
        // Guarantee: buyer details stay one minute before the seven-year deadline and go one minute after it, with or
        // without a settings row, and further runs are no-ops. Negative control: the aged-before-deadline run keeps them.
        var world = await SaleWorld.SharedAsync(fixture);
        var sale = await PrepareAsync(world, deliver: true);
        await using var database = NewContext();
        await using var owner = new NpgsqlConnection(fixture.Database.ConnectionString);
        await owner.OpenAsync();
        await ExecuteAsync(owner, "ALTER TABLE \"RetentionSettings\" DISABLE TRIGGER USER; DELETE FROM \"RetentionSettings\"; " +
            "ALTER TABLE \"RetentionSettings\" ENABLE TRIGGER USER;");

        // Anchors two minutes short of seven years: the deadline is still two minutes ahead.
        await AgeAsync(owner, "interval '7 years' - interval '2 minutes'");
        await Worker(fixture.CreateFactory()).RunBatchAsync(CancellationToken.None);
        Assert.False(await RemovedAsync(database, sale.SaleId));

        // Four more minutes put the deadline two minutes behind.
        await AgeAsync(owner, "interval '4 minutes'");
        await Worker(fixture.CreateFactory()).RunBatchAsync(CancellationToken.None);
        Assert.True(await RemovedAsync(database, sale.SaleId));

        var documents = await database.SaleDocuments.AsNoTracking().Where(item => item.SalesOrderId == sale.SaleId).ToListAsync();
        var snapshot = documents.Select(item => item.ContentsJson).ToList();
        for (var run = 0; run < 3; run++)
        {
            await Worker(fixture.CreateFactory()).RunBatchAsync(CancellationToken.None);
        }

        Assert.Equal(snapshot, (await database.SaleDocuments.AsNoTracking().Where(item => item.SalesOrderId == sale.SaleId)
            .ToListAsync()).Select(item => item.ContentsJson).ToList());
    }

    [Fact]
    public async Task TheDeadlineUsesCalendarYearsAndTheLargerOfTheCurrentAndCommittedSetting()
    {
        // Guarantee: leap days and year ends follow calendar years in UTC; shortening the setting never shortens an
        // earlier commitment, and extending it reaches records that already exist.
        await using var owner = new NpgsqlConnection(fixture.Database.ConnectionString);
        await owner.OpenAsync();
        await ExecuteAsync(owner, "ALTER TABLE \"RetentionSettingChanges\" DISABLE TRIGGER USER; ALTER TABLE \"RetentionSettings\" DISABLE TRIGGER USER; " +
            "DELETE FROM \"RetentionSettingChanges\"; DELETE FROM \"RetentionSettings\"; " +
            "ALTER TABLE \"RetentionSettingChanges\" ENABLE TRIGGER USER; ALTER TABLE \"RetentionSettings\" ENABLE TRIGGER USER;");

        // No settings row: the approved default of seven years applies.
        Assert.Equal(DateTimeOffset.Parse("2027-02-28T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture), await DeadlineAsync(owner, "2020-02-29T12:00:00Z"));
        Assert.Equal(DateTimeOffset.Parse("2028-02-28T23:59:59Z", System.Globalization.CultureInfo.InvariantCulture), await DeadlineAsync(owner, "2021-02-28T23:59:59Z"));
        Assert.Equal(DateTimeOffset.Parse("2028-03-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture), await DeadlineAsync(owner, "2021-03-01T00:00:00Z"));
        Assert.Equal(DateTimeOffset.Parse("2033-12-31T23:59:59Z", System.Globalization.CultureInfo.InvariantCulture), await DeadlineAsync(owner, "2026-12-31T23:59:59Z"));
        Assert.Equal(DateTimeOffset.Parse("2033-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture), await DeadlineAsync(owner, "2026-01-01T00:00:00Z"));

        var administrator = Guid.NewGuid();
        using (var factory = fixture.CreateFactory())
        {
            using var scope = factory.Services.CreateScope();
            administrator = await ReservationTestData.ProvisionAsync(scope.ServiceProvider, null, AccountRoles.Administrator);
        }

        // Shortened to one year on 2030-01-01: an anchor before the change keeps seven years, one after gets one.
        await ExecuteAsync(owner, "ALTER TABLE \"RetentionSettingChanges\" DISABLE TRIGGER USER; ALTER TABLE \"RetentionSettings\" DISABLE TRIGGER USER; " +
            $"INSERT INTO \"RetentionSettings\" (\"Id\", \"SecurityArtifactDays\", \"MailDetailDays\", \"InvitationDays\", \"BusinessEvidenceYears\", \"UpdatedById\", \"UpdatedAtUtc\", \"Version\") " +
            $"VALUES (1, 30, 30, 30, 1, '{administrator}', '2030-01-01Z', 2); " +
            $"INSERT INTO \"RetentionSettingChanges\" (\"Id\", \"SettingsVersion\", \"Setting\", \"PreviousValue\", \"NewValue\", \"ShorteningConfirmed\", \"ActorId\", \"ChangedAtUtc\") " +
            $"VALUES (gen_random_uuid(), 2, 'BusinessEvidenceYears', 7, 1, true, '{administrator}', '2030-01-01Z'); " +
            "ALTER TABLE \"RetentionSettingChanges\" ENABLE TRIGGER USER; ALTER TABLE \"RetentionSettings\" ENABLE TRIGGER USER;");
        Assert.Equal(DateTimeOffset.Parse("2036-06-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture), await DeadlineAsync(owner, "2029-06-01T00:00:00Z"));
        Assert.Equal(DateTimeOffset.Parse("2031-06-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture), await DeadlineAsync(owner, "2030-06-01T00:00:00Z"));

        // Extended to ten years afterwards: every anchor, old or new, gets at least ten.
        await ExecuteAsync(owner, "ALTER TABLE \"RetentionSettings\" DISABLE TRIGGER USER; UPDATE \"RetentionSettings\" SET \"BusinessEvidenceYears\" = 10; " +
            "ALTER TABLE \"RetentionSettings\" ENABLE TRIGGER USER;");
        Assert.Equal(DateTimeOffset.Parse("2039-06-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture), await DeadlineAsync(owner, "2029-06-01T00:00:00Z"));
        Assert.Equal(DateTimeOffset.Parse("2040-06-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture), await DeadlineAsync(owner, "2030-06-01T00:00:00Z"));
    }

    private static async Task<DateTimeOffset> DeadlineAsync(NpgsqlConnection owner, string anchor)
    {
        await using var command = new NpgsqlCommand($"SELECT public.retention_business_deadline('{anchor}'::timestamptz)", owner);
        return (DateTimeOffset)(DateTime)(await command.ExecuteScalarAsync())!;
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
        await ExecuteAsync(owner, "ALTER TABLE \"SalesOrders\" DISABLE TRIGGER USER");
        await ExecuteAsync(owner, "UPDATE \"SalesOrders\" SET " + AgeColumns.Replace("{0}", age, StringComparison.Ordinal));
        await ExecuteAsync(owner, "ALTER TABLE \"SalesOrders\" ENABLE TRIGGER USER");
        await ExecuteAsync(owner, "ALTER TABLE \"SalesOrderEvents\" DISABLE TRIGGER USER");
        await ExecuteAsync(owner, $"UPDATE \"SalesOrderEvents\" SET \"RecordedAt\" = \"RecordedAt\" - ({age})");
        await ExecuteAsync(owner, "ALTER TABLE \"SalesOrderEvents\" ENABLE TRIGGER USER");
        await ExecuteAsync(owner, "ALTER TABLE \"SaleDocumentCorrections\" DISABLE TRIGGER USER");
        await ExecuteAsync(owner, $"UPDATE \"SaleDocumentCorrections\" SET \"RecordedAt\" = \"RecordedAt\" - ({age})");
        await ExecuteAsync(owner, "ALTER TABLE \"SaleDocumentCorrections\" ENABLE TRIGGER USER");
    }

    private static async Task<bool> RemovedAsync(DealershipDbContext database, Guid saleId)
    {
        var reviews = await database.Database.SqlQueryRaw<string>(
            "SELECT \"SettlementReview\"::text AS \"Value\" FROM \"SalesOrderEvents\" " +
            "WHERE \"Kind\" = 'Settled' AND \"SalesOrderId\" = {0}", saleId).ToListAsync();
        return !reviews.Single().Contains("BuyerNric", StringComparison.Ordinal);
    }

    private static async Task<bool> HeldAsync(DealershipDbContext database, Guid saleId) =>
        await database.SalesOrders.AsNoTracking().AnyAsync(item => item.Id == saleId && item.RetentionHoldUntil != null);

    // The SQL state of the refusal, or null when the database accepted the statement.
    private async Task<string?> FailureAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(fixture.Database.RuntimeConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        try
        {
            await command.ExecuteNonQueryAsync();
            return null;
        }
        catch (PostgresException failure)
        {
            return failure.SqlState;
        }
    }

    private DealershipDbContext NewContext() => new(new DbContextOptionsBuilder<DealershipDbContext>()
        .UseNpgsql(fixture.Database.RuntimeConnectionString).Options);
}
