using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dealership.Application.Accounts;
using Dealership.Application.Sales;
using Dealership.Domain.Sales;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace Dealership.IntegrationTests;

public sealed class SaleHandoverTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task HandoverRequiresCurrentAgentAndPhysicalEvidenceAndRetainsExactReplayAcrossRestart()
    {
        var world = await SaleWorld.SharedAsync(fixture);
        var (held, request) = await SettledAsync(world);
        using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var path = $"/api/staff/sales/{held.Id}/handovers";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(path, request)).StatusCode);
        await LoginAsync(client, world.Manager);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(path, request)).StatusCode);
        var service = world.Services.GetRequiredService<SaleHandoverService>();
        foreach (var actor in new[] { world.Colleague, world.OtherBranchAgent, held.CustomerId })
        {
            Assert.NotEqual(SaleResultStatus.Succeeded,
                (await service.DeliverAsync(actor, held.Id, request, default)).Status);
        }

        var badEvidence = new[]
        {
            request.Evidence with { OdometerKilometres = request.Evidence.OdometerKilometres - 101 },
            request.Evidence with { KeysDelivered = -1 },
            request.Evidence with { CustomerAcknowledged = false },
            request.Evidence with { OwnershipTransferCompleted = false },
            request.Evidence with { InsuranceConfirmed = false }
        };
        foreach (var evidence in badEvidence)
        {
            Assert.Equal(SaleResultStatus.Invalid,
                (await service.DeliverAsync(world.Agent, held.Id, request with { Evidence = evidence }, default)).Status);
        }

        Assert.Equal(SaleResultStatus.Conflict, (await service.DeliverAsync(world.Agent, held.Id,
            request with { ExpectedVersion = request.ExpectedVersion - 1 }, default)).Status);
        await using (var changed = NewContext())
        {
            await changed.Branches.Where(item => item.Id == held.BranchId)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.IsActive, false));
            try
            {
                Assert.Equal(SaleResultStatus.Forbidden,
                    (await service.DeliverAsync(world.Agent, held.Id, request, default)).Status);
            }
            finally
            {
                await changed.Branches.Where(item => item.Id == held.BranchId)
                    .ExecuteUpdateAsync(update => update.SetProperty(item => item.IsActive, true));
            }

            await changed.CustomerProfiles.Where(item => item.UserId == held.CustomerId)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.ResidentialAddress, "27 Jalan Meranti, Shah Alam"));
        }

        Assert.Equal(SaleResultStatus.Succeeded, (await world.Handling.AssignAsync(world.Manager, held.Id,
            new(Guid.NewGuid(), request.ExpectedVersion, world.Colleague), default)).Status);
        Assert.Equal(SaleResultStatus.NotFound,
            (await service.DeliverAsync(world.Agent, held.Id, request, default)).Status);
        request = request with { ExpectedVersion = request.ExpectedVersion + 1 };
        await LoginAsync(client, world.Colleague);
        var omittedKeys = new
        {
            request.OperationId,
            request.ExpectedVersion,
            Evidence = new
            {
                request.Evidence.OdometerKilometres,
                request.Evidence.CustomerAcknowledged,
                request.Evidence.OwnershipTransferCompleted,
                request.Evidence.InsuranceConfirmed
            }
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, omittedKeys)).StatusCode);
        var response = await client.PostAsJsonAsync(path, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        var receipt = (await response.Content.ReadFromJsonAsync<SaleReceipt>())!;
        Assert.Equal("Delivered", receipt.Status);
        Assert.Equal("Delivered", receipt.Kind);

        await using var database = NewContext();
        var sale = await database.SalesOrders.AsNoTracking().SingleAsync(item => item.Id == held.Id);
        Assert.True(sale.IsClosed);
        Assert.Null(sale.PausedSeconds);
        Assert.Equal(request.Evidence, sale.HandoverEvidence);
        Assert.Equal(receipt.RecordedAt, sale.DeliveredAt);
        Assert.Equal(sale.DeliveredAt, sale.ClosedAt);
        var vehicle = await database.Vehicles.AsNoTracking().SingleAsync(item => item.Id == held.VehicleId);
        Assert.Equal(VehicleStatus.Delivered, vehicle.Status);
        Assert.Equal(request.Evidence.OdometerKilometres, vehicle.MileageKilometres);
        var documents = await database.SaleDocuments.AsNoTracking().Where(item => item.SalesOrderId == held.Id).ToListAsync();
        Assert.Equal(3, documents.Count);
        var note = documents.Single(item => item.Kind == SaleDocumentKind.HandoverNote);
        var order = documents.Single(item => item.Kind == SaleDocumentKind.PurchaseOrder);
        Assert.StartsWith("VHN-", note.Number);
        Assert.Equal(request.OperationId, note.IssuingOperationId);
        Assert.Equal(order.Contents().Review, note.Contents().Review);
        Assert.Equal(SalesOrderStatus.Settled, order.Contents().Sale.Status);
        Assert.Equal(request.Evidence, note.Contents().Sale.HandoverEvidence);
        Assert.Equal(sale.PaymentEvidence, order.Contents().Sale.PaymentEvidence);
        Assert.False(await database.ReservationReleases.AnyAsync(item => item.ReservationId == held.Id));
        Assert.False((await database.Reservations.SingleAsync(item => item.Id == held.Id)).IsActive);

        using var restarted = fixture.CreateFactory();
        using var scope = restarted.Services.CreateScope();
        var replay = scope.ServiceProvider.GetRequiredService<SaleHandoverService>();
        Assert.Equal(receipt, (await replay.DeliverAsync(world.Colleague, held.Id, request, default)).Receipt);
        Assert.Equal(SaleResultStatus.Conflict, (await replay.DeliverAsync(world.Colleague, held.Id,
            request with { Evidence = request.Evidence with { KeysDelivered = 3 } }, default)).Status);
        Assert.Equal(SaleResultStatus.Conflict, (await world.Handling.AssignAsync(world.Manager, held.Id,
            new(Guid.NewGuid(), sale.Version, world.Agent), default)).Status);
        Assert.Equal(SaleResultStatus.Conflict, (await world.Terms.CancelAsync(world.Manager, held.Id,
            new(Guid.NewGuid(), sale.Version, "Buyer changed their mind"), default)).Status);
        var reads = world.Services.GetRequiredService<ISaleDocumentStore>();
        Assert.Equal(3, (await reads.ReadAsync(held.CustomerId, held.Id, default)).Value!.Count);
        Assert.Equal(SaleResultStatus.NotFound, (await reads.ReadAsync(world.Agent, held.Id, default)).Status);
        await database.Users.Where(item => item.Id == world.Colleague)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.IsActive, false));
        try
        {
            Assert.Equal(SaleResultStatus.Forbidden,
                (await replay.DeliverAsync(world.Colleague, held.Id, request, default)).Status);
        }
        finally
        {
            await database.Users.Where(item => item.Id == world.Colleague)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.IsActive, true));
        }

        await RejectSqlAsync("UPDATE \"SalesOrders\" SET \"Version\" = \"Version\" + 1 WHERE \"Id\" = @id", held.Id);
        await RejectSqlAsync("UPDATE \"SaleDocuments\" SET \"ContentsJson\" = '{}' WHERE \"Id\" = @id", note.Id);
        await RejectSqlAsync("UPDATE \"Vehicles\" SET \"Status\" = 'Sold' WHERE \"Id\" = @id", held.VehicleId);
        await RejectSqlAsync("UPDATE \"SaleDeliveryMailOutbox\" SET \"RecipientEmail\" = 'other@example.invalid' WHERE \"SalesOrderId\" = @id", held.Id);
    }

    [Fact]
    public async Task ConcurrentDeliveryCommitsOnceAndMissingAtomicOutputsRollBack()
    {
        var world = await SaleWorld.SharedAsync(fixture);
        var (held, request) = await SettledAsync(world);
        using var first = world.Services.CreateScope();
        using var second = world.Services.CreateScope();
        var outcomes = await Task.WhenAll(
            first.ServiceProvider.GetRequiredService<SaleHandoverService>().DeliverAsync(world.Agent, held.Id, request, default),
            second.ServiceProvider.GetRequiredService<SaleHandoverService>().DeliverAsync(world.Agent, held.Id,
                request with { OperationId = Guid.NewGuid() }, default));
        Assert.Single(outcomes, item => item.Status == SaleResultStatus.Succeeded);
        Assert.Single(outcomes, item => item.Status == SaleResultStatus.Conflict);
        await using var database = NewContext();
        // Settlement queued one mail and the single committed handover queued the other.
        Assert.Equal(2, await database.SaleDeliveryMailOutbox.CountAsync(item => item.SalesOrderId == held.Id));
        Assert.Equal(1, await database.SaleDocuments.CountAsync(item => item.SalesOrderId == held.Id && item.Kind == SaleDocumentKind.HandoverNote));
        var (competing, competingRequest) = await SettledAsync(world);
        var race = await Task.WhenAll(
            first.ServiceProvider.GetRequiredService<SaleHandoverService>().DeliverAsync(world.Agent, competing.Id,
                competingRequest, default),
            second.ServiceProvider.GetRequiredService<SaleHandlingService>().AssignAsync(world.Manager, competing.Id,
                new(Guid.NewGuid(), competingRequest.ExpectedVersion, world.Colleague), default));
        Assert.Single(race, item => item.Status == SaleResultStatus.Succeeded);
        var racedSale = await database.SalesOrders.AsNoTracking().SingleAsync(item => item.Id == competing.Id);
        Assert.Equal(racedSale.Status == SalesOrderStatus.Delivered ? 2 : 1,
            await database.SaleDeliveryMailOutbox.CountAsync(item => item.SalesOrderId == competing.Id));
        var (rollback, rollbackRequest) = await SettledAsync(world);
        foreach (var includeNote in new[] { false, true })
        {
            await using var transaction = await database.Database.BeginTransactionAsync();
            var sale = await database.SalesOrders.SingleAsync(item => item.Id == rollback.Id);
            var entry = sale.Deliver(Guid.NewGuid(), rollbackRequest.Evidence, rollbackRequest.Evidence.OdometerKilometres,
                world.Agent, DateTimeOffset.UtcNow);
            database.SalesOrderEvents.Add(entry);
            if (includeNote)
            {
                var original = await database.SaleDocuments.SingleAsync(item => item.SalesOrderId == rollback.Id &&
                    item.Kind == SaleDocumentKind.PurchaseOrder);
                database.SaleDocuments.Add(new SaleDocument(sale.Id, entry.Id, SaleDocumentKind.HandoverNote,
                    "VHN-2099-999999", entry.RecordedAt, original.Contents() with { Sale = entry.Snapshot }));
            }

            await database.SaveChangesAsync();
            var failure = await Assert.ThrowsAsync<PostgresException>(() => transaction.CommitAsync());
            Assert.Equal("CK_SalesOrders_HandoverReceipt", failure.ConstraintName);
            database.ChangeTracker.Clear();
        }

        Assert.Equal(SalesOrderStatus.Settled, (await database.SalesOrders.SingleAsync(item => item.Id == rollback.Id)).Status);
        Assert.Equal(VehicleStatus.Sold, (await database.Vehicles.SingleAsync(item => item.Id == rollback.VehicleId)).Status);
        Assert.Equal(1, await database.SaleDeliveryMailOutbox.CountAsync(item => item.SalesOrderId == rollback.Id));
        await RejectSqlAsync("""
            WITH moment AS (SELECT clock_timestamp() AS now)
            UPDATE "SalesOrders" SET "Status" = 'Delivered', "Version" = "Version" + 1,
                "DeliveredAt" = moment.now, "ClosedAt" = moment.now, "UpdatedAt" = moment.now,
                "HandoverEvidence" = '{"OdometerKilometres":0,"KeysDelivered":2,"CustomerAcknowledged":true,
                    "OwnershipTransferCompleted":true,"InsuranceConfirmed":true}'::jsonb
            FROM moment WHERE "Id" = @id
            """, rollback.Id, "CK_SalesOrders_HandoverEvidence");
    }

    [Fact]
    public async Task DurableMailFailureDoesNotUndoDeliveryAndClaimsRecoverAfterRestart()
    {
        var world = await SaleWorld.SharedAsync(fixture);
        var (held, request) = await SettledAsync(world);
        Assert.Equal(SaleResultStatus.Succeeded, (await world.Services.GetRequiredService<SaleHandoverService>()
            .DeliverAsync(world.Agent, held.Id, request, default)).Status);
        await QuietOtherMailAsync(request.OperationId);
        using var factory = fixture.CreateFactory();
        var transport = new RecordingMailTransport(fail: true);
        using var worker = new MailDeliveryWorker(factory.Services.GetRequiredService<IServiceScopeFactory>(), transport,
            new MailDeliverySignal(), NullLogger<MailDeliveryWorker>.Instance);
        await worker.DeliverBatchAsync(default);
        await using var database = NewContext();
        var intent = await database.SaleDeliveryMailOutbox.AsNoTracking()
            .SingleAsync(item => item.DeliveryOperationId == request.OperationId);
        Assert.Null(intent.DeliveredAt);
        Assert.Equal(1, intent.DeliveryAttempts);
        Assert.Equal(SalesOrderStatus.Delivered, (await database.SalesOrders.SingleAsync(item => item.Id == held.Id)).Status);
        var clock = new DeliveryClock(intent.NextDeliveryAt.AddSeconds(1));
        using var restarted = fixture.CreateFactory();
        using var scope = restarted.Services.CreateScope();
        var composer = scope.ServiceProvider.GetRequiredService<SaleDeliveryMailComposer>();
        Assert.Contains(scope.ServiceProvider.GetServices<IMailDeliverySource>(), item => item is SaleDeliveryMailSource);
        var source = new SaleDeliveryMailSource(database, clock, composer);
        var claim = Assert.IsType<ClaimedMail>(await source.ClaimNextAsync(default));
        Assert.Equal(held.Buyer.Email, claim.Message.Recipient);
        Assert.Contains($"/account/sales/{held.Id}/documents", claim.Message.Body);
        Assert.DoesNotContain(held.Buyer.Nric!, claim.Message.Body);
        Assert.NotEqual(intent.DeliveryClaimId, claim.ClaimId);
        await source.MarkDeliveredAsync(intent.Id, intent.DeliveryClaimId!.Value, default);
        Assert.Null(await database.SaleDeliveryMailOutbox.Where(item => item.Id == intent.Id).Select(item => item.DeliveredAt).SingleAsync());
        await source.MarkDeliveredAsync(claim.Id, claim.ClaimId, default);
        Assert.NotNull(await database.SaleDeliveryMailOutbox.Where(item => item.Id == intent.Id).Select(item => item.DeliveredAt).SingleAsync());
        Assert.Null(await source.ClaimNextAsync(default));
    }

    [Fact]
    public async Task SettlementQueuesOnePaymentMail()
    {
        var world = await SaleWorld.SharedAsync(fixture);
        var (held, _) = await SettledAsync(world);
        await using var database = NewContext();
        var settled = await database.SalesOrderEvents.AsNoTracking().SingleAsync(item =>
            item.SalesOrderId == held.Id && item.Kind == SalesOrderEventKind.Settled);
        var intent = await database.SaleDeliveryMailOutbox.AsNoTracking()
            .SingleAsync(item => item.SalesOrderId == held.Id);
        Assert.Equal(settled.Id, intent.DeliveryOperationId);
        await QuietOtherMailAsync(settled.Id);
        var source = new SaleDeliveryMailSource(database, new DeliveryClock(intent.NextDeliveryAt.AddSeconds(1)),
            world.Services.GetRequiredService<SaleDeliveryMailComposer>());
        var claim = Assert.IsType<ClaimedMail>(await source.ClaimNextAsync(default));
        var receipt = await database.SaleDocuments.AsNoTracking().SingleAsync(item =>
            item.SalesOrderId == held.Id && item.Kind == SaleDocumentKind.PaymentReceipt);
        Assert.Equal(held.Buyer.Email, claim.Message.Recipient);
        Assert.Equal("ApexAuto payment confirmed", claim.Message.Subject);
        Assert.Contains(receipt.Number, claim.Message.Body);
        Assert.Contains("Bring your original MyKad.", claim.Message.Body);
        Assert.Contains($"/account/sales/{held.Id}/documents", claim.Message.Body);
        Assert.DoesNotContain(held.Buyer.Nric!, claim.Message.Body);

        // Only settlement and handover history entries may queue sale mail.
        await RejectSqlAsync("""
            INSERT INTO "SaleDeliveryMailOutbox" ("Id", "SalesOrderId", "DeliveryOperationId", "RecipientEmail",
                "DeliveryAttempts", "NextDeliveryAt")
            SELECT gen_random_uuid(), e."SalesOrderId", e."Id", r."BuyerEmail", 0, e."RecordedAt"
            FROM "SalesOrderEvents" e JOIN "Reservations" r ON r."Id" = e."SalesOrderId"
            WHERE e."SalesOrderId" = @id AND e."Kind" = 'Submitted'
            """, held.Id, "CK_SaleDeliveryMailOutbox_Delivery");
    }

    // Sale mail left pending by other cases in the shared database would be claimed ahead of the one under test.
    private async Task QuietOtherMailAsync(Guid keptOperationId)
    {
        await using var database = NewContext();
        var now = DateTimeOffset.UtcNow;
        await database.SaleDeliveryMailOutbox
            .Where(item => item.DeliveryOperationId != keptOperationId && item.DeliveredAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.DeliveredAt, now));
    }

    private async Task<(Reservation Hold, SaleHandoverRequest Request)> SettledAsync(SaleWorld world)
    {
        var held = await world.HoldAsync();
        Assert.Equal(SaleResultStatus.Succeeded, (await world.Handling.ClaimAsync(world.Agent, held.Id, new(Guid.NewGuid(), 1), default)).Status);
        Assert.Equal(SaleResultStatus.Succeeded, (await world.Terms.SubmitAsync(world.Agent, held.Id, new(Guid.NewGuid(), 2), default)).Status);
        await using var database = NewContext();
        var sale = await database.SalesOrders.AsNoTracking().SingleAsync(item => item.Id == held.Id);
        var profile = await database.CustomerProfiles.SingleAsync(item => item.UserId == sale.CustomerId);
        var vehicle = await database.Vehicles.SingleAsync(item => item.Id == sale.VehicleId);
        var review = SaleSettlementReview.From(profile, SaleReviewedVehicle.Of(vehicle));
        var payment = new SalePaymentEvidence(SalePaymentMethod.Cash, sale.BalancePayable, "CASH 43879", new DateOnly(2026, 11, 2),
            null, 0m, null, false, false);
        Assert.Equal(SaleResultStatus.Succeeded, (await world.Settlement.SettleAsync(world.Agent, held.Id,
            new(Guid.NewGuid(), 3, payment, review), default)).Status);
        return (held, new(Guid.NewGuid(), 4, new(vehicle.MileageKilometres + 100, 2, true, true, true)));
    }

    private async Task LoginAsync(HttpClient client, Guid actorId)
    {
        await using var database = NewContext();
        var email = await database.Users.Where(item => item.Id == actorId).Select(item => item.Email).SingleAsync();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(Guid.NewGuid(), email!, "Password123!"));
        var session = (await response.Content.ReadFromJsonAsync<LoginSession>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
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

    private sealed class DeliveryClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
