using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dealership.Application.Accounts;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace Dealership.IntegrationTests;

public sealed class EmailVerificationTests(RegistrationDatabase database) : IClassFixture<RegistrationDatabase>
{
    [Fact]
    public async Task RegistrationQueuesMailAndLatestTokenConfirmsAcrossRestart()
    {
        string firstToken;
        string latestToken;
        RegistrationRequest registration;

        using (var factory = CreateFactory())
        using (var client = factory.CreateClient())
        {
            registration = await RegisterAsync(client);
            var first = await GetLatestChallengeAsync(factory, registration.Email!);
            Assert.Null(first.ResendKey);
            Assert.Null(first.DeliveredAt);
            firstToken = GetToken(factory, first);

            await LoginAsync(client, registration);
            var resend = await client.PostAsJsonAsync("/api/auth/email-verification/resend",
                new { resendKey = Guid.NewGuid() });
            Assert.Equal(HttpStatusCode.Accepted, resend.StatusCode);
            latestToken = GetToken(factory, await GetLatestChallengeAsync(factory, registration.Email!));
            Assert.NotEqual(firstToken, latestToken);
            Assert.Equal(HttpStatusCode.BadRequest,
                (await client.PostAsJsonAsync("/api/auth/email-verification", new { token = firstToken })).StatusCode);
        }

        using var restarted = CreateFactory();
        using var restartedClient = restarted.CreateClient();
        Assert.Equal(HttpStatusCode.NoContent,
            (await restartedClient.PostAsJsonAsync("/api/auth/email-verification", new { token = latestToken })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await restartedClient.PostAsJsonAsync("/api/auth/email-verification", new { token = latestToken })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await restartedClient.PostAsJsonAsync("/api/auth/email-verification", new { token = firstToken })).StatusCode);
        await LoginAsync(restartedClient, registration);
        var account = await restartedClient.GetFromJsonAsync<CustomerAccount>("/api/account");
        Assert.True(account!.EmailVerified);
    }

    [Fact]
    public async Task ResendLimitsAndExactReplayPersistAcrossRestart()
    {
        var clock = new VerificationClock();
        var firstKey = Guid.NewGuid();
        RegistrationRequest registration;

        using (var factory = CreateFactory(clock))
        using (var client = factory.CreateClient())
        {
            registration = await RegisterAsync(client);
            await LoginAsync(client, registration);
            Assert.Equal(HttpStatusCode.Accepted,
                (await client.PostAsJsonAsync("/api/auth/email-verification/resend", new { resendKey = firstKey })).StatusCode);
            Assert.Equal(HttpStatusCode.Accepted,
                (await client.PostAsJsonAsync("/api/auth/email-verification/resend", new { resendKey = firstKey })).StatusCode);
            var denied = await client.PostAsJsonAsync("/api/auth/email-verification/resend",
                new { resendKey = Guid.NewGuid() });
            Assert.Equal(HttpStatusCode.TooManyRequests, denied.StatusCode);
            Assert.NotNull(denied.Headers.RetryAfter);
        }

        using var restarted = CreateFactory(clock);
        using var restartedClient = restarted.CreateClient();
        await LoginAsync(restartedClient, registration);
        Assert.Equal(HttpStatusCode.Accepted,
            (await restartedClient.PostAsJsonAsync("/api/auth/email-verification/resend", new { resendKey = firstKey })).StatusCode);

        for (var count = 0; count < 2; count++)
        {
            clock.Now += TimeSpan.FromMinutes(1);
            Assert.Equal(HttpStatusCode.Accepted,
                (await restartedClient.PostAsJsonAsync("/api/auth/email-verification/resend",
                    new { resendKey = Guid.NewGuid() })).StatusCode);
        }

        clock.Now += TimeSpan.FromMinutes(1);
        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await restartedClient.PostAsJsonAsync("/api/auth/email-verification/resend",
                new { resendKey = Guid.NewGuid() })).StatusCode);
        using var scope = restarted.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        Assert.Equal(3, await context.EmailVerificationChallenges.CountAsync(challenge => challenge.ResendKey != null &&
            challenge.RecipientEmail == registration.Email));
    }

    [Fact]
    public async Task ConcurrentResendsCreateOnlyOneNewChallenge()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var registration = await RegisterAsync(client);
        await LoginAsync(client, registration);

        var first = client.PostAsJsonAsync("/api/auth/email-verification/resend",
            new { resendKey = Guid.NewGuid() });
        var second = client.PostAsJsonAsync("/api/auth/email-verification/resend",
            new { resendKey = Guid.NewGuid() });
        var responses = await Task.WhenAll(first, second);

        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Accepted));
        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.TooManyRequests));

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var challenges = await context.EmailVerificationChallenges.Where(challenge =>
            challenge.RecipientEmail == registration.Email).ToListAsync();
        Assert.Equal(2, challenges.Count);
        Assert.Single(challenges, challenge => challenge.InvalidatedAt is null && challenge.ConsumedAt is null);
    }

    [Fact]
    public async Task LinkForPreviousAccountEmailCannotConfirmChangedAddress()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var registration = await RegisterAsync(client);
        var challenge = await GetLatestChallengeAsync(factory, registration.Email!);
        var token = GetToken(factory, challenge);

        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AccountUser>>();
            var user = (await users.FindByEmailAsync(registration.Email!))!;
            var changed = await users.SetEmailAsync(user, $"changed-{Guid.NewGuid():N}@example.invalid");
            Assert.True(changed.Succeeded);
        }

        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/auth/email-verification", new { token })).StatusCode);
    }

    [Fact]
    public async Task WrongOrExpiredTokenDoesNotConfirmAnotherAccount()
    {
        var clock = new VerificationClock();
        using var factory = CreateFactory(clock);
        using var client = factory.CreateClient();
        var first = await RegisterAsync(client);
        var second = await RegisterAsync(client);
        var challenge = await GetLatestChallengeAsync(factory, first.Email!);
        var token = GetToken(factory, challenge);
        var forged = token[..^1] + (token[^1] == 'a' ? 'b' : 'a');
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/auth/email-verification", new { token = forged })).StatusCode);
        clock.Now += TimeSpan.FromHours(25);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/auth/email-verification", new { token })).StatusCode);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        Assert.False((await context.Users.SingleAsync(user => user.Email == first.Email)).EmailConfirmed);
        Assert.False((await context.Users.SingleAsync(user => user.Email == second.Email)).EmailConfirmed);
    }

    [Fact]
    public async Task FailedMailAttemptRemainsDueAndRestartDeliversSameChallenge()
    {
        var clock = new VerificationClock();
        var registration = NewRegistration();
        var failedSender = new RecordingMailTransport(fail: true, registration.Email!);
        Guid challengeId;

        using (var factory = CreateFactory(clock, failedSender))
        using (var client = factory.CreateClient())
        {
            await RegisterAsync(client, registration);
            challengeId = (await GetLatestChallengeAsync(factory, registration.Email!)).Id;
            var attempted = await failedSender.Attempt.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Contains(GetToken(factory, await GetLatestChallengeAsync(factory, registration.Email!)), attempted.Body);
        }

        clock.Now += TimeSpan.FromMinutes(2);
        var succeedingSender = new RecordingMailTransport(fail: false, registration.Email!);
        using var restarted = CreateFactory(clock, succeedingSender);
        using var restartedClient = restarted.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await restartedClient.GetAsync("/health")).StatusCode);
        var delivered = await succeedingSender.Attempt.Task.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.Contains(GetToken(restarted, await GetLatestChallengeAsync(restarted, registration.Email!)), delivered.Body);
        await WaitForDeliveredAsync(restarted, challengeId);
    }

    [Fact]
    public async Task RestrictedSqlRejectsOrphanAndSecondActiveChallenge()
    {
        using var services = database.CreateServices();
        using var scope = services.CreateScope();
        var registration = NewRegistration();
        var registered = await scope.ServiceProvider.GetRequiredService<RegistrationService>()
            .RegisterAsync(registration, CancellationToken.None);
        Assert.Equal(RegistrationStatus.Succeeded, registered.Status);
        var context = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var challenge = await context.EmailVerificationChallenges.AsNoTracking()
            .SingleAsync(candidate => candidate.RecipientEmail == registration.Email);
        await using var connection = new NpgsqlConnection(database.RuntimeConnectionString);
        await connection.OpenAsync();
        await using var orphan = new NpgsqlCommand(
            "UPDATE \"EmailVerificationChallenges\" SET \"UserId\" = @orphan WHERE \"Id\" = @id", connection);
        orphan.Parameters.AddWithValue("orphan", Guid.NewGuid());
        orphan.Parameters.AddWithValue("id", challenge.Id);
        var orphanError = await Assert.ThrowsAsync<PostgresException>(async () => await orphan.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, orphanError.SqlState);

        await using var duplicate = new NpgsqlCommand("""
            INSERT INTO "EmailVerificationChallenges"
                ("Id", "UserId", "RecipientEmail", "CreatedAt", "ExpiresAt", "DeliveryAttempts",
                 "NextDeliveryAt")
            VALUES (@id, @userId, @email, @createdAt, @expiresAt, 0, @createdAt)
            """, connection);
        duplicate.Parameters.AddWithValue("id", Guid.NewGuid());
        duplicate.Parameters.AddWithValue("userId", challenge.UserId);
        duplicate.Parameters.AddWithValue("email", challenge.RecipientEmail);
        duplicate.Parameters.AddWithValue("createdAt", challenge.CreatedAt);
        duplicate.Parameters.AddWithValue("expiresAt", challenge.ExpiresAt);
        var duplicateError = await Assert.ThrowsAsync<PostgresException>(async () => await duplicate.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, duplicateError.SqlState);
    }

    [Fact]
    public async Task InterruptedVerificationIntentRollsBackRegistration()
    {
        var registration = NewRegistration();

        using (var interrupted = CreateFactory(interruptIntent: true))
        using (var client = interrupted.CreateClient())
        {
            Assert.Equal(HttpStatusCode.InternalServerError,
                (await client.PostAsJsonAsync("/api/auth/register", registration)).StatusCode);
        }

        using var restarted = CreateFactory();
        using var restartedClient = restarted.CreateClient();
        using (var scope = restarted.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
            Assert.False(await context.Users.AnyAsync(user => user.Email == registration.Email));
            Assert.False(await context.EmailVerificationChallenges.AnyAsync(challenge =>
                challenge.RecipientEmail == registration.Email));
        }

        Assert.Equal(HttpStatusCode.NoContent,
            (await restartedClient.PostAsJsonAsync("/api/auth/register", registration)).StatusCode);
    }

    private WebApplicationFactory<Program> CreateFactory(VerificationClock? clock = null,
        IMailTransport? sender = null, bool interruptIntent = false)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Email:Mode"] = "Disabled" }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<DealershipDbContext>>();
                services.RemoveAll<DealershipDbContext>();
                services.AddDbContext<DealershipDbContext>(options =>
                {
                    options.UseNpgsql(database.RuntimeConnectionString);

                    if (interruptIntent)
                    {
                        options.AddInterceptors(new IntentFailureInterceptor());
                    }
                });

                if (clock is not null)
                {
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton<TimeProvider>(clock);
                }

                if (sender is not null)
                {
                    services.RemoveAll<IMailTransport>();
                    services.AddSingleton(sender);
                }
            });
        });
    }

    private static async Task WaitForDeliveredAsync(WebApplicationFactory<Program> factory, Guid challengeId)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            using var scope = factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
            var challenge = await context.EmailVerificationChallenges.AsNoTracking()
                .SingleAsync(record => record.Id == challengeId);

            if (challenge.DeliveredAt is not null)
            {
                Assert.Equal(2, challenge.DeliveryAttempts);
                return;
            }

            await Task.Delay(250);
        }

        Assert.Fail("The restarted worker did not confirm SMTP acceptance.");
    }

    private static RegistrationRequest NewRegistration()
    {
        return new RegistrationRequest(Guid.NewGuid(), "Nur Aina Abdullah",
            $"nur.aina.{Guid.NewGuid():N}@example.invalid", "Password123!",
            $"940215{Random.Shared.Next(100000, 1000000)}", "0123456789", "18 Jalan Melati, Petaling Jaya, Selangor", TestUsernames.Next());
    }

    private static async Task<RegistrationRequest> RegisterAsync(HttpClient client, RegistrationRequest? request = null)
    {
        var registration = request ?? NewRegistration();
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.PostAsJsonAsync("/api/auth/register", registration)).StatusCode);
        return registration;
    }

    private static async Task LoginAsync(HttpClient client, RegistrationRequest registration)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(Guid.NewGuid(), registration.Email, registration.Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<LoginSession>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session!.AccessToken);
    }

    private static async Task<EmailVerificationChallenge> GetLatestChallengeAsync(WebApplicationFactory<Program> factory,
        string email)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        return await context.EmailVerificationChallenges.AsNoTracking()
            .Where(challenge => challenge.RecipientEmail == email)
            .OrderByDescending(challenge => challenge.CreatedAt)
            .FirstAsync();
    }

    private static string GetToken(WebApplicationFactory<Program> factory, EmailVerificationChallenge challenge)
    {
        return factory.Services.GetRequiredService<EmailVerificationToken>().Create(challenge);
    }

    private sealed class VerificationClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class IntentFailureInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context?.ChangeTracker.Entries<EmailVerificationChallenge>()
                .Any(entry => entry.State == EntityState.Added) == true)
            {
                throw new InvalidOperationException("Interrupted verification intent write.");
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
