using Dealership.Application.Accounts;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Photos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using static Dealership.IntegrationTests.ReservationTestData;
using static Dealership.IntegrationTests.RetentionMinimizationTests;

namespace Dealership.IntegrationTests;

// Its own class: the check-in scenario provisions a branch with a name that is unique per class database.
public sealed class RetentionMediaDisposalTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task SignedInspectionAndCheckInPhotographsAreQueuedForRemovalOnlyAfterTheDeadline()
    {
        using var data = await CheckInTestData.CreateAsync(fixture);
        using var scope = data.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var manager = await ProvisionAsync(services, data.BranchId, AccountRoles.BranchManager);
        var technician = await ProvisionAsync(services, data.BranchId, AccountRoles.WorkshopTechnician);
        await using var database = NewContext();
        database.PurchaseSettings.Add(new(500m, 0.08m, manager, DateTimeOffset.UtcNow));
        await database.SaveChangesAsync();
        await CreateListingAsync(data, services, manager, technician, 0, withInspectionEvidence: true);

        await Worker(data.Factory).RunBatchAsync(CancellationToken.None);
        Assert.Equal(0, await RemovalCountAsync(database));
        Assert.Equal(0, await RemovedIdentityCountAsync(database));

        await using var owner = new NpgsqlConnection(fixture.Database.ConnectionString);
        await owner.OpenAsync();
        await AgeAsync(owner, "6 years");
        await Worker(data.Factory).RunBatchAsync(CancellationToken.None);
        Assert.Equal(0, await RemovalCountAsync(database));
        Assert.Equal(0, await RemovedIdentityCountAsync(database));

        await AgeAsync(owner, "2 years");
        // Other columns and other keys of a signed revision stay immutable for the runtime role.
        Assert.Equal(PostgresErrorCodes.CheckViolation, await FailureAsync(database,
            "UPDATE \"InspectionSignedRevisions\" SET \"Revision\" = \"Revision\" + 1"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, await FailureAsync(database,
            "UPDATE \"InspectionSignedRevisions\" SET \"ReportJson\" = \"ReportJson\" - 'vin'"));
        await Worker(data.Factory).RunBatchAsync(CancellationToken.None);
        Assert.True(await RemovalCountAsync(database) > 0);
        Assert.True(await database.Database.SqlQueryRaw<int>(
            "SELECT count(*)::int AS \"Value\" FROM \"InspectionPhotoRemovals\" WHERE \"Reason\" = 'RetentionExpired'")
            .SingleAsync() > 0);
        Assert.True(await database.Database.SqlQueryRaw<int>(
            "SELECT count(*)::int AS \"Value\" FROM \"PhotoRemovals\" WHERE \"Reason\" = 'RetentionExpired'")
            .SingleAsync() > 0);
        Assert.Equal(0, await database.Database.SqlQueryRaw<int>(
            "SELECT count(*)::int AS \"Value\" FROM \"InspectionSignedRevisions\" " +
            "WHERE \"ReportJson\"->'technicianIdentity' IS NOT NULL").SingleAsync());
        foreach (var revision in await database.InspectionSignedRevisions.AsNoTracking().ToListAsync())
        {
            Assert.Null(revision.Report().TechnicianIdentity);
        }

        // The shared photo worker deletes the objects of the queued receipts.
        await data.Factory.Services.GetServices<IHostedService>().OfType<PhotoCleanupWorker>().Single()
            .RunBatchAsync(CancellationToken.None);
        Assert.True(await database.Database.SqlQueryRaw<int>(
            "SELECT count(*)::int AS \"Value\" FROM \"InspectionPhotoRemovals\" " +
            "WHERE \"Reason\" = 'RetentionExpired' AND \"DeletedAt\" IS NOT NULL").SingleAsync() > 0);

        // Receipts and removed identities are never rewritten, and nothing is deleted.
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, await FailureAsync(database,
            "DELETE FROM \"InspectionPhotoRemovals\""));
        Assert.Equal(PostgresErrorCodes.CheckViolation, await FailureAsync(database,
            "UPDATE \"InspectionPhotoRemovals\" SET \"Reason\" = 'UnattachedExpired'"));
    }

    // The owner role ages the records the deadline reads, because the runtime role cannot rewrite history.
    private static async Task AgeAsync(NpgsqlConnection owner, string age)
    {
        await ExecuteAsync(owner, "ALTER TABLE \"InspectionSignedRevisions\" DISABLE TRIGGER USER");
        await ExecuteAsync(owner, $"UPDATE \"InspectionSignedRevisions\" SET \"SignedAt\" = \"SignedAt\" - interval '{age}'");
        await ExecuteAsync(owner, "ALTER TABLE \"InspectionSignedRevisions\" ENABLE TRIGGER USER");
        await ExecuteAsync(owner, "ALTER TABLE \"AppointmentCheckIns\" DISABLE TRIGGER USER");
        await ExecuteAsync(owner, $"UPDATE \"AppointmentCheckIns\" SET \"CheckedInAt\" = \"CheckedInAt\" - interval '{age}'");
        await ExecuteAsync(owner, "ALTER TABLE \"AppointmentCheckIns\" ENABLE TRIGGER USER");
    }

    private static async Task<int> RemovalCountAsync(DealershipDbContext database)
    {
        return await database.Database.SqlQueryRaw<int>(
            "SELECT ((SELECT count(*) FROM \"InspectionPhotoRemovals\" WHERE \"Reason\" = 'RetentionExpired') + " +
            "(SELECT count(*) FROM \"PhotoRemovals\" WHERE \"Reason\" = 'RetentionExpired'))::int AS \"Value\"")
            .SingleAsync();
    }

    private static async Task<int> RemovedIdentityCountAsync(DealershipDbContext database)
    {
        return await database.Database.SqlQueryRaw<int>(
            "SELECT count(*)::int AS \"Value\" FROM \"InspectionSignedRevisions\" " +
            "WHERE \"ReportJson\"->'technicianIdentity' IS NULL").SingleAsync();
    }

    private DealershipDbContext NewContext() => new(new DbContextOptionsBuilder<DealershipDbContext>()
        .UseNpgsql(fixture.Database.RuntimeConnectionString).Options);
}
