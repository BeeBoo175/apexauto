using Dealership.Application.Accounts;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static Dealership.IntegrationTests.ReservationTestData;

namespace Dealership.IntegrationTests;

// Its own class: the check-in scenario provisions a branch with a name that is unique per class database.
public sealed class AdversarialMediaBypassTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task RemovalReceiptsForAttachedEvidenceNeedTheirOwnReasonAndCannotBeRewrittenOrDeleted()
    {
        // Guarantee: the database refuses a removal receipt whose reason does not match the photograph's state, so
        // attached signed or checked-in evidence cannot be queued as "unattached", or as an erased owner's upload.
        using var data = await CheckInTestData.CreateAsync(fixture);
        using var scope = data.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var manager = await ProvisionAsync(services, data.BranchId, AccountRoles.BranchManager);
        var technician = await ProvisionAsync(services, data.BranchId, AccountRoles.WorkshopTechnician);
        await using var database = new DealershipDbContext(new DbContextOptionsBuilder<DealershipDbContext>()
            .UseNpgsql(fixture.Database.RuntimeConnectionString).Options);
        database.PurchaseSettings.Add(new(500m, 0.08m, manager, DateTimeOffset.UtcNow));
        await database.SaveChangesAsync();
        await CreateListingAsync(data, services, manager, technician, 0, withInspectionEvidence: true);

        var inspectionPhoto = await database.Database.SqlQueryRaw<Guid>(
            "SELECT \"PhotoId\" AS \"Value\" FROM \"InspectionDraftPhotos\" LIMIT 1").FirstAsync();
        var checkInPhoto = await database.Database.SqlQueryRaw<Guid>(
            "SELECT a.\"PhotoId\" AS \"Value\" FROM \"AppointmentPhotos\" a JOIN \"AppointmentCheckIns\" k " +
            "ON k.\"AppointmentId\" = a.\"AppointmentId\" LIMIT 1").FirstAsync();

        var probes = new[]
        {
            $"INSERT INTO \"InspectionPhotoRemovals\" (\"PhotoId\", \"Reason\", \"EligibleAt\", \"RequestedAt\", \"Attempts\", \"NextAttemptAt\") " +
                $"VALUES ('{inspectionPhoto}', 'UnattachedExpired', now(), now(), 0, now())",
            $"INSERT INTO \"InspectionPhotoRemovals\" (\"PhotoId\", \"Reason\", \"EligibleAt\", \"RequestedAt\", \"Attempts\", \"NextAttemptAt\") " +
                $"VALUES ('{inspectionPhoto}', 'OwnerErased', now(), now(), 0, now())",
            $"INSERT INTO \"InspectionPhotoRemovals\" (\"PhotoId\", \"Reason\", \"EligibleAt\", \"RequestedAt\", \"Attempts\", \"NextAttemptAt\") " +
                $"VALUES ('{Guid.NewGuid()}', 'RetentionExpired', now(), now(), 0, now())",
            $"INSERT INTO \"PhotoRemovals\" (\"PhotoId\", \"Reason\", \"EligibleAt\", \"RequestedAt\", \"Attempts\", \"NextAttemptAt\") " +
                $"VALUES ('{checkInPhoto}', 'UnattachedExpired', now(), now(), 0, now())",
            $"INSERT INTO \"PhotoRemovals\" (\"PhotoId\", \"Reason\", \"EligibleAt\", \"RequestedAt\", \"Attempts\", \"NextAttemptAt\") " +
                $"VALUES ('{checkInPhoto}', 'OwnerErased', now(), now(), 0, now())",
            $"INSERT INTO \"PhotoRemovals\" (\"PhotoId\", \"Reason\", \"EligibleAt\", \"RequestedAt\", \"Attempts\", \"NextAttemptAt\") " +
                $"VALUES ('{Guid.NewGuid()}', 'RetentionExpired', now(), now(), 0, now())"
        };

        foreach (var sql in probes)
        {
            Assert.NotNull(await FailureAsync(sql));
        }

        Assert.Equal(0, await CountAsync(database, "InspectionPhotoRemovals"));
        Assert.Equal(0, await CountAsync(database, "PhotoRemovals"));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, await FailureAsync("DELETE FROM \"PhotoRemovals\""));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, await FailureAsync("DELETE FROM \"InspectionPhotoRemovals\""));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, await FailureAsync("TRUNCATE \"PhotoRemovals\""));
    }

    private static Task<int> CountAsync(DealershipDbContext database, string table) => table == "PhotoRemovals"
        ? database.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM \"PhotoRemovals\"").SingleAsync()
        : database.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM \"InspectionPhotoRemovals\"").SingleAsync();

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
}
