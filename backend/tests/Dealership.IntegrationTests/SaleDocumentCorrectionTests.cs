using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dealership.Application.Accounts;
using Dealership.Application.Sales;
using Dealership.Domain.Sales;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Dealership.IntegrationTests;

public sealed class SaleDocumentCorrectionTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task ManagerReissuesOnlyBuyerSpellingWithCurrentScopeAndExactReplayAfterDelivery()
    {
        var world = await SaleWorld.SharedAsync(fixture);
        var (saleId, original, request) = await PrepareAsync(world, deliver: true);
        var service = world.Services.GetRequiredService<SaleDocumentCorrectionService>();
        foreach (var actor in new[] { world.Agent, world.OtherBranchAgent, original.Contents().CustomerId })
        {
            Assert.NotEqual(SaleResultStatus.Succeeded, (await service.ReissueAsync(actor, saleId, request, default)).Status);
        }

        Assert.Equal(SaleResultStatus.Invalid, (await service.ReissueAsync(world.Manager, saleId,
            request with { Correction = request.Correction with { Reason = " " } }, default)).Status);
        Assert.Equal(SaleResultStatus.Conflict, (await service.ReissueAsync(world.Manager, saleId,
            request with { ExpectedVersion = request.ExpectedVersion - 1 }, default)).Status);
        using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var path = $"/api/staff/sales/{saleId}/document-corrections";
        var body = new
        {
            request.OperationId,
            request.ExpectedVersion,
            request.DocumentId,
            request.Correction.BuyerName,
            request.Correction.BuyerAddress,
            request.Correction.Reason
        };
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(path, body)).StatusCode);
        await LoginAsync(client, world.Manager);
        var forbiddenField = new
        {
            request.OperationId,
            request.ExpectedVersion,
            request.DocumentId,
            request.Correction.BuyerName,
            request.Correction.BuyerAddress,
            request.Correction.Reason,
            BuyerNric = "950101101234"
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, forbiddenField)).StatusCode);
        var response = await client.PostAsJsonAsync(path, body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        var receipt = (await response.Content.ReadFromJsonAsync<SaleDocumentCorrectionReceipt>())!;
        Assert.Equal(original.Id, receipt.OriginalDocumentId);
        Assert.NotEqual(original.Number, receipt.ReplacementNumber);
        await using var database = NewContext();
        var sale = await database.SalesOrders.AsNoTracking().SingleAsync(item => item.Id == saleId);
        Assert.Equal(request.ExpectedVersion, sale.Version);
        Assert.Equal(SalesOrderStatus.Delivered, sale.Status);
        Assert.Equal(original.ContentsJson, (await database.SaleDocuments.SingleAsync(item => item.Id == original.Id)).ContentsJson);
        var replacement = await database.SaleDocuments.SingleAsync(item => item.Id == receipt.ReplacementDocumentId);
        Assert.Equal(original.IssuingOperationId, replacement.IssuingOperationId);
        Assert.Equal(original.Contents() with { Review = request.Correction.Apply(original.Contents().Review) }, replacement.Contents());
        Assert.Equal(1, await database.SaleDeliveryMailOutbox.CountAsync(item => item.SalesOrderId == saleId));
        using var restarted = fixture.CreateFactory();
        using var scope = restarted.Services.CreateScope();
        var replay = scope.ServiceProvider.GetRequiredService<SaleDocumentCorrectionService>();
        var history = (await scope.ServiceProvider.GetRequiredService<ISaleReadStore>()
            .HistoryAsync(world.Manager, saleId, 1, 10, default)).Value!;
        var originals = await database.SaleDocuments.AsNoTracking()
            .Where(item => item.SalesOrderId == saleId && item.CorrectionOperationId == null).ToListAsync();
        Assert.Equal(3, history.Items.SelectMany(item => item.Documents).Count());
        Assert.DoesNotContain(history.Items.SelectMany(item => item.Documents), item => item.Id == replacement.Id);
        foreach (var document in originals)
        {
            var issued = Assert.Single(history.Items, item => item.Id == document.IssuingOperationId);
            Assert.Contains(new SaleHistoryDocument(document.Id, document.Kind.ToString(), document.Number), issued.Documents);
        }
        Assert.Equal(receipt, (await replay.ReissueAsync(world.Manager, saleId, request, default)).Receipt);
        Assert.Equal(SaleResultStatus.Conflict, (await replay.ReissueAsync(world.Manager, saleId,
            request with { Correction = request.Correction with { Reason = "Different reason" } }, default)).Status);
        Assert.Equal(SaleResultStatus.Conflict, (await replay.ReissueAsync(world.Manager, saleId,
            request with { OperationId = Guid.NewGuid() }, default)).Status);
        await database.Users.Where(item => item.Id == world.Manager)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.IsActive, false));
        try
        {
            Assert.Equal(SaleResultStatus.Forbidden, (await replay.ReissueAsync(world.Manager, saleId, request, default)).Status);
        }
        finally
        {
            await database.Users.Where(item => item.Id == world.Manager)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.IsActive, true));
        }

        var otherBranch = await database.Users.Where(item => item.Id == world.OtherBranchAgent)
            .Select(item => item.BranchId).SingleAsync();
        await database.Users.Where(item => item.Id == world.Manager)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.BranchId, otherBranch));
        try
        {
            Assert.Equal(SaleResultStatus.NotFound, (await replay.ReissueAsync(world.Manager, saleId, request, default)).Status);
        }
        finally
        {
            await database.Users.Where(item => item.Id == world.Manager)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.BranchId, sale.BranchId));
        }

        await database.Branches.Where(item => item.Id == sale.BranchId)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.IsActive, false));
        try
        {
            Assert.Equal(SaleResultStatus.Forbidden, (await replay.ReissueAsync(world.Manager, saleId, request, default)).Status);
        }
        finally
        {
            await database.Branches.Where(item => item.Id == sale.BranchId)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.IsActive, true));
        }

        var reads = world.Services.GetRequiredService<ISaleDocumentStore>();
        var current = (await reads.ReadAsync(sale.CustomerId, saleId, default)).Value!;
        Assert.Equal(3, current.Count);
        Assert.Contains(current, item => item.Id == replacement.Id && item.ReplacesDocumentId == original.Id);
        var old = (await reads.ReadOneAsync(sale.CustomerId, saleId, original.Id, default)).Value!;
        Assert.Equal(replacement.Id, old.ReplacementDocumentId);
        Assert.Equal(original.Contents(), old.Contents);
        Assert.Equal(SaleResultStatus.NotFound, (await reads.ReadOneAsync(world.OtherBranchAgent, saleId, original.Id, default)).Status);
        var administrator = await ReservationTestData.ProvisionAsync(world.Services, null, AccountRoles.Administrator);
        Assert.Equal(SaleResultStatus.Forbidden, (await service.ReissueAsync(administrator, saleId, request, default)).Status);
        var generalManager = await ReservationTestData.ProvisionAsync(world.Services, null, AccountRoles.GeneralManager);
        var paymentReceipt = await database.SaleDocuments.AsNoTracking().SingleAsync(item =>
            item.SalesOrderId == saleId && item.Kind == SaleDocumentKind.PaymentReceipt);
        var receiptCorrection = await service.ReissueAsync(generalManager, saleId,
            request with { OperationId = Guid.NewGuid(), DocumentId = paymentReceipt.Id }, default);
        Assert.Equal(SaleResultStatus.Succeeded, receiptCorrection.Status);
        Assert.StartsWith("VRC-", receiptCorrection.Receipt!.ReplacementNumber);
        await SaleDocumentPdfChecks.VerifyAsync(world, saleId, original.Id, replacement.Id, factory, restarted,
            fixture.Database.RuntimeConnectionString);
        await RejectSqlAsync("UPDATE \"SaleDocuments\" SET \"Number\" = 'VPO-2099-999998' WHERE \"Id\" = @id", original.Id);
        await RejectSqlAsync("UPDATE \"SaleDocumentCorrections\" SET \"Reason\" = 'Changed reason' WHERE \"Id\" = @id", request.OperationId);
    }

    [Fact]
    public async Task CorrectionsSerializePerOriginalAndRequireExactAtomicReplacementWithoutChangingMilestones()
    {
        var world = await SaleWorld.SharedAsync(fixture);
        var (saleId, original, request) = await PrepareAsync(world, deliver: false);
        using var first = world.Services.CreateScope();
        using var second = world.Services.CreateScope();
        var results = await Task.WhenAll(
            first.ServiceProvider.GetRequiredService<SaleDocumentCorrectionService>().ReissueAsync(world.Manager, saleId, request, default),
            second.ServiceProvider.GetRequiredService<SaleDocumentCorrectionService>().ReissueAsync(world.Manager, saleId,
                request with { OperationId = Guid.NewGuid() }, default));
        Assert.Single(results, item => item.Status == SaleResultStatus.Succeeded);
        Assert.Single(results, item => item.Status == SaleResultStatus.Conflict);
        var receipt = results.Single(item => item.Receipt is not null).Receipt!;
        await using var database = NewContext();
        var replacement = await database.SaleDocuments.AsNoTracking().SingleAsync(item => item.Id == receipt.ReplacementDocumentId);
        var next = request with
        {
            OperationId = Guid.NewGuid(),
            DocumentId = replacement.Id,
            Correction = request.Correction with { BuyerName = "Nur Aina Abdullah" }
        };
        Assert.Equal(SaleResultStatus.Succeeded, (await world.Services.GetRequiredService<SaleDocumentCorrectionService>()
            .ReissueAsync(world.Manager, saleId, next, default)).Status);
        var sale = await database.SalesOrders.AsNoTracking().SingleAsync(item => item.Id == saleId);
        var vehicleMileage = await database.Vehicles.Where(item => item.Id == sale.VehicleId)
            .Select(item => item.MileageKilometres).SingleAsync();
        Assert.Equal(SaleResultStatus.Succeeded, (await world.Services.GetRequiredService<SaleHandoverService>()
            .DeliverAsync(world.Agent, saleId, new(Guid.NewGuid(), sale.Version,
                new(vehicleMileage, 2, true, true, true)), default)).Status);
        var note = await database.SaleDocuments.AsNoTracking().SingleAsync(item =>
            item.SalesOrderId == saleId && item.Kind == SaleDocumentKind.HandoverNote);
        Assert.Equal(original.Contents().Review, note.Contents().Review);
        var noteCorrection = next with
        {
            OperationId = Guid.NewGuid(),
            DocumentId = note.Id,
            ExpectedVersion = sale.Version + 1
        };
        var noteResult = await world.Services.GetRequiredService<SaleDocumentCorrectionService>()
            .ReissueAsync(world.Manager, saleId, noteCorrection, default);
        Assert.Equal(SaleResultStatus.Succeeded, noteResult.Status);
        var replays = await Task.WhenAll(
            first.ServiceProvider.GetRequiredService<SaleDocumentCorrectionService>().ReissueAsync(world.Manager, saleId, noteCorrection, default),
            second.ServiceProvider.GetRequiredService<SaleDocumentCorrectionService>().ReissueAsync(world.Manager, saleId, noteCorrection, default));
        Assert.All(replays, result => Assert.Equal(noteResult, result));
        // The intake host freezes its clock at midnight; delivery intents use the real database clock.
        var source = new SaleDeliveryMailSource(database, TimeProvider.System,
            world.Services.GetRequiredService<SaleDeliveryMailComposer>());
        Assert.NotNull(await source.ClaimNextAsync(default));

        var paymentReceipt = await database.SaleDocuments.AsNoTracking().SingleAsync(item =>
            item.SalesOrderId == saleId && item.Kind == SaleDocumentKind.PaymentReceipt);
        var atomicRequest = noteCorrection with { OperationId = Guid.NewGuid(), DocumentId = paymentReceipt.Id };
        var contents = paymentReceipt.Contents() with
        {
            Review = atomicRequest.Correction.Apply(paymentReceipt.Contents().Review)
        };
        var invalidContents = new[]
        {
            contents with { CustomerId = Guid.NewGuid() },
            contents with { Review = contents.Review with { BuyerNric = "950101101234" } },
            contents with { Review = contents.Review with { Vehicle = contents.Review.Vehicle with { Vin = "WBA12345678901234" } } },
            contents with { Sale = contents.Sale with { BalancePayable = contents.Sale.BalancePayable + 1m } },
            contents with { Sale = contents.Sale with { PaymentEvidence = contents.Sale.PaymentEvidence! with { ReceivedAmount = 1m } } },
            contents with { Sale = contents.Sale with { HandoverEvidence = new(100000, 3, true, true, true) } }
        };
        foreach (var invalid in invalidContents)
        {
            await RejectReplacementAsync(world.Manager, saleId, paymentReceipt,
                atomicRequest with { OperationId = Guid.NewGuid() }, invalid);
        }

        await using var transaction = await database.Database.BeginTransactionAsync();
        var moment = await database.Database.SqlQueryRaw<DateTimeOffset>("SELECT clock_timestamp() AS \"Value\"").SingleAsync();
        database.SaleDocumentCorrections.Add(new(saleId, world.Manager, atomicRequest, moment));
        await database.SaveChangesAsync();
        var failure = await Assert.ThrowsAsync<PostgresException>(() => transaction.CommitAsync());
        Assert.Equal("CK_SaleDocumentCorrections_Replacement", failure.ConstraintName);
    }

    private async Task RejectReplacementAsync(Guid actorId, Guid saleId, SaleDocument original,
        SaleDocumentCorrectionRequest request, SaleDocumentContents contents)
    {
        await using var database = NewContext();
        await using var transaction = await database.Database.BeginTransactionAsync();
        var moment = await database.Database.SqlQueryRaw<DateTimeOffset>("SELECT clock_timestamp() AS \"Value\"").SingleAsync();
        database.SaleDocumentCorrections.Add(new(saleId, actorId, request, moment));
        database.SaleDocuments.Add(new(saleId, original.IssuingOperationId, original.Kind, "VRC-2099-999999", moment,
            contents, request.OperationId));
        await database.SaveChangesAsync();
        var failure = await Assert.ThrowsAsync<PostgresException>(() => transaction.CommitAsync());
        Assert.Equal("CK_SaleDocuments_Correction", failure.ConstraintName);
    }

    private async Task<(Guid SaleId, SaleDocument Document, SaleDocumentCorrectionRequest Request)> PrepareAsync(
        SaleWorld world, bool deliver)
    {
        var hold = await world.HoldAsync();
        Assert.Equal(SaleResultStatus.Succeeded, (await world.Handling.ClaimAsync(world.Agent, hold.Id, new(Guid.NewGuid(), 1), default)).Status);
        Assert.Equal(SaleResultStatus.Succeeded, (await world.Terms.SubmitAsync(world.Agent, hold.Id, new(Guid.NewGuid(), 2), default)).Status);
        await using var database = NewContext();
        var sale = await database.SalesOrders.AsNoTracking().SingleAsync(item => item.Id == hold.Id);
        var profile = await database.CustomerProfiles.SingleAsync(item => item.UserId == sale.CustomerId);
        var vehicle = await database.Vehicles.SingleAsync(item => item.Id == sale.VehicleId);
        var review = new SaleSettlementReview(profile.LegalName, profile.Nric, profile.ResidentialAddress, SaleReviewedVehicle.Of(vehicle));
        var payment = new SalePaymentEvidence(SalePaymentMethod.Cash, sale.BalancePayable, "CASH 43879", new DateOnly(2026, 11, 2),
            null, 0m, null, false, false);
        Assert.Equal(SaleResultStatus.Succeeded, (await world.Settlement.SettleAsync(world.Agent, hold.Id,
            new(Guid.NewGuid(), 3, payment, review), default)).Status);
        if (deliver)
        {
            Assert.Equal(SaleResultStatus.Succeeded, (await world.Services.GetRequiredService<SaleHandoverService>()
                .DeliverAsync(world.Agent, hold.Id, new(Guid.NewGuid(), 4, new(vehicle.MileageKilometres, 2, true, true, true)), default)).Status);
        }

        var original = await database.SaleDocuments.AsNoTracking().SingleAsync(item =>
            item.SalesOrderId == hold.Id && item.Kind == SaleDocumentKind.PurchaseOrder);
        return (hold.Id, original, new(Guid.NewGuid(), deliver ? 5 : 4, original.Id,
            new(review.BuyerName, "27 Jalan Meranti, Petaling Jaya, Selangor", "Correct buyer address spelling")));
    }

    private async Task LoginAsync(HttpClient client, Guid actorId)
    {
        await using var database = NewContext();
        var email = await database.Users.Where(item => item.Id == actorId).Select(item => item.Email).SingleAsync();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(Guid.NewGuid(), email!, "Password123!"));
        var session = (await response.Content.ReadFromJsonAsync<LoginSession>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
    }

    private async Task RejectSqlAsync(string sql, Guid id)
    {
        await using var connection = new NpgsqlConnection(fixture.Database.RuntimeConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        var failure = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
    }

    private DealershipDbContext NewContext() => new(new DbContextOptionsBuilder<DealershipDbContext>()
        .UseNpgsql(fixture.Database.RuntimeConnectionString).Options);
}
