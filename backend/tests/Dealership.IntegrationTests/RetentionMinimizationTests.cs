using Dealership.Application.Intake;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Retention;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using static Dealership.IntegrationTests.RetentionSql;

// The helpers below build SQL from fixed timestamp fragments, never from request data.
#pragma warning disable EF1003

namespace Dealership.IntegrationTests;

public sealed class RetentionMinimizationTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task ExpiredSecurityArtifactsAndTerminalInvitationsLoseTheirIdentityWhileLiveOnesStay()
    {
        using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var registration = await IntakePhotoFixture.RegisterAsync(client);
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var userId = await database.Users.Where(user => user.Email == registration.Email).Select(user => user.Id)
            .SingleAsync();
        var ids = new RetentionIds();
        await SeedAsync(database, userId, registration.Email!, ids);

        await Worker(factory).RunBatchAsync(CancellationToken.None);

        var sessions = await database.AccountSessions.AsNoTracking().ToDictionaryAsync(item => item.Id);
        Assert.Null(sessions[ids.OldSession].SecurityStamp);
        Assert.NotNull(sessions[ids.OldSession].MinimizedAt);
        Assert.Null(sessions[ids.RevokedSession].SecurityStamp);
        Assert.NotNull(sessions[ids.LiveSession].SecurityStamp);
        Assert.Null(sessions[ids.LiveSession].MinimizedAt);

        var resets = await database.PasswordResetChallenges.AsNoTracking().ToDictionaryAsync(item => item.Id);
        Assert.Null(resets[ids.OldReset].RecipientEmail);
        Assert.NotNull(resets[ids.OldReset].MinimizedAt);
        Assert.Equal(registration.Email, resets[ids.LiveReset].RecipientEmail);

        var consumed = resets[ids.ConsumedReset];
        Assert.Null(consumed.RecipientEmail);
        Assert.Null(consumed.CompletedSecurityStamp);
        Assert.NotNull(consumed.ConsumedAt);

        var verifications = await database.EmailVerificationChallenges.AsNoTracking()
            .ToDictionaryAsync(item => item.Id);
        Assert.Null(verifications[ids.OldVerification].RecipientEmail);
        Assert.Equal(registration.Email, verifications[ids.LiveVerification].RecipientEmail);

        var invitations = await database.StaffInvitations.AsNoTracking().ToDictionaryAsync(item => item.Id);
        var retired = invitations[ids.ExpiredInvitation];
        Assert.Equal((null, null, null), (retired.Email, retired.Name, retired.Mobile));
        Assert.NotNull(retired.MinimizedAt);
        Assert.False(retired.IsPending(DateTimeOffset.UtcNow));
        Assert.NotNull(invitations[ids.LiveInvitation].Email);

        // A second run finds nothing, and the executor owns no DELETE privilege.
        await Worker(factory).RunBatchAsync(CancellationToken.None);
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, await FailureAsync(database,
            "DELETE FROM \"AccountSessions\" WHERE \"Id\" = {0}", ids.OldSession));
    }

    [Fact]
    public async Task OnlyTheListedColumnsOfATerminalRowCanBeMinimized()
    {
        using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var registration = await IntakePhotoFixture.RegisterAsync(client);
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var userId = await database.Users.Where(user => user.Email == registration.Email).Select(user => user.Id)
            .SingleAsync();
        var ids = new RetentionIds();
        await SeedAsync(database, userId, registration.Email!, ids);

        // A live session cannot be minimized, an expired one cannot change anything else, and nothing is deletable.
        Assert.Equal(PostgresErrorCodes.CheckViolation, await FailureAsync(database,
            "UPDATE \"AccountSessions\" SET \"SecurityStamp\" = NULL, \"MinimizedAt\" = now() WHERE \"Id\" = {0}",
            ids.LiveSession));
        Assert.Equal(PostgresErrorCodes.CheckViolation, await FailureAsync(database,
            "UPDATE \"AccountSessions\" SET \"ExpiresAt\" = now() + interval '1 day', \"SecurityStamp\" = NULL, " +
            "\"MinimizedAt\" = now() WHERE \"Id\" = {0}", ids.OldSession));
        Assert.Equal(PostgresErrorCodes.CheckViolation, await FailureAsync(database,
            "UPDATE \"PasswordResetChallenges\" SET \"RecipientEmail\" = NULL, \"MinimizedAt\" = now() WHERE \"Id\" = {0}",
            ids.LiveReset));
        Assert.Equal(PostgresErrorCodes.CheckViolation, await FailureAsync(database,
            "UPDATE \"PasswordResetChallenges\" SET \"RecipientEmail\" = NULL WHERE \"Id\" = {0}", ids.LiveReset));
        Assert.Equal(PostgresErrorCodes.CheckViolation, await FailureAsync(database,
            "UPDATE \"StaffInvitations\" SET \"Email\" = NULL, \"Name\" = NULL, \"Mobile\" = NULL WHERE \"Id\" = {0}",
            ids.LiveInvitation));

        Assert.Equal(1, await database.Database.ExecuteSqlRawAsync(
            "UPDATE \"AccountSessions\" SET \"SecurityStamp\" = NULL, \"MinimizedAt\" = now() WHERE \"Id\" = {0}",
            ids.OldSession));
        // A minimized row stays minimized and a second marker write is refused.
        Assert.Equal(PostgresErrorCodes.CheckViolation, await FailureAsync(database,
            "UPDATE \"AccountSessions\" SET \"SecurityStamp\" = 'restored', \"MinimizedAt\" = NULL WHERE \"Id\" = {0}",
            ids.OldSession));
    }

    [Fact]
    public async Task FailingRowsAtTheHeadOfAClassDoNotBlockLaterRows()
    {
        // Earlier scenarios of this class database leave eligible rows behind; drain them so the batch is not shared.
        using (var drain = fixture.CreateFactory())
        {
            await Worker(drain).RunBatchAsync(CancellationToken.None);
        }

        using var factory = fixture.CreateFactory().WithWebHostBuilder(builder => builder.ConfigureAppConfiguration(
            (_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Retention:BatchSize"] = "2"
            })));
        using var client = factory.CreateClient();
        var registration = await IntakePhotoFixture.RegisterAsync(client);
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var userId = await database.Users.Where(user => user.Email == registration.Email).Select(user => user.Id)
            .SingleAsync();
        // Four failing rows sort before three good ones and exceed the batch size of two.
        var failing = Enumerable.Range(1, 4).Select(index => Guid.Parse($"00000000-0000-0000-0000-00000000000{index}")).ToArray();
        var good = Enumerable.Range(1, 3).Select(index => Guid.Parse($"ffffffff-0000-0000-0000-00000000000{index}")).ToArray();
        foreach (var id in failing.Concat(good))
        {
            await InsertVerificationAsync(database, id, userId, registration.Email!, "now() - interval '90 days'", "now() - interval '90 days' + interval '1 minute'");
        }

        await using var owner = new NpgsqlConnection(fixture.Database.ConnectionString);
        await owner.OpenAsync();
        await ExecuteAsync(owner, $"""
            CREATE FUNCTION public.fail_test_minimization() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF OLD."Id" < '10000000-0000-0000-0000-000000000000'::uuid THEN
                    RAISE EXCEPTION 'injected failure';
                END IF;
                RETURN NEW;
            END $$;
            CREATE TRIGGER fail_test_minimization BEFORE UPDATE ON "EmailVerificationChallenges"
                FOR EACH ROW EXECUTE FUNCTION public.fail_test_minimization();
            """);

        try
        {
            await Worker(factory).RunBatchAsync(CancellationToken.None);
            Assert.Equal(2, await MinimizedCountAsync(database, good));
            Assert.Equal(0, await MinimizedCountAsync(database, failing));

            await Worker(factory).RunBatchAsync(CancellationToken.None);
            Assert.Equal(3, await MinimizedCountAsync(database, good));
            Assert.Equal(0, await MinimizedCountAsync(database, failing));
        }
        finally
        {
            await ExecuteAsync(owner, "DROP TRIGGER fail_test_minimization ON \"EmailVerificationChallenges\"; " +
                "DROP FUNCTION public.fail_test_minimization();");
        }
    }

    [Fact]
    public async Task DeliveredMailLosesItsRecipientOnlyAfterTheConfiguredDays()
    {
        using var data = await CheckInTestData.CreateAsync(fixture);
        var appointment = await data.BookAsync();
        await data.ClaimAsync(appointment.Id);
        using (var scope = data.Factory.Services.CreateScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<AppointmentCheckInService>()
                .CheckInAsync(data.StaffId, appointment.Id, CheckInTestData.Evidence, CancellationToken.None);
            Assert.Equal(CheckInStatus.Succeeded, result.Status);
        }

        using var scopeForSql = data.Factory.Services.CreateScope();
        var database = scopeForSql.ServiceProvider.GetRequiredService<DealershipDbContext>();
        await Worker(data.Factory).RunBatchAsync(CancellationToken.None);
        Assert.All(await RecipientsAsync(database), recipient => Assert.Equal(data.CustomerEmail, recipient));

        // A delivery inside the thirty-day horizon keeps its recipient; the row is then aged past it by the owner role.
        await database.Database.ExecuteSqlRawAsync(
            "UPDATE \"AppointmentMailOutbox\" SET \"DeliveredAt\" = now() - interval '29 days'");
        await Worker(data.Factory).RunBatchAsync(CancellationToken.None);
        Assert.All(await RecipientsAsync(database), recipient => Assert.Equal(data.CustomerEmail, recipient));

        await using var owner = new NpgsqlConnection(fixture.Database.ConnectionString);
        await owner.OpenAsync();
        await ExecuteAsync(owner, "ALTER TABLE \"AppointmentMailOutbox\" DISABLE TRIGGER protect_appointment_mail; " +
            "UPDATE \"AppointmentMailOutbox\" SET \"DeliveredAt\" = now() - interval '31 days'; " +
            "ALTER TABLE \"AppointmentMailOutbox\" ENABLE TRIGGER protect_appointment_mail;");
        await Worker(data.Factory).RunBatchAsync(CancellationToken.None);
        Assert.All(await RecipientsAsync(database), Assert.Null);
    }

    [Fact]
    public async Task EveryRetentionRuleRunsAgainstTheCurrentSchema()
    {
        var warnings = new List<string>();
        using var factory = fixture.CreateFactory().WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddLogging(
            logging => logging.AddProvider(new WarningCapture(warnings)))));
        using var client = factory.CreateClient();

        await Worker(factory).RunBatchAsync(CancellationToken.None);

        Assert.DoesNotContain(warnings, message => message.Contains("Retention of", StringComparison.Ordinal));
    }

    internal sealed class WarningCapture(List<string> messages) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new Capture(messages);

        public void Dispose()
        {
        }

        private sealed class Capture(List<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel == LogLevel.Warning;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (logLevel == LogLevel.Warning)
                {
                    lock (messages)
                    {
                        messages.Add(formatter(state, exception));
                    }
                }
            }
        }
    }

    internal static RetentionWorker Worker(WebApplicationFactory<Program> factory)
    {
        return factory.Services.GetServices<IHostedService>().OfType<RetentionWorker>().Single();
    }

    private static async Task<string?[]> RecipientsAsync(DealershipDbContext database)
    {
        return (await database.Database.SqlQueryRaw<string?>(
            "SELECT \"RecipientEmail\" AS \"Value\" FROM \"AppointmentMailOutbox\"").ToListAsync()).ToArray();
    }

    private static async Task<int> MinimizedCountAsync(DealershipDbContext database, Guid[] ids)
    {
        return await database.EmailVerificationChallenges.AsNoTracking()
            .CountAsync(item => ids.Contains(item.Id) && item.RecipientEmail == null);
    }

    private static async Task SeedAsync(DealershipDbContext database, Guid userId, string email, RetentionIds ids)
    {
        await InsertSessionAsync(database, ids.OldSession, userId, "now() - interval '100 days'", "now() - interval '90 days'", "NULL");
        await InsertSessionAsync(database, ids.RevokedSession, userId, "now() - interval '100 days'", "now() + interval '1 day'",
            "now() - interval '40 days'");
        await InsertSessionAsync(database, ids.LiveSession, userId, "now()", "now() + interval '1 hour'", "NULL");
        await InsertResetAsync(database, ids.OldReset, userId, email, "now() - interval '60 days'", "NULL");
        await InsertResetAsync(database, ids.LiveReset, userId, email, "now()", "NULL");
        await InsertResetAsync(database, ids.ConsumedReset, userId, email, "now() - interval '60 days'",
            "now() - interval '60 days' + interval '10 minutes'");
        await InsertVerificationAsync(database, ids.OldVerification, userId, email, "now() - interval '60 days'",
            "now() - interval '60 days' + interval '1 minute'");
        await InsertVerificationAsync(database, ids.LiveVerification, userId, email, "now()", "now()");
        await InsertInvitationAsync(database, ids.ExpiredInvitation, userId, "now() - interval '90 days'", "now() - interval '60 days'");
        await InsertInvitationAsync(database, ids.LiveInvitation, userId, "now()", "now() + interval '1 day'");
    }

    private static Task InsertSessionAsync(DealershipDbContext database, Guid id, Guid userId, string created,
        string expires, string revoked)
    {
        return database.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"AccountSessions\" (\"Id\", \"UserId\", \"SecurityStamp\", \"CreatedAt\", \"ExpiresAt\", \"RevokedAt\") " +
            $"VALUES ({{0}}, {{1}}, 'stamp-{id:N}', {created}, {expires}, {revoked})", id, userId);
    }

    private static Task InsertResetAsync(DealershipDbContext database, Guid id, Guid userId, string email,
        string created, string consumed)
    {
        var stamp = consumed == "NULL" ? "NULL" : "'completed-stamp'";
        // Only the live link may stay pending: a second pending link would violate the one-per-user index.
        var invalidated = consumed == "NULL" && created != "now()" ? $"{created} + interval '10 minutes'" : "NULL";
        return database.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"PasswordResetChallenges\" (\"Id\", \"UserId\", \"RecipientEmail\", \"CreatedAt\", \"ExpiresAt\", " +
            "\"ConsumedAt\", \"CompletedSecurityStamp\", \"InvalidatedAt\", \"DeliveryAttempts\", \"NextDeliveryAt\") " +
            $"VALUES ({{0}}, {{1}}, {{2}}, {created}, {created} + interval '1 hour', {consumed}, {stamp}, {invalidated}, 0, {created})",
            id, userId, email);
    }

    private static Task InsertVerificationAsync(DealershipDbContext database, Guid id, Guid userId, string email,
        string created, string invalidated)
    {
        return database.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"EmailVerificationChallenges\" (\"Id\", \"UserId\", \"RecipientEmail\", \"CreatedAt\", \"ExpiresAt\", " +
            "\"InvalidatedAt\", \"DeliveryAttempts\", \"NextDeliveryAt\") " +
            $"VALUES ({{0}}, {{1}}, {{2}}, {created}, {created} + interval '1 hour', {invalidated}, 0, {created})", id, userId, email);
    }

    private static Task InsertInvitationAsync(DealershipDbContext database, Guid id, Guid invitedBy, string created,
        string expires)
    {
        return database.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"StaffInvitations\" (\"Id\", \"Email\", \"Role\", \"Name\", \"Mobile\", \"InvitedById\", \"CreatedAt\", " +
            "\"ExpiresAt\", \"TokenNonce\", \"SentAt\", \"DeliveryAttempts\", \"NextDeliveryAt\") " +
            $"VALUES ({{0}}, 'invitee.{id:N}@example.invalid', 'Administrator', 'Invited Person', '0123456789', {{1}}, " +
            $"{created}, {expires}, {{2}}, ARRAY[]::timestamptz[], 0, {created})", id, invitedBy, Guid.NewGuid());
    }

    private sealed class RetentionIds
    {
        public Guid OldSession { get; } = Guid.NewGuid();
        public Guid RevokedSession { get; } = Guid.NewGuid();
        public Guid LiveSession { get; } = Guid.NewGuid();
        public Guid OldReset { get; } = Guid.NewGuid();
        public Guid LiveReset { get; } = Guid.NewGuid();
        public Guid ConsumedReset { get; } = Guid.NewGuid();
        public Guid OldVerification { get; } = Guid.NewGuid();
        public Guid LiveVerification { get; } = Guid.NewGuid();
        public Guid ExpiredInvitation { get; } = Guid.NewGuid();
        public Guid LiveInvitation { get; } = Guid.NewGuid();
    }
}
