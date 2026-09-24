using Dealership.Application.Accounts;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static Dealership.IntegrationTests.ReservationTestData;
using static Dealership.IntegrationTests.RetentionSql;

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

        // Real photographs that no signed inspection or checked-in appointment holds, so only the reason rule can refuse them.
        var unsignedInspectionPhoto = Guid.NewGuid();
        var unattachedPhoto = Guid.NewGuid();
        await using var owner = new NpgsqlConnection(fixture.Database.ConnectionString);
        await owner.OpenAsync();
        await CloneInspectionPhotoAsync(owner, inspectionPhoto, unsignedInspectionPhoto);
        await CloneIntakePhotoAsync(owner, checkInPhoto, unattachedPhoto);

        var probes = new (string Sql, string Guard)[]
        {
            (RemovalSql("InspectionPhotoRemovals", inspectionPhoto, "UnattachedExpired"), "CK_InspectionPhotoRemovals_Eligibility"),
            (RemovalSql("InspectionPhotoRemovals", inspectionPhoto, "OwnerErased"), "CK_InspectionPhotoRemovals_Eligibility"),
            (RemovalSql("InspectionPhotoRemovals", unsignedInspectionPhoto, "RetentionExpired"), "CK_InspectionPhotoRemovals_Eligibility"),
            (RemovalSql("PhotoRemovals", checkInPhoto, "UnattachedExpired"), "CK_PhotoRemovals_CheckedIn"),
            (RemovalSql("PhotoRemovals", checkInPhoto, "OwnerErased"), "CK_PhotoRemovals_CheckedIn"),
            (RemovalSql("PhotoRemovals", unattachedPhoto, "RetentionExpired"), "CK_PhotoRemovals_Eligibility")
        };

        foreach (var (sql, guard) in probes)
        {
            await ExpectRuntimeRefusalAsync(fixture.Database.RuntimeConnectionString, sql,
                PostgresErrorCodes.CheckViolation, guard);
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

    // The owner role copies a photograph row without the guard triggers, which only protect the runtime role's inserts.
    private static async Task CloneInspectionPhotoAsync(NpgsqlConnection owner, Guid source, Guid clone)
    {
        await ExecuteAsync(owner, "ALTER TABLE \"InspectionPhotos\" DISABLE TRIGGER USER");
        try
        {
            await ExecuteAsync(owner, $"""
                INSERT INTO "InspectionPhotos" ("Id", "InspectionId", "BranchId", "ActorId", "AssignmentVersion",
                    "PointCode", "ObjectKey", "ContentType", "Bytes", "Sha256", "Width", "Height",
                    "CreatedAt", "ExpiresAt", "AvailableAt")
                SELECT '{clone}', "InspectionId", "BranchId", "ActorId", "AssignmentVersion", "PointCode",
                    'inspection/photos/{clone:N}', "ContentType", "Bytes", "Sha256", "Width", "Height",
                    "CreatedAt", "ExpiresAt", "AvailableAt"
                FROM "InspectionPhotos" WHERE "Id" = '{source}'
                """);
        }
        finally
        {
            await ExecuteAsync(owner, "ALTER TABLE \"InspectionPhotos\" ENABLE TRIGGER USER");
        }
    }

    private static async Task CloneIntakePhotoAsync(NpgsqlConnection owner, Guid source, Guid clone)
    {
        await ExecuteAsync(owner, "ALTER TABLE \"IntakePhotos\" DISABLE TRIGGER USER");
        try
        {
            await ExecuteAsync(owner, $"""
                INSERT INTO "IntakePhotos" ("Id", "OwnerId", "Angle", "ObjectKey", "ContentType", "Bytes", "Sha256",
                    "Width", "Height", "CreatedAt", "ExpiresAt", "AvailableAt")
                SELECT '{clone}', "OwnerId", "Angle", 'intake/photos/{clone:N}', "ContentType", "Bytes", "Sha256",
                    "Width", "Height", "CreatedAt", "ExpiresAt", "AvailableAt"
                FROM "IntakePhotos" WHERE "Id" = '{source}'
                """);
        }
        finally
        {
            await ExecuteAsync(owner, "ALTER TABLE \"IntakePhotos\" ENABLE TRIGGER USER");
        }
    }

    private static string RemovalSql(string table, Guid photoId, string reason) =>
        $"INSERT INTO \"{table}\" (\"PhotoId\", \"Reason\", \"EligibleAt\", \"RequestedAt\", \"Attempts\", \"NextAttemptAt\") " +
        $"VALUES ('{photoId}', '{reason}', now(), now(), 0, now())";

    // The SQL state of the refusal, or null when the database accepted the statement.
    private Task<string?> FailureAsync(string sql) => RuntimeFailureAsync(fixture.Database.RuntimeConnectionString, sql);
}
