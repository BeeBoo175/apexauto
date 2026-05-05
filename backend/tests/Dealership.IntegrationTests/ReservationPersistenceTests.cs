using Dealership.Domain.Sales;
using static Dealership.IntegrationTests.ReservationTestData;
using System.Text.Json;
using Dealership.Application.Accounts;
using Dealership.Application.Branches;
using Dealership.Application.Inspections;
using Dealership.Application.Intake;
using Dealership.Application.Vehicles;
using Dealership.Domain.Inspections;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Dealership.IntegrationTests;

public sealed class ReservationPersistenceTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task RestrictedSqlRetainsScopedConfirmationsOneActiveSlotAndOneFullRefund()
    {
        using var data = await CheckInTestData.CreateAsync(fixture);
        using var scope = data.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var manager = await ProvisionAsync(services, data.BranchId, AccountRoles.BranchManager);
        var technician = await ProvisionAsync(services, data.BranchId, AccountRoles.WorkshopTechnician);
        var first = await CreateListingAsync(data, services, manager, technician, 0);
        var second = await CreateListingAsync(data, services, manager, technician, 1);
        await using var database = NewContext();
        var settings = new PurchaseSettings(500m, 0.08m, manager, DateTimeOffset.UtcNow);
        var promo = new PromoCode(Guid.NewGuid(), "RAYA1000", 1000m, DateTimeOffset.UtcNow.AddDays(1), true);
        database.PurchaseSettings.Add(settings);
        database.PromoCodes.Add(promo);
        await database.SaveChangesAsync();
        var profile = await database.CustomerProfiles.SingleAsync(item => item.UserId == data.OwnerId);
        var reservation = new Reservation(Guid.NewGuid(), profile, data.CustomerEmail, first.Publication,
            first.Revision, settings, promo, await NowAsync(database));
        AddConfirmed(database, Numbered(reservation));
        await database.SaveChangesAsync();

        await database.Database.ExecuteSqlInterpolatedAsync($"""
            BEGIN;
            UPDATE "Vehicles" SET "Status" = 'PendingInspection' WHERE "Id" = {reservation.VehicleId};
            UPDATE "Vehicles" SET "Status" = 'Available' WHERE "Id" = {reservation.VehicleId};
            COMMIT;
            """);
        Assert.True(await database.Reservations.Where(item => item.Id == reservation.Id).Select(item => item.IsActive).SingleAsync());
        Assert.False(await database.ReservationReleases.AnyAsync(item => item.ReservationId == reservation.Id));

        await RejectCloneAsync(reservation.Id, new { Id = Guid.NewGuid() }, PostgresErrorCodes.UniqueViolation);
        await RejectCloneAsync(reservation.Id, new
        {
            Id = Guid.NewGuid(),
            VehicleId = second.Revision.VehicleId,
            PublicationId = second.Publication.Id
        }, PostgresErrorCodes.UniqueViolation);
        await RejectCloneAsync(reservation.Id, new { Id = Guid.NewGuid(), BranchId = Guid.NewGuid() }, PostgresErrorCodes.CheckViolation);
        await RejectCloneAsync(reservation.Id, new { Id = Guid.NewGuid(), CustomerId = Guid.NewGuid() }, PostgresErrorCodes.CheckViolation);
        await RejectCloneAsync(reservation.Id, new { Id = Guid.NewGuid(), PromoCode = "raya1000" }, PostgresErrorCodes.CheckViolation);
        await RejectCloneAsync(reservation.Id, new { Id = Guid.NewGuid(), VehiclePrice = 61000m }, PostgresErrorCodes.CheckViolation);
        var wrongNric = reservation.Buyer.Nric == "940215105422" ? "940215105423" : "940215105422";
        await RejectCloneAsync(reservation.Id, new { Id = Guid.NewGuid(), BuyerNric = wrongNric }, PostgresErrorCodes.CheckViolation);
        await RejectCloneAsync(reservation.Id, new { Id = Guid.NewGuid(), Deposit = 1m }, PostgresErrorCodes.CheckViolation);
        foreach (var assignment in new[] { "\"Total\" = 1", "\"IsActive\" = false", "\"CreatedAt\" = now()", "\"CustomerId\" = gen_random_uuid()" })
        {
            await RejectAsync($"UPDATE \"Reservations\" SET {assignment} WHERE \"Id\" = '{reservation.Id}'", PostgresErrorCodes.CheckViolation);
        }

        await database.Users.Where(item => item.Id == data.OwnerId)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.EmailConfirmed, false));
        await RejectCloneAsync(reservation.Id, new { Id = Guid.NewGuid() }, PostgresErrorCodes.CheckViolation);
        await database.Users.Where(item => item.Id == data.OwnerId)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.EmailConfirmed, true));
        await VerifyForeignKeysAsync();
        await RejectAsync($"DELETE FROM \"Reservations\" WHERE \"Id\" = '{reservation.Id}'", PostgresErrorCodes.CheckViolation, maintenance: true);
        await RejectAsync($"DELETE FROM \"PromoCodes\" WHERE \"Id\" = '{promo.Id}'", PostgresErrorCodes.ForeignKeyViolation, maintenance: true);

        await database.PromoCodes.Where(item => item.Id == promo.Id)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.Discount, 2000m).SetProperty(item => item.IsActive, false));
        await database.PurchaseSettings.ExecuteUpdateAsync(update => update.SetProperty(item => item.ProcessingFee, 700m));
        var retained = await database.Reservations.AsNoTracking().SingleAsync(item => item.Id == reservation.Id);
        Assert.Equal(new PurchasePrice(60000m, 500m, 90m, 40m, 1000m, 59630m), retained.Price);
        Assert.Equal("RAYA1000", retained.PromoCode);
        Assert.Equal(reservation.Buyer, retained.Buyer);
        Assert.Equal(reservation.CreatedAt, retained.CreatedAt);
        Assert.Equal(reservation.ExpiresAt, retained.ExpiresAt);
        await RejectReleaseAsync(reservation.Id, "CustomerCancelled", data.StaffId, null);
        await RejectReleaseAsync(reservation.Id, "StaffCancelled", data.StaffId, "Outside grant");
        await RejectReleaseAsync(reservation.Id, "StaffCancelled", manager, " ");
        await RejectReleaseAsync(reservation.Id, "Expired", null, null);
        await RejectReleaseAsync(reservation.Id, "Withdrawn", null, null);
        await RejectReleaseAsync(reservation.Id, "Reinspection", null, null);
        await RejectReleaseAsync(reservation.Id, "CustomerCancelled", data.OwnerId, null, refund: 1m);

        var release = await ReleaseAsync(database, retained, ReservationReleaseKind.CustomerCancelled,
            data.OwnerId, null, await NowAsync(database));
        Assert.False(await database.Reservations.Where(item => item.Id == retained.Id).Select(item => item.IsActive).SingleAsync());
        await RejectReleaseAsync(retained.Id, "CustomerCancelled", data.OwnerId, null, state: PostgresErrorCodes.UniqueViolation);
        await RejectAsync($"UPDATE \"ReservationReleases\" SET \"Refund\" = 1 WHERE \"Id\" = '{release.Id}'", PostgresErrorCodes.CheckViolation);
        await RejectAsync($"DELETE FROM \"ReservationReleases\" WHERE \"Id\" = '{release.Id}'", PostgresErrorCodes.CheckViolation, maintenance: true);
        await RejectAsync($"UPDATE \"Reservations\" SET \"IsActive\" = true WHERE \"Id\" = '{retained.Id}'", PostgresErrorCodes.CheckViolation);

        var currentSettings = await database.PurchaseSettings.AsNoTracking().SingleAsync();
        await VerifyRacesAsync(data, services, first, second, currentSettings);
        var next = new Reservation(Guid.NewGuid(), profile, data.CustomerEmail, second.Publication,
            second.Revision, currentSettings, null, await NowAsync(database));
        AddConfirmed(database, Numbered(next));
        await database.SaveChangesAsync();
        var reopened = await services.GetRequiredService<InspectionReopeningService>().ReopenAsync(manager, second.InspectionId,
            new(Guid.NewGuid(), 1, 1, "Reinspect before confirming sale"), CancellationToken.None);
        Assert.Equal(InspectionAssignmentStatus.Succeeded, reopened.Status);
        await VerifyAutomaticReleaseAsync(next, ReservationReleaseKind.Reinspection, VehicleStatus.PendingInspection);

        var third = new Reservation(Guid.NewGuid(), profile, data.CustomerEmail, first.Publication,
            first.Revision, currentSettings, null, await NowAsync(database));
        AddConfirmed(database, Numbered(third));
        await database.SaveChangesAsync();
        var withdrawn = await services.GetRequiredService<ListingService>().WithdrawAsync(manager, first.Revision.VehicleId,
            new(Guid.NewGuid(), first.Publication.Id, "Correct public description"), CancellationToken.None);
        Assert.Equal(ListingStatus.Succeeded, withdrawn.Status);
        await VerifyAutomaticReleaseAsync(third, ReservationReleaseKind.Withdrawn, VehicleStatus.Available);
        await RejectCloneAsync(third.Id, new { Id = Guid.NewGuid() }, PostgresErrorCodes.CheckViolation);
    }

    private async Task VerifyRacesAsync(CheckInTestData data, IServiceProvider services, ListingRecord first,
        ListingRecord second, PurchaseSettings settings)
    {
        await using var database = NewContext();
        var profile = await database.CustomerProfiles.SingleAsync(item => item.UserId == data.OwnerId);
        var now = await NowAsync(database);
        var one = new Reservation(Guid.NewGuid(), profile, data.CustomerEmail, first.Publication, first.Revision, settings, null, now);
        var two = new Reservation(Guid.NewGuid(), profile, data.CustomerEmail, second.Publication, second.Revision, settings, null, now);
        var sameCustomer = await Task.WhenAll(TryInsertAsync(one), TryInsertAsync(two));
        Assert.Single(sameCustomer, success => success);
        await CancelAsync(sameCustomer[0] ? one : two);

        var email = $"hafiz.{Guid.NewGuid():N}@example.invalid";
        var registration = new RegistrationRequest(Guid.NewGuid(), "Hafiz Rahman", email, "Password123!",
            $"920418{Random.Shared.Next(100000, 1000000)}", "0135678910", "20 Jalan Mawar, Shah Alam");
        Assert.Equal(RegistrationStatus.Succeeded, (await services.GetRequiredService<RegistrationService>()
            .RegisterAsync(registration, CancellationToken.None)).Status);
        var customerId = await database.Users.Where(item => item.Email == email).Select(item => item.Id).SingleAsync();
        var challenge = await database.EmailVerificationChallenges.SingleAsync(item => item.UserId == customerId);
        var token = services.GetRequiredService<EmailVerificationToken>().Create(challenge);
        Assert.Equal(VerificationStatus.Accepted, (await services.GetRequiredService<EmailVerificationService>()
            .ConfirmAsync(token, CancellationToken.None)).Status);
        var other = await database.CustomerProfiles.SingleAsync(item => item.UserId == customerId);
        now = await NowAsync(database);
        one = new(Guid.NewGuid(), profile, data.CustomerEmail, first.Publication, first.Revision, settings, null, now);
        two = new(Guid.NewGuid(), other, email, first.Publication, first.Revision, settings, null, now);
        var sameVehicle = await Task.WhenAll(TryInsertAsync(one), TryInsertAsync(two));
        Assert.Single(sameVehicle, success => success);
        var winner = sameVehicle[0] ? one : two;
        var otherCustomer = sameVehicle[0] ? customerId : data.OwnerId;
        await RejectReleaseAsync(winner.Id, "CustomerCancelled", otherCustomer, null);
        var otherBranch = Guid.NewGuid();
        await services.GetRequiredService<BranchProvisioningService>().ProvisionBranchAsync(
            DevelopmentFixtureManifest.Branches[1] with { Id = otherBranch }, CancellationToken.None);
        var outside = await ProvisionAsync(services, otherBranch, AccountRoles.BranchManager);
        await RejectReleaseAsync(winner.Id, "StaffCancelled", outside, "Customer requested cancellation");
        await CancelAsync(winner, settings.UpdatedById);

        const decimal preciseRate = 0.1666666666666666666666666666m;
        await database.PurchaseSettings.ExecuteUpdateAsync(update => update
            .SetProperty(item => item.ProcessingFee, 0.03m).SetProperty(item => item.SstRate, preciseRate));
        var preciseSettings = await database.PurchaseSettings.AsNoTracking().SingleAsync();
        var precise = new Reservation(Guid.NewGuid(), profile, data.CustomerEmail, first.Publication, first.Revision,
            preciseSettings, null, await NowAsync(database));
        AddConfirmed(database, Numbered(precise));
        await database.SaveChangesAsync();
        Assert.Equal(0m, (await database.Reservations.AsNoTracking().SingleAsync(item => item.Id == precise.Id)).Price.ServiceTax);
        await CancelAsync(precise);
        await database.PurchaseSettings.ExecuteUpdateAsync(update => update
            .SetProperty(item => item.ProcessingFee, settings.ProcessingFee).SetProperty(item => item.SstRate, settings.SstRate));
    }

    // These tests build holds directly, so each takes the next free reference the service would have assigned.
    private static int numbers;

    private static Reservation Numbered(Reservation reservation)
    {
        if (reservation.Number.Length == 0)
        {
            reservation.AssignNumber($"VRS-2026-{Interlocked.Increment(ref numbers):D6}");
        }

        return reservation;
    }

    private async Task<bool> TryInsertAsync(Reservation reservation)
    {
        await using var database = NewContext();
        AddConfirmed(database, Numbered(reservation));
        try
        {
            await database.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return false;
        }
    }

    private async Task CancelAsync(Reservation reservation, Guid? manager = null)
    {
        await using var database = NewContext();
        var kind = manager is null ? ReservationReleaseKind.CustomerCancelled : ReservationReleaseKind.StaffCancelled;
        var reason = manager is null ? null : "Customer requested cancellation";
        await ReleaseAsync(database, reservation, kind, manager ?? reservation.CustomerId, reason,
            await NowAsync(database));
    }

    private async Task VerifyAutomaticReleaseAsync(Reservation reservation, ReservationReleaseKind kind, VehicleStatus status)
    {
        await using var restarted = NewContext();
        var retained = await restarted.Reservations.SingleAsync(item => item.Id == reservation.Id);
        Assert.False(retained.IsActive);
        Assert.Equal(reservation.Price, retained.Price);
        var refund = await restarted.ReservationReleases.SingleAsync(item => item.ReservationId == reservation.Id);
        Assert.Equal(kind, refund.Kind);
        Assert.Equal(500m, refund.Refund);
        Assert.Null(refund.ActorId);
        Assert.Equal(status, await restarted.Vehicles.Where(item => item.Id == reservation.VehicleId)
            .Select(item => item.Status).SingleAsync());
        await restarted.Database.ExecuteSqlInterpolatedAsync($"SELECT release_unavailable_reservation({reservation.VehicleId})");
        Assert.Equal(1, await restarted.ReservationReleases.CountAsync(item => item.ReservationId == reservation.Id));
    }

    private async Task VerifyForeignKeysAsync()
    {
        await using var connection = new NpgsqlConnection(fixture.Database.RuntimeConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT count(*) FROM pg_constraint WHERE contype = 'f' AND
                conrelid IN ('"Reservations"'::regclass, '"ReservationReleases"'::regclass) AND confdeltype = 'r'
            """, connection);
        Assert.Equal(5L, await command.ExecuteScalarAsync());
        command.CommandText = """
            SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conrelid = '"Reservations"'::regclass
                AND confrelid = '"ListingPublications"'::regclass
            """;
        Assert.Contains("FOREIGN KEY (\"PublicationId\", \"VehicleId\", \"BranchId\")", (string)(await command.ExecuteScalarAsync())!);
    }

    private async Task RejectCloneAsync(Guid source, object patch, string state)
    {
        await using var connection = new NpgsqlConnection(fixture.Database.RuntimeConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            INSERT INTO "Reservations" SELECT (jsonb_populate_record(NULL::"Reservations", to_jsonb(r) || @patch::jsonb)).*
                FROM "Reservations" r WHERE "Id" = @id
            """, connection);
        command.Parameters.AddWithValue("id", source);
        command.Parameters.AddWithValue("patch", JsonSerializer.Serialize(patch));
        var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(state, error.SqlState);
    }

    // Confirms the way the reservation service does: the hold and its open sale in one save.
    private static void AddConfirmed(DealershipDbContext database, Reservation reservation)
    {
        database.Reservations.Add(reservation);
        var sale = SalesOrder.Open(reservation, Guid.NewGuid(), out var created);
        database.SalesOrders.Add(sale);
        database.SalesOrderEvents.Add(created);
    }

    private async Task RejectReleaseAsync(Guid reservationId, string kind, Guid? actor, string? reason,
        decimal refund = 500m, string state = PostgresErrorCodes.CheckViolation)
    {
        await using var connection = new NpgsqlConnection(fixture.Database.RuntimeConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            INSERT INTO "ReservationReleases" ("Id", "ReservationId", "Kind", "ActorId", "Reason", "RecordedAt", "Refund")
                VALUES (gen_random_uuid(), @id, @kind, @actor, @reason, clock_timestamp(), @refund)
            """, connection);
        command.Parameters.AddWithValue("id", reservationId);
        command.Parameters.AddWithValue("kind", kind);
        command.Parameters.AddWithValue("actor", NpgsqlTypes.NpgsqlDbType.Uuid, (object?)actor ?? DBNull.Value);
        command.Parameters.AddWithValue("reason", NpgsqlTypes.NpgsqlDbType.Text, (object?)reason ?? DBNull.Value);
        command.Parameters.AddWithValue("refund", refund);
        var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(state, error.SqlState);
    }

    private async Task RejectAsync(string sql, string state, bool maintenance = false)
    {
        await using var connection = new NpgsqlConnection(maintenance ? fixture.Database.ConnectionString : fixture.Database.RuntimeConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(state, error.SqlState);
    }

    private DealershipDbContext NewContext() => new(new DbContextOptionsBuilder<DealershipDbContext>()
        .UseNpgsql(fixture.Database.RuntimeConnectionString).Options);

    private static Task<DateTimeOffset> NowAsync(DealershipDbContext database) =>
        database.Database.SqlQuery<DateTimeOffset>($"SELECT clock_timestamp() AS \"Value\"").SingleAsync();

}
