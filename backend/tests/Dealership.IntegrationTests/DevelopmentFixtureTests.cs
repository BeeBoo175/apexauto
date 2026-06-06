using Dealership.Infrastructure.Photos;
using Dealership.Application.Photos;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Diagnostics;
using System.Text.Json;
using Amazon.S3;
using Dealership.Domain.Intake;
using Dealership.Application;
using Dealership.Application.Accounts;
using Dealership.Application.Intake;
using Dealership.Infrastructure;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Storage;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Dealership.IntegrationTests;

public sealed class DevelopmentFixtureTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task SnapshotRollsBackResumesSerializesAndRetainsRuntimeGuardsAcrossThreeBranches()
    {
        var connection = fixture.Database.ConnectionString;
        var target = Target(connection);
        var fault = new DevelopmentFixtureInterruption();
        await using var provider = CreateProvider(connection, fault);
        var definitions = await ReadGuardsAsync();
        await using (var scope = provider.CreateAsyncScope())
        {
            var runner = scope.ServiceProvider.GetRequiredService<DevelopmentFixtureRunner>();
            Assert.All(await runner.ReviewAsync(target, CancellationToken.None), report => Assert.True(report.RequiresAttention));
            var storage = scope.ServiceProvider.GetRequiredService<PhotoStorageSettings>();
            var endpoint = storage.ServiceURL;
            try
            {
                storage.ServiceURL = "https://example.invalid";
                await Assert.ThrowsAsync<InvalidOperationException>(() => runner.SeedAsync(target, CancellationToken.None));
                Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<DealershipDbContext>().Branches.CountAsync());
            }
            finally
            {
                storage.ServiceURL = endpoint;
            }

            await scope.ServiceProvider.GetRequiredService<IAmazonS3>().DeleteBucketAsync(fixture.BucketName);
        }

        await using (var runtime = CreateProvider(fixture.Database.RuntimeConnectionString, new DevelopmentFixtureInterruption()))
        {
            var denied = await Assert.ThrowsAsync<PostgresException>(() => SeedAsync(runtime, Target(fixture.Database.RuntimeConnectionString)));
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var database = services.GetRequiredService<DealershipDbContext>();
            var ownerId = await database.CustomerProfiles.Where(profile =>
                profile.RegistrationKey == DevelopmentFixtureManifest.Customers[1].Registration.RegistrationKey)
                .Select(profile => profile.UserId).SingleAsync();
            using var content = DevelopmentFixturePhotos.Open(PhotoAngle.Driver);
            var validated = await services.GetRequiredService<IPhotoImageValidator>().ValidateAsync(content, "image/jpeg", CancellationToken.None);
            await services.GetRequiredService<IIntakePhotoStore>().PrepareAsync(ownerId, Guid.NewGuid(),
                PhotoAngle.Driver, validated!, TimeSpan.FromHours(24), CancellationToken.None);
            await Assert.ThrowsAsync<InvalidOperationException>(() => SeedAsync(provider, target));
            Assert.Equal(1, await database.IntakePhotos.CountAsync());
            await database.IntakePhotos.ExecuteDeleteAsync();
        }

        await Assert.ThrowsAsync<IOException>(() => SeedAsync(provider, target));
        await using (var scope = provider.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
            Assert.Equal(0, await database.IntakeAppointments.CountAsync());
            Assert.Equal(0, await database.IntakePhotos.CountAsync());
            Assert.Equal(0, await database.AppointmentAssignments.CountAsync());
            Assert.Equal(20, await database.Users.CountAsync());
        }

        Assert.Equal(definitions, await ReadGuardsAsync());
        fault.Enabled = false;
        fault.FailInspectionUploadOnce = true;
        await Assert.ThrowsAsync<IOException>(() => SeedAsync(provider, target));
        await using (var scope = provider.CreateAsyncScope())
        {
            var pending = await scope.ServiceProvider.GetRequiredService<DealershipDbContext>().InspectionPhotos.SingleAsync();
            Assert.Null(pending.AvailableAt);
        }
        await DevelopmentInspectionScenarioAssertions.SeedInterruptedRevisionAsync(provider);
        await DevelopmentInspectionScenarioAssertions.SeedPartialRepairAsync(provider);
        fault.FailListingUploadOnce = true;
        await Assert.ThrowsAsync<IOException>(() => SeedAsync(provider, target));
        await using (var scope = provider.CreateAsyncScope())
        {
            var pending = await scope.ServiceProvider.GetRequiredService<DealershipDbContext>().ListingPhotos.SingleAsync();
            Assert.Null(pending.AvailableAt);
        }

        fault.FailReservationCommitOnce = true;
        await Assert.ThrowsAsync<IOException>(() => SeedAsync(provider, target, deliverMail: true));
        var interrupted = await DevelopmentReservationFixtureAssertions.InterruptedAsync(provider);
        await using (var salesScope = provider.CreateAsyncScope())
        {
            await DevelopmentUnscheduledVolumeAssertions.SeedInterruptedSaleAsync(salesScope.ServiceProvider);
        }
        await using var restarted = CreateProvider(connection, fault);
        var competing = await Task.WhenAll(SeedAsync(provider, target), SeedAsync(restarted, target));
        Assert.All(competing.SelectMany(reports => reports), report => Assert.False(report.RequiresAttention,
            $"{report.Fixture}: {report.State}"));
        Assert.Equal(definitions, await ReadGuardsAsync());
        await DevelopmentReservationFixtureAssertions.VerifyAsync(restarted, interrupted);
        await using (var volumeScope = restarted.CreateAsyncScope())
        {
            await volumeScope.ServiceProvider.GetRequiredService<DealershipDbContext>().Database.OpenConnectionAsync();
            await DevelopmentTestDriveVolumeAssertions.VerifyAsync(volumeScope.ServiceProvider);
            await DevelopmentUnscheduledVolumeAssertions.VerifyAsync(volumeScope.ServiceProvider);
        }
        var testDriveSnapshot = await DevelopmentTestDriveFixtureAssertions.SnapshotAsync(restarted);
        await AssertFixtureMailAsync(restarted, interrupted.Id);
        await AssertNormalAuthenticationAsync();
        await DevelopmentIntakeFixtureAssertions.VerifyAsync(provider);
        await InspectionQueueFixtureAssertions.VerifyAsync(provider);
        await DevelopmentListingFixtureAssertions.VerifyCatalogueAsync(provider);
        await DevelopmentPurchaseFixtureAssertions.VerifyAsync(provider, target);
        var snapshot = await DevelopmentIntakeFixtureAssertions.SnapshotAsync(provider);
        await ValuationMigrationAssertions.VerifyAsync(fixture.Database);
        Assert.Equal(definitions, await ReadGuardsAsync());
        await CancellationMigrationAssertions.VerifyAsync(fixture.Database);
        Assert.Equal(definitions, await ReadGuardsAsync());
        Assert.Equal(snapshot, await DevelopmentIntakeFixtureAssertions.SnapshotAsync(provider));
        Assert.All(await SeedAsync(restarted, target), report => Assert.False(report.RequiresAttention));
        Assert.Equal(testDriveSnapshot, await DevelopmentTestDriveFixtureAssertions.SnapshotAsync(restarted));
        Assert.Equal(snapshot, await DevelopmentIntakeFixtureAssertions.SnapshotAsync(restarted));
        await AssertReviewPreservesDriftAsync(provider, target);
        await AssertRuntimeGuardsAsync();
        await DevelopmentInspectionScenarioAssertions.VerifyAsync(provider, target);
        await NoShowLifecycleAssertions.VerifyAsync(fixture);
        await DevelopmentInspectionFixtureAssertions.VerifyAsync(provider, target);
        await DevelopmentInspectionScenarioAssertions.VerifyHumanEditPreservationAsync(provider, target);
        await DevelopmentListingFixtureAssertions.VerifyAsync(provider, target);
        await DevelopmentReservationFixtureAssertions.VerifyPreservationAsync(restarted, target, fixture.Database);
    }

    private static async Task AssertFixtureMailAsync(ServiceProvider provider, Guid optedInReservationId)
    {
        await using var scope = provider.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        Assert.False(await database.AppointmentMailOutbox.AnyAsync(item => item.DeliveredAt == null));
        var optedIn = Assert.Single(await database.ReservationMailOutbox.AsNoTracking()
            .Where(item => item.DeliveredAt == null).ToArrayAsync());
        Assert.Equal(optedInReservationId, optedIn.ReservationId);
        Assert.False(await database.ReservationMailOutbox.AnyAsync(item => item.DeliveryAttempts != 0));
        var deliveries = await database.SaleDeliveryMailOutbox.AsNoTracking().ToArrayAsync();
        Assert.Equal(2, deliveries.Length);
        Assert.All(deliveries, delivery =>
        {
            Assert.NotNull(delivery.DeliveredAt);
            Assert.Equal(0, delivery.DeliveryAttempts);
        });
        // Both competing default reruns preserved this earlier opt-in intent; restore it for later snapshot assertions.
        await database.ReservationMailOutbox.Where(item => item.Id == optedIn.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.DeliveredAt, DateTimeOffset.UtcNow));
    }

    private static async Task AssertReviewPreservesDriftAsync(ServiceProvider provider, DevelopmentFixtureTarget target)
    {
        await using var scope = provider.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var runner = scope.ServiceProvider.GetRequiredService<DevelopmentFixtureRunner>();
        Assert.All(await runner.ReviewAsync(target, CancellationToken.None), report => Assert.False(report.RequiresAttention));
        var branch = DevelopmentFixtureManifest.Branches[0];
        var administrator = DevelopmentFixtureManifest.Staff.Single(item => item.Role == AccountRoles.Administrator);
        await database.Branches.Where(item => item.Id == branch.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Phone, "+60355659876"));
        await database.Users.Where(item => item.Id == administrator.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.IsActive, false));
        try
        {
            var prerequisites = await SnapshotPrerequisitesAsync(database);
            var business = await DevelopmentIntakeFixtureAssertions.SnapshotAsync(provider);
            var reports = await runner.ReviewAsync(target, CancellationToken.None);
            Assert.True(reports.Single(report => report.Fixture == branch.Name).RequiresAttention);
            Assert.True(reports.Single(report => report.Fixture == $"{administrator.Role} ({administrator.Email})").RequiresAttention);
            Assert.Equal(2, reports.Count(report => report.RequiresAttention));
            Assert.Equal(2, await ReviewCommandExitCodeAsync(target));
            Assert.Equal(prerequisites, await SnapshotPrerequisitesAsync(database));
            Assert.Equal(business, await DevelopmentIntakeFixtureAssertions.SnapshotAsync(provider));
        }
        finally
        {
            await database.Branches.Where(item => item.Id == branch.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Phone, branch.Phone));
            await database.Users.Where(item => item.Id == administrator.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.IsActive, true));
        }
    }

    private static async Task<string> SnapshotPrerequisitesAsync(DealershipDbContext database)
    {
        return JsonSerializer.Serialize(new
        {
            Branches = await database.Branches.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync(),
            Users = await database.Users.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync(),
            Profiles = await database.CustomerProfiles.AsNoTracking().OrderBy(item => item.UserId).ToArrayAsync(),
            Verification = await database.EmailVerificationChallenges.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync(),
            Recovery = await database.PasswordResetChallenges.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync()
        });
    }

    private static async Task<int> ReviewCommandExitCodeAsync(DevelopmentFixtureTarget target)
    {
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var command = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            $"../../../../../tools/DevelopmentFixtures/bin/{configuration}/net10.0/DevelopmentFixtures.dll"));
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add(command);
        start.ArgumentList.Add("review");
        start.Environment["DOTNET_ENVIRONMENT"] = "Development";
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        start.Environment["POSTGRES_DB"] = new NpgsqlConnectionStringBuilder(target.ConnectionString).Database;
        start.Environment["ConnectionStrings__DefaultConnection"] = target.ConnectionString;
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try
        {
            await process.WaitForExitAsync(deadline.Token);
            await Task.WhenAll(output, error);
            return process.ExitCode;
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
    }

    private async Task<string[]> ReadGuardsAsync()
    {
        await using var connection = new NpgsqlConnection(fixture.Database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT pg_get_functiondef(oid) FROM pg_proc WHERE pronamespace = 'public'::regnamespace
                AND proname IN ('require_available_appointment_photo', 'validate_appointment_cancellation',
                    'validate_appointment_checkin', 'guard_appointment_assignment')
                ORDER BY proname
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var definitions = new List<string>();
        while (await reader.ReadAsync())
        {
            definitions.Add(reader.GetString(0));
        }

        Assert.Equal(4, definitions.Count);
        return [.. definitions];
    }

    private async Task AssertRuntimeGuardsAsync()
    {
        await using var connection = new NpgsqlConnection(fixture.Database.RuntimeConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            INSERT INTO "AppointmentCheckIns" ("AppointmentId", "OwnerId", "BranchId", "VehicleId", "StaffId", "CheckedInAt",
                "Vin", "MileageKilometres", "CustomerPresent", "IdentityConfirmed", "OwnershipConfirmed", "VehicleConfirmed")
            SELECT a."Id", a."OwnerId", a."BranchId", @vehicle, u."Id", a."StartsAt", 'PM2M80010KC006242', 96000, true, true, true, true
            FROM "IntakeAppointments" a JOIN "AspNetUsers" u ON u."BranchId" = a."BranchId" AND u."Role" = 'SalesAgent'
            ORDER BY a."StartsAt" DESC LIMIT 1
            """, connection);
        command.Parameters.AddWithValue("vehicle", Guid.NewGuid());
        var checkIn = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal("CK_AppointmentCheckIns_Time", checkIn.ConstraintName);
        command.Parameters.Clear();
        command.CommandText = """
            UPDATE "IntakeAppointments" SET "CancelledAt" = "CreatedAt",
                "CancellationPhotosExpireAt" = "CreatedAt" + interval '30 days'
            WHERE "Id" = (SELECT a."Id" FROM "IntakeAppointments" a WHERE a."CancelledAt" IS NULL
                AND NOT EXISTS (SELECT 1 FROM "AppointmentCheckIns" c WHERE c."AppointmentId" = a."Id")
                ORDER BY a."StartsAt" LIMIT 1)
            """;
        var cancellation = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal("CK_IntakeAppointments_CancellationTime", cancellation.ConstraintName);
        command.CommandText = """
            INSERT INTO "AppointmentPhotos" ("AppointmentId", "OwnerId", "Angle", "PhotoId")
            SELECT p."AppointmentId", p."OwnerId", p."Angle", p."PhotoId" FROM "AppointmentPhotos" p
            JOIN "IntakePhotos" i ON i."Id" = p."PhotoId" WHERE i."ExpiresAt" < clock_timestamp() LIMIT 1
            """;
        var photo = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal("CK_AppointmentPhotos_Available", photo.ConstraintName);
        command.CommandText = """
            INSERT INTO "AppointmentAssignments" ("Id", "AppointmentId", "BranchId", "ActorId", "Kind", "HandlerId",
                "ExpectedVersion", "Version", "RecordedAt")
            SELECT gen_random_uuid(), a."Id", a."BranchId", u."Id", 'Claimed', u."Id", 0, 1, a."StartsAt"
            FROM "IntakeAppointments" a JOIN "AspNetUsers" u ON u."BranchId" = a."BranchId" AND u."Role" = 'SalesAgent'
            WHERE a."CancelledAt" IS NULL AND a."StartsAt" < clock_timestamp() - interval '1 day'
                AND NOT EXISTS (SELECT 1 FROM "AppointmentCheckIns" c WHERE c."AppointmentId" = a."Id")
            ORDER BY a."StartsAt" LIMIT 1
            """;
        var assignment = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal("CK_AppointmentAssignments_Availability", assignment.ConstraintName);
        command.CommandText = """
            SELECT count(*) FROM pg_trigger WHERE tgrelid IN ('public."IntakeAppointments"'::regclass,
                'public."AppointmentPhotos"'::regclass, 'public."AppointmentCheckIns"'::regclass,
                'public."AppointmentAssignments"'::regclass) AND tgenabled <> 'O'
            """;
        Assert.Equal(0L, await command.ExecuteScalarAsync());
    }

    private async Task AssertNormalAuthenticationAsync()
    {
        // Separate hosts keep each normal process-local login budget below its limit.
        foreach (var customers in DevelopmentFixtureManifest.Customers.Chunk(5))
        {
            using var customerFactory = fixture.CreateFactory();
            using var customerClient = customerFactory.CreateClient();
            foreach (var customer in customers)
            {
                await AssertLoginAsync(customerClient, customer.Registration.Email!, HttpStatusCode.OK);
            }
        }

        foreach (var accounts in DevelopmentFixtureManifest.Staff.Chunk(5))
        {
            using var staffFactory = fixture.CreateFactory();
            using var staffClient = staffFactory.CreateClient();

            foreach (var account in accounts)
            {
                await AssertLoginAsync(staffClient, account.Email, HttpStatusCode.Forbidden);
            }
        }
    }

    private static async Task AssertLoginAsync(HttpClient client, string email, HttpStatusCode accountStatus)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(Guid.NewGuid(), email, DevelopmentFixtureManifest.Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<LoginSession>();
        Assert.NotNull(session);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        Assert.Equal(accountStatus, (await client.GetAsync("/api/account")).StatusCode);
        await DevelopmentIntakeFixtureAssertions.VerifyAuthenticatedAsync(client, email);
        client.DefaultRequestHeaders.Authorization = null;
    }

    private ServiceProvider CreateProvider(string connection, DevelopmentFixtureInterruption fault)
    {
        var configuration = new ConfigurationBuilder().AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"))
            .AddEnvironmentVariables().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connection,
                ["AWS:BucketName"] = fixture.BucketName,
                ["IntakePhotos:CleanupEnabled"] = "false"
            }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddIntakeApplication();
        services.AddPhotoApplication();
        services.AddInfrastructure(configuration);
        services.AddDevelopmentFixtures(configuration);
        services.AddS3Storage(configuration);
        services.AddPhotos(configuration);
        fault.Configure(services);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    private static DevelopmentFixtureTarget Target(string connection)
    {
        return DevelopmentFixtureTarget.Validate("Development", new NpgsqlConnectionStringBuilder(connection).Database, connection);
    }

    private static async Task<IReadOnlyList<DevelopmentFixtureReport>> SeedAsync(ServiceProvider provider,
        DevelopmentFixtureTarget target, bool deliverMail = false)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<DevelopmentFixtureRunner>()
            .SeedAsync(target, CancellationToken.None, deliverMail);
    }
}
