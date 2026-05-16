using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Dealership.Application.Accounts;

using Dealership.Application.Sales;
using Dealership.Domain.Sales;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Dealership.IntegrationTests;

public sealed class SaleSettlementTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task SettlementFreezesApprovedTermsAndReplaysAfterReassignmentAndRestart()
    {
        var world = await SaleWorld.SharedAsync(fixture);
        var held = await ApprovedAsync(world);
        var sale = await ReadAsync(held.Id);
        var request = await RequestAsync(sale);
        Assert.Equal(SaleResultStatus.Forbidden, (await world.Settlement.SettleAsync(world.Manager, held.Id, request,
            default)).Status);
        Assert.Equal(SaleResultStatus.NotFound, (await world.Settlement.SettleAsync(world.Colleague, held.Id, request,
            default)).Status);
        var underpaid = request with { Payment = request.Payment with { ReceivedAmount = sale.BalancePayable - 0.01m } };
        Assert.Equal(SaleResultStatus.Invalid, (await world.Settlement.SettleAsync(world.Agent, held.Id, underpaid,
            default)).Status);
        await RejectSqlAsync("""
            WITH moment AS (SELECT clock_timestamp() AS now)
            UPDATE "SalesOrders" SET "Status" = 'Settled', "PausedSeconds" = NULL,
                "SettledAt" = moment.now, "UpdatedAt" = moment.now, "Version" = "Version" + 1,
                "PaymentEvidence" = '{"Method":0,"ReceivedAmount":0,"PaymentReference":"CASH 48739",
                    "PaymentDate":"2026-11-02","Bank":null,"FinancingAmount":0,"LouReference":null,
                    "LouVerified":false,"ZeroBalanceConfirmed":false}'::jsonb
            FROM moment WHERE "Id" = @id
            """, held.Id, "CK_SalesOrders_Payment");

        var result = await world.Settlement.SettleAsync(world.Agent, held.Id, request, default);
        Assert.Equal(SaleResultStatus.Succeeded, result.Status);
        Assert.Equal("Settled", result.Receipt!.Status);
        Assert.Equal(result, await world.Settlement.SettleAsync(world.Agent, held.Id, request, default));
        Assert.Equal(SaleResultStatus.Conflict, (await world.Settlement.SettleAsync(world.Agent, held.Id,
            request with { Payment = request.Payment with { PaymentReference = "Different transfer" } }, default)).Status);

        await using (var database = NewContext())
        {
            Assert.Equal(VehicleStatus.Sold, (await database.Vehicles.SingleAsync(item => item.Id == held.VehicleId)).Status);
            Assert.False((await database.Reservations.SingleAsync(item => item.Id == held.Id)).IsActive);
            Assert.False(await database.ReservationReleases.AnyAsync(item => item.ReservationId == held.Id));
            var entry = await database.SalesOrderEvents.SingleAsync(item => item.Id == request.OperationId);
            Assert.Equal(request.Payment, entry.Snapshot.PaymentEvidence);
            Assert.Equal(request.Review, entry.SettlementReview);
            var documents = await database.SaleDocuments.Where(item => item.SalesOrderId == held.Id).ToListAsync();
            Assert.Equal(2, documents.Count);
            Assert.Equal(sale.OrderNumber, documents.Single(item => item.Kind == SaleDocumentKind.PurchaseOrder).Number);
            Assert.StartsWith("VRC-", documents.Single(item => item.Kind == SaleDocumentKind.PaymentReceipt).Number);
            Assert.All(documents, document =>
            {
                Assert.Equal(request.OperationId, document.IssuingOperationId);
                Assert.Equal(request.Review, document.Contents().Review);
                Assert.Equal(entry.Snapshot, document.Contents().Sale);
                Assert.Equal(entry.RecordedAt, document.IssuedAt);
            });
        }

        Assert.Equal(SaleResultStatus.Conflict, (await world.Settlement.SettleAsync(world.Agent, held.Id,
            request with { Review = request.Review with { BuyerAddress = "24 Jalan Melati" } }, default)).Status);
        await RejectSqlAsync("UPDATE \"SaleDocuments\" SET \"ContentsJson\" = '{}'::jsonb WHERE \"SalesOrderId\" = @id",
            held.Id, "CK_SaleDocuments_Immutable");
        await RejectSqlAsync("UPDATE \"SalesOrderEvents\" SET \"SettlementReview\" = '{}'::jsonb WHERE \"Id\" = @id",
            request.OperationId, "CK_SalesOrderEvents_Immutable");

        Assert.Equal(SaleResultStatus.Conflict, (await world.Terms.CancelAsync(world.Manager, held.Id,
            new(Guid.NewGuid(), 4, "Buyer changed their mind"), default)).Status);
        Assert.Equal(SaleResultStatus.Conflict, (await world.Terms.SaveTermsAsync(world.Agent, held.Id,
            new(Guid.NewGuid(), 4, null, 100m, null, 0m), default)).Status);
        var moved = await world.Handling.AssignAsync(world.Manager, held.Id, new(Guid.NewGuid(), 4, world.Colleague), default);
        Assert.Equal(SaleResultStatus.Succeeded, moved.Status);
        using var restarted = fixture.CreateFactory();
        using var restartedScope = restarted.Services.CreateScope();
        var service = restartedScope.ServiceProvider.GetRequiredService<SaleSettlementService>();
        Assert.Equal(result, await service.SettleAsync(world.Agent, held.Id, request, default));
        Assert.Equal(SaleResultStatus.Forbidden, (await service.SettleAsync(world.Manager, held.Id,
            request with { OperationId = Guid.NewGuid(), ExpectedVersion = 5 }, default)).Status);

        await RejectSqlAsync("""
            UPDATE "SalesOrders" SET "PaymentEvidence" = jsonb_set("PaymentEvidence", '{ReceivedAmount}', '0'),
                "Version" = "Version" + 1 WHERE "Id" = @id
            """, held.Id);
        await RejectSqlAsync("""
            UPDATE "SalesOrders" SET "NegotiatedDiscount" = "NegotiatedDiscount" + 1,
                "BalancePayable" = "BalancePayable" - 1, "Version" = "Version" + 1 WHERE "Id" = @id
            """, held.Id);
        await RejectSqlAsync("UPDATE \"Vehicles\" SET \"Status\" = 'Available' WHERE \"Id\" = @id", held.VehicleId);
    }

    [Fact]
    public async Task ConcurrentSettlementCommitsOnceAndMissingReceiptRollsBackEveryWrite()
    {
        var world = await SaleWorld.SharedAsync(fixture);
        var held = await ApprovedAsync(world);
        var request = await RequestAsync(await ReadAsync(held.Id));
        using var first = world.Services.CreateScope();
        using var second = world.Services.CreateScope();
        var results = await Task.WhenAll(
            first.ServiceProvider.GetRequiredService<SaleSettlementService>().SettleAsync(world.Agent, held.Id, request, default),
            second.ServiceProvider.GetRequiredService<SaleSettlementService>().SettleAsync(world.Agent, held.Id,
                request with { OperationId = Guid.NewGuid() }, default));
        Assert.Single(results, item => item.Status == SaleResultStatus.Succeeded);
        Assert.Single(results, item => item.Status == SaleResultStatus.Conflict);
        await using (var issued = NewContext())
        {
            Assert.Equal(2, await issued.SaleDocuments.CountAsync(item => item.SalesOrderId == held.Id));
        }

        var competing = await ApprovedAsync(world);
        var competingRequest = await RequestAsync(await ReadAsync(competing.Id));
        var race = await Task.WhenAll(
            first.ServiceProvider.GetRequiredService<SaleSettlementService>().SettleAsync(world.Agent, competing.Id,
                competingRequest, default),
            second.ServiceProvider.GetRequiredService<SaleTermsService>().CancelAsync(world.Manager, competing.Id,
                new(Guid.NewGuid(), competingRequest.ExpectedVersion, "Customer withdrew before payment"), default));
        Assert.Single(race, item => item.Status == SaleResultStatus.Succeeded);
        var outcome = await ReadAsync(competing.Id);
        await using (var persisted = NewContext())
        {
            var refunded = await persisted.ReservationReleases.AnyAsync(item => item.ReservationId == competing.Id);
            Assert.Equal(outcome.Status == SalesOrderStatus.Cancelled, refunded);
            Assert.Equal(outcome.Status == SalesOrderStatus.Settled, outcome.PaymentEvidence is not null);
        }

        var rollback = await ApprovedAsync(world);
        await using var database = NewContext();
        await using (var transaction = await database.Database.BeginTransactionAsync())
        {
            var sale = await database.SalesOrders.SingleAsync(item => item.Id == rollback.Id);
            sale.Settle(Guid.NewGuid(), (await RequestAsync(sale)).Payment, null, world.Agent, DateTimeOffset.UtcNow);
            await database.SaveChangesAsync();
            var failure = await Assert.ThrowsAsync<PostgresException>(() => transaction.CommitAsync());
            Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
        }

        database.ChangeTracker.Clear();
        Assert.Equal(SalesOrderStatus.Approved, (await database.SalesOrders.SingleAsync(item => item.Id == rollback.Id)).Status);
        Assert.True((await database.Reservations.SingleAsync(item => item.Id == rollback.Id)).IsActive);
        Assert.Equal(VehicleStatus.Available, (await database.Vehicles.SingleAsync(item => item.Id == rollback.VehicleId)).Status);
        Assert.Equal(SaleResultStatus.Succeeded, (await world.Settlement.SettleAsync(world.Agent, rollback.Id,
            await RequestAsync(await ReadAsync(rollback.Id)), default)).Status);
    }

    [Fact]
    public async Task ReviewPreventsIdentitySubstitutionAndMissingDocumentsRollBackSettlement()
    {
        var world = await SaleWorld.SharedAsync(fixture);
        var held = await ApprovedAsync(world);
        var request = await RequestAsync(await ReadAsync(held.Id));
        var reviews = new[]
        {
            request.Review with { BuyerNric = "990101101234" },
            request.Review with { Vehicle = request.Review.Vehicle with { Vin = "MNB12345678901234" } },
            request.Review with { Vehicle = request.Review.Vehicle with { MileageKilometres = 1 } }
        };
        foreach (var review in reviews)
        {
            Assert.Equal(SaleResultStatus.Conflict, (await world.Settlement.SettleAsync(world.Agent, held.Id,
                request with { Review = review }, default)).Status);
        }

        Assert.Equal(SaleResultStatus.Invalid, (await world.Settlement.SettleAsync(world.Agent, held.Id,
            request with { Review = request.Review with { BuyerName = " " } }, default)).Status);
        await using var database = NewContext();
        await using (var transaction = await database.Database.BeginTransactionAsync())
        {
            var sale = await database.SalesOrders.SingleAsync(item => item.Id == held.Id);
            var entry = sale.Settle(request.OperationId, request.Payment, null, world.Agent, DateTimeOffset.UtcNow);
            entry.CaptureSettlementReview(request.Review);
            database.SalesOrderEvents.Add(entry);
            var branch = await database.Branches.SingleAsync(item => item.Id == sale.BranchId);
            database.SaleDocuments.Add(new SaleDocument(sale.Id, entry.Id, SaleDocumentKind.PurchaseOrder,
                sale.OrderNumber!, entry.RecordedAt, new SaleDocumentContents(sale.CustomerId, sale.BranchId,
                    branch.Name, branch.Address, held.Number, request.Review, entry.Snapshot)));
            await database.SaveChangesAsync();
            var failure = await Assert.ThrowsAsync<PostgresException>(() => transaction.CommitAsync());
            Assert.Equal("CK_SalesOrders_Documents", failure.ConstraintName);
        }

        database.ChangeTracker.Clear();
        Assert.Empty(await database.SaleDocuments.Where(item => item.SalesOrderId == held.Id).ToListAsync());
        Assert.False(await database.SalesOrderEvents.AnyAsync(item => item.Id == request.OperationId));
        Assert.Equal(SalesOrderStatus.Approved, (await ReadAsync(held.Id)).Status);
        Assert.True((await database.Reservations.SingleAsync(item => item.Id == held.Id)).IsActive);
        Assert.Equal(SaleResultStatus.Succeeded, (await world.Settlement.SettleAsync(world.Agent, held.Id, request, default)).Status);
    }

    [Fact]
    public async Task SettlementHttpUsesExactMoneyAndDocumentsFollowCurrentOwnership()
    {
        var world = await SaleWorld.SharedAsync(fixture);
        var held = await ApprovedAsync(world);
        var request = await RequestAsync(await ReadAsync(held.Id));
        using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var commandPath = $"/api/staff/sales/{held.Id}/settlements";
        var documentsPath = $"/api/sales/{held.Id}/documents";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(documentsPath)).StatusCode);
        await LoginAsync(client, world.Agent);
        var reviewed = await client.GetFromJsonAsync<SaleSettlementReview>($"/api/staff/sales/{held.Id}/settlement-review");
        Assert.Equal(request.Review, reviewed);
        var input = new SaleSettlementInput(request.OperationId, request.ExpectedVersion,
            new(request.Payment.Method, request.Payment.ReceivedAmount, request.Payment.PaymentReference,
                request.Payment.PaymentDate, null, 0m, null, false, false), request.Review);
        var json = JsonSerializer.Serialize(input, JsonSerializerOptions.Web)
            .Replace("\"financingAmount\":0", "\"financingAmount\":0.001", StringComparison.Ordinal);
        using var invalid = new StringContent(json, Encoding.UTF8, "application/json");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync(commandPath, invalid)).StatusCode);
        var response = await client.PostAsJsonAsync(commandPath, input);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(commandPath, input)).StatusCode);
        var documents = await client.GetFromJsonAsync<List<SaleIssuedDocument>>(documentsPath);
        Assert.Equal(2, documents!.Count);
        Assert.All(documents, document => Assert.Equal(request.Review, document.Contents.Review));

        await LoginAsync(client, held.CustomerId);
        var customerRead = await client.GetAsync(documentsPath);
        Assert.Equal(HttpStatusCode.OK, customerRead.StatusCode);
        Assert.True(customerRead.Headers.CacheControl!.NoStore);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/staff/sales/{held.Id}/settlement-review")).StatusCode);
        await LoginAsync(client, world.OtherBranchAgent);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(documentsPath)).StatusCode);
        var unrelated = await world.HoldAsync();
        await using (var connection = new NpgsqlConnection(fixture.Database.RuntimeConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("""
                INSERT INTO "SaleDocuments" ("Id", "SalesOrderId", "IssuingOperationId", "Kind", "Number", "IssuedAt", "ContentsJson")
                VALUES (gen_random_uuid(), @sale, @operation, 'PaymentReceipt', 'VRC-9999-999999', clock_timestamp(), '{}'::jsonb)
                """, connection);
            command.Parameters.AddWithValue("sale", unrelated.Id);
            command.Parameters.AddWithValue("operation", request.OperationId);
            var failure = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, failure.SqlState);
            Assert.Contains("SalesOrderEvents", failure.ConstraintName!);
        }

        var reads = world.Services.GetRequiredService<ISaleDocumentStore>();
        Assert.Equal(SaleResultStatus.NotFound, (await reads.ReviewAsync(world.Agent, unrelated.Id, default)).Status);
        Assert.Equal(SaleResultStatus.Succeeded, (await reads.ReadAsync(world.Manager, held.Id, default)).Status);
        await LoginAsync(client, unrelated.CustomerId);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(documentsPath)).StatusCode);

        await world.Handling.AssignAsync(world.Manager, held.Id, new(Guid.NewGuid(), 4, world.Colleague), default);
        Assert.Equal(SaleResultStatus.NotFound, (await reads.ReadAsync(world.Agent, held.Id, default)).Status);
        Assert.Equal(SaleResultStatus.Succeeded, (await reads.ReadAsync(world.Colleague, held.Id, default)).Status);
        await using var database = NewContext();
        await database.CustomerProfiles.Where(item => item.UserId == held.CustomerId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.LegalName, "Hafiz bin Rahman")
                .SetProperty(item => item.ResidentialAddress, "30 Jalan Mawar, Shah Alam"));
        await database.Users.Where(item => item.Id == world.Colleague)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.IsActive, false));
        try
        {
            Assert.Equal(SaleResultStatus.Forbidden, (await reads.ReadAsync(world.Colleague, held.Id, default)).Status);
        }
        finally
        {
            await database.Users.Where(item => item.Id == world.Colleague)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.IsActive, true));
        }

        await LoginAsync(client, held.CustomerId);
        var retained = await client.GetFromJsonAsync<List<SaleIssuedDocument>>(documentsPath);
        Assert.Equal(documents.Select(item => item.Contents), retained!.Select(item => item.Contents));
    }

    private async Task LoginAsync(HttpClient client, Guid actorId)
    {
        await using var database = NewContext();
        var email = await database.Users.Where(item => item.Id == actorId).Select(item => item.Email).SingleAsync();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(Guid.NewGuid(), email!, "Password123!"));
        var session = (await response.Content.ReadFromJsonAsync<LoginSession>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AcquiresTheExistingTradeInWithoutChangingItsInspection(bool inspectionSigned)
    {
        var world = await SaleWorld.SharedAsync(fixture);
        var held = await world.HoldAsync(intakeOwner: true);
        await world.Handling.ClaimAsync(world.Agent, held.Id, new(Guid.NewGuid(), 1), default);
        var tradeInId = await world.TradeInAsync();
        await using var database = NewContext();
        var candidate = await database.Vehicles.AsNoTracking().SingleAsync(item => item.Id == tradeInId);
        var inspection = await database.InspectionDrafts.AsNoTracking().SingleAsync(item => item.VehicleId == candidate.Id);
        if (!inspectionSigned)
        {
            var reopened = await world.Services.GetRequiredService<Dealership.Application.Inspections.InspectionReopeningService>()
                .ReopenAsync(world.Manager, inspection.Id, new(Guid.NewGuid(), inspection.AssignmentVersion,
                    inspection.Revision, "Check newly reported condition"), default);
            Assert.Equal(Dealership.Application.Inspections.InspectionAssignmentStatus.Succeeded, reopened.Status);
        }

        var before = await database.InspectionDrafts.AsNoTracking().SingleAsync(item => item.Id == inspection.Id);
        var vehicleBefore = await database.Vehicles.AsNoTracking().SingleAsync(item => item.Id == candidate.Id);
        var discount = inspectionSigned ? 0m : (await ReadAsync(held.Id)).BalancePayable - 1000m;
        Assert.Equal(SaleResultStatus.Succeeded, (await world.Terms.SaveTermsAsync(world.Agent, held.Id,
            new(Guid.NewGuid(), 2, null, discount, candidate.IntakeAppointmentId, 1000m), default)).Status);
        Assert.Equal(SaleResultStatus.Succeeded, (await world.Terms.SubmitAsync(world.Agent, held.Id,
            new(Guid.NewGuid(), 3), default)).Status);
        if (!inspectionSigned)
        {
            Assert.Equal(SaleResultStatus.Succeeded, (await world.Terms.ApproveAsync(world.Manager, held.Id,
                new(Guid.NewGuid(), 4), default)).Status);
        }

        var request = await RequestAsync(await ReadAsync(held.Id));
        var payment = inspectionSigned
            ? new SalePaymentEvidence(SalePaymentMethod.HirePurchase, 5000m, null, null, "Maybank",
                request.Payment.ReceivedAmount - 5000m, "LOU 42873", true, false)
            : new SalePaymentEvidence(SalePaymentMethod.ZeroBalance, 0m, null, null, null, 0m, null, false, true);
        request = request with { Payment = payment };
        Assert.Equal(SaleResultStatus.Succeeded, (await world.Settlement.SettleAsync(world.Agent, held.Id, request, default)).Status);
        Assert.Equal(SaleResultStatus.Succeeded, (await world.Settlement.SettleAsync(world.Agent, held.Id, request, default)).Status);
        var after = await database.InspectionDrafts.AsNoTracking().SingleAsync(item => item.Id == inspection.Id);
        Assert.Equal((before.Id, before.IsSigned, before.Revision, before.AssignmentVersion),
            (after.Id, after.IsSigned, after.Revision, after.AssignmentVersion));
        Assert.Equal(vehicleBefore.Status, (await database.Vehicles.AsNoTracking().SingleAsync(item => item.Id == candidate.Id)).Status);
        Assert.Equal(candidate.Id, (await ReadAsync(held.Id)).AcquiredVehicleId);
        Assert.Equal(1, await database.SalesOrders.CountAsync(item => item.AcquiredVehicleId == candidate.Id));
        var handover = world.Services.GetRequiredService<SaleHandoverService>();
        var delivery = new SaleHandoverRequest(Guid.NewGuid(), request.ExpectedVersion + 1,
            new(request.Review.Vehicle.MileageKilometres, 2, true, true, true));
        Assert.Equal(SaleResultStatus.Succeeded,
            (await handover.DeliverAsync(world.Agent, held.Id, delivery, default)).Status);
        Assert.Equal(SaleResultStatus.Succeeded,
            (await handover.DeliverAsync(world.Agent, held.Id, delivery, default)).Status);
        Assert.Equal(candidate.Id, (await ReadAsync(held.Id)).AcquiredVehicleId);
        Assert.Equal(1, await database.SalesOrders.CountAsync(item => item.AcquiredVehicleId == candidate.Id));
        var retained = await database.InspectionDrafts.AsNoTracking().SingleAsync(item => item.Id == inspection.Id);
        Assert.Equal((before.Id, before.IsSigned, before.Revision, before.AssignmentVersion),
            (retained.Id, retained.IsSigned, retained.Revision, retained.AssignmentVersion));
    }

    private static async Task<Reservation> ApprovedAsync(SaleWorld world)
    {
        var held = await world.HoldAsync();
        Assert.Equal(SaleResultStatus.Succeeded, (await world.Handling.ClaimAsync(world.Agent, held.Id,
            new(Guid.NewGuid(), 1), default)).Status);
        Assert.Equal(SaleResultStatus.Succeeded, (await world.Terms.SubmitAsync(world.Agent, held.Id,
            new(Guid.NewGuid(), 2), default)).Status);
        return held;
    }

    private async Task<SaleSettlementRequest> RequestAsync(SalesOrder sale)
    {
        await using var database = NewContext();
        var profile = await database.CustomerProfiles.AsNoTracking().SingleAsync(item => item.UserId == sale.CustomerId);
        var vehicle = await database.Vehicles.AsNoTracking().SingleAsync(item => item.Id == sale.VehicleId);
        return new(Guid.NewGuid(), sale.Version,
            new(SalePaymentMethod.BankTransfer, sale.BalancePayable, "MBB 021126 43789", new DateOnly(2026, 11, 2),
                null, 0m, null, false, false),
            new(profile.LegalName, profile.Nric, profile.ResidentialAddress, SaleReviewedVehicle.Of(vehicle)));
    }
    private async Task<SalesOrder> ReadAsync(Guid id)
    {
        await using var database = NewContext();
        return await database.SalesOrders.AsNoTracking().SingleAsync(item => item.Id == id);
    }

    private async Task RejectSqlAsync(string sql, Guid id, string? constraint = null)
    {
        await using var connection = new NpgsqlConnection(fixture.Database.RuntimeConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        var failure = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
        if (constraint is not null)
        {
            Assert.Equal(constraint, failure.ConstraintName);
        }
    }

    private DealershipDbContext NewContext() => new(new DbContextOptionsBuilder<DealershipDbContext>()
        .UseNpgsql(fixture.Database.RuntimeConnectionString).Options);
}
