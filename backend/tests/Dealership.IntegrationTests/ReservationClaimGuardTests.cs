using Dealership.Application.Accounts;
using Dealership.Application.Sales;
using Dealership.Application.Vehicles;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static Dealership.IntegrationTests.ReservationTestData;

namespace Dealership.IntegrationTests;

public sealed class ReservationClaimGuardTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{    [Fact]
    public async Task TheDatabaseRejectsClaimChangesThatSkipTheRetainedHistory()
    {
        using var data = await CheckInTestData.CreateAsync(fixture);
        using var scope = data.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var manager = await ProvisionAsync(services, data.BranchId, AccountRoles.BranchManager);
        var technician = await ProvisionAsync(services, data.BranchId, AccountRoles.WorkshopTechnician);
        var agent = await ProvisionAsync(services, data.BranchId, AccountRoles.SalesAgent);
        var listing = await CreateListingAsync(data, services, manager, technician, 0);
        await using var database = NewContext();
        database.PurchaseSettings.Add(new(500m, 0.08m, manager, DateTimeOffset.UtcNow));
        await database.SaveChangesAsync();
        var customer = await RegisterCustomerAsync(services, database);
        var held = await HoldAsync(data, customer, listing.Publication);
        var deals = services.GetRequiredService<DealService>();
        Assert.Equal(DealStatus.Succeeded, (await deals.ClaimAsync(agent, held.Id, new(Guid.NewGuid()), default)).Status);

        // A claim fact cannot move without a matching event, and a recorded event cannot be rewritten or removed.
        await RejectedAsync($"UPDATE \"Reservations\" SET \"ClaimedById\" = NULL, \"ClaimedAt\" = NULL, \"FrozenRemaining\" = NULL WHERE \"Id\" = '{held.Id}'",
            "CK_Reservations_ClaimEvents");
        await RejectedAsync($"UPDATE \"Reservations\" SET \"ClaimedById\" = '{technician}' WHERE \"Id\" = '{held.Id}'",
            "CK_Reservations_Claim");
        await RejectedAsync($"UPDATE \"Reservations\" SET \"ExpiresAt\" = \"ExpiresAt\" + interval '1 day' WHERE \"Id\" = '{held.Id}'",
            "CK_Reservations_Immutable");
        await RejectedAsync($"UPDATE \"Reservations\" SET \"Deposit\" = 1 WHERE \"Id\" = '{held.Id}'", "CK_Reservations_Immutable");
        await RejectedAsync("UPDATE \"ReservationEvents\" SET \"RecordedAtUtc\" = now()", null, PostgresErrorCodes.RestrictViolation);
        await RejectedAsync("DELETE FROM \"ReservationEvents\"", null, PostgresErrorCodes.InsufficientPrivilege);
        await RejectedAsync($"INSERT INTO \"ReservationEvents\" (\"Id\", \"ReservationId\", \"Sequence\", \"Kind\", \"ActorId\", \"AgentId\", \"PreviousAgentId\", \"RecordedAtUtc\") VALUES (gen_random_uuid(), '{held.Id}', 5, 'Released', '{agent}', NULL, '{agent}', now())",
            "CK_ReservationEvents_History");
        await RejectedAsync($"INSERT INTO \"ReservationEvents\" (\"Id\", \"ReservationId\", \"Sequence\", \"Kind\", \"ActorId\", \"AgentId\", \"PreviousAgentId\", \"RecordedAtUtc\") VALUES (gen_random_uuid(), '{held.Id}', 2, 'Reassigned', '{technician}', '{technician}', '{agent}', now())",
            "CK_ReservationEvents_Actor");
        // A claim must come from a sales agent of the hold's branch, with the frozen time it had left.
        await RejectedAsync("UPDATE \"Reservations\" SET \"FrozenRemaining\" = interval '49 hours' WHERE \"ClaimedById\" IS NOT NULL",
            "CK_Reservations_Immutable");
    }

    private static async Task<Reservation> HoldAsync(CheckInTestData data, Guid customerId, ListingPublication publication)
    {
        using var scope = data.Factory.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<ReservationService>().ReserveAsync(customerId,
            publication.VehicleId, new(Guid.NewGuid(), publication.Id, null), CancellationToken.None);
        Assert.Equal(ReservationStatus.Succeeded, result.Status);
        return result.Reservation!;
    }

    private async Task RejectedAsync(string sql, string? constraint, string? state = null)
    {
        await using var connection = new NpgsqlConnection(fixture.Database.RuntimeConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        if (constraint is not null)
        {
            Assert.Equal(constraint, error.ConstraintName);
        }

        if (state is not null)
        {
            Assert.Equal(state, error.SqlState);
        }
    }

    private DealershipDbContext NewContext() => new(new DbContextOptionsBuilder<DealershipDbContext>()
        .UseNpgsql(fixture.Database.RuntimeConnectionString).Options);
}
