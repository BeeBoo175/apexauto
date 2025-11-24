using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Dealership.Application.Accounts;
using Dealership.Infrastructure.Configuration;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Microsoft.AspNetCore.Identity;

namespace Dealership.IntegrationTests;

public sealed class AccountSessionTests(RegistrationDatabase database) : IClassFixture<RegistrationDatabase>
{
    [Fact]
    public async Task LoginKeyReplaysConcurrentAndRestartedRequestsWithoutRevivingLogout()
    {
        var key = Guid.NewGuid();
        RegistrationRequest registration;
        LoginSession original;

        using (var factory = CreateFactory())
        using (var client = factory.CreateClient())
        {
            registration = await RegisterAsync(client);
            var other = await RegisterAsync(client);
            var concurrent = await Task.WhenAll(LoginAsync(client, registration, key), LoginAsync(client, registration, key));
            original = concurrent[0];
            Assert.Equal(original, concurrent[1]);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login",
                new LoginRequest(key, other.Email, other.Password))).StatusCode);
            var collisionKey = Guid.NewGuid();
            var competingOwners = await Task.WhenAll(
                client.PostAsJsonAsync("/api/auth/login", new LoginRequest(collisionKey, registration.Email, registration.Password)),
                client.PostAsJsonAsync("/api/auth/login", new LoginRequest(collisionKey, other.Email, other.Password)));
            Assert.Single(competingOwners, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Single(competingOwners, response => response.StatusCode == HttpStatusCode.Unauthorized);
        }

        using var restarted = CreateFactory();
        using var restartedClient = restarted.CreateClient();
        Assert.Equal(original, await LoginAsync(restartedClient, registration, key));
        restartedClient.DefaultRequestHeaders.Authorization = Bearer(original);
        Assert.Equal(HttpStatusCode.NoContent, (await restartedClient.PostAsync("/api/auth/logout", null)).StatusCode);
        restartedClient.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await restartedClient.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(key, registration.Email, registration.Password))).StatusCode);
        var fresh = await LoginAsync(restartedClient, registration);
        Assert.NotEqual(original.AccessToken, fresh.AccessToken);
        using var scope = restarted.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        Assert.Equal(1, await context.AccountSessions.CountAsync(session => session.Id == key));
    }

    [Fact]
    public async Task OwnAccountAndConcurrentCurrentLogoutRemainIsolatedAfterRestart()
    {
        LoginSession first;
        LoginSession second;
        LoginSession other;
        CustomerAccount account;

        using (var factory = CreateFactory())
        using (var client = factory.CreateClient())
        {
            var registration = await RegisterAsync(client);
            var otherRegistration = await RegisterAsync(client);
            first = await LoginAsync(client, registration with { Email = $"\t{registration.Email!.ToUpperInvariant()}\u00a0" });
            second = await LoginAsync(client, registration);
            other = await LoginAsync(client, otherRegistration);
            client.DefaultRequestHeaders.Authorization = Bearer(first);
            var response = await client.GetAsync("/api/account");
            Assert.True(response.Headers.CacheControl?.NoStore);
            account = (await response.Content.ReadFromJsonAsync<CustomerAccount>())!;
            Assert.Equal(registration.Email, account.Email);
            Assert.Equal(registration.LegalName, account.LegalName);
            Assert.False(account.EmailVerified);
            client.DefaultRequestHeaders.Authorization = Bearer(other);
            var otherAccount = (await client.GetFromJsonAsync<CustomerAccount>($"/api/account?userId={account.UserId}"))!;
            Assert.Equal(otherRegistration.Email, otherAccount.Email);
            Assert.NotEqual(account.UserId, otherAccount.UserId);
            client.DefaultRequestHeaders.Authorization = Bearer(first);
            var logouts = await Task.WhenAll(client.PostAsync("/api/auth/logout", null), client.PostAsync("/api/auth/logout", null));
            Assert.Contains(logouts, result => result.StatusCode == HttpStatusCode.NoContent);
            Assert.All(logouts, result => Assert.Contains(result.StatusCode, new[] { HttpStatusCode.NoContent, HttpStatusCode.Unauthorized }));
        }

        using var restarted = CreateFactory();
        using var restartedClient = restarted.CreateClient();
        restartedClient.DefaultRequestHeaders.Authorization = Bearer(first);
        Assert.Equal(HttpStatusCode.Unauthorized, (await restartedClient.GetAsync("/api/account")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await restartedClient.PostAsync("/api/auth/logout", null)).StatusCode);
        restartedClient.DefaultRequestHeaders.Authorization = Bearer(second);
        Assert.Equal(account.UserId, (await restartedClient.GetFromJsonAsync<CustomerAccount>("/api/account"))!.UserId);
        restartedClient.DefaultRequestHeaders.Authorization = Bearer(other);
        Assert.NotEqual(account.UserId, (await restartedClient.GetFromJsonAsync<CustomerAccount>("/api/account"))!.UserId);
    }

    [Fact]
    public async Task InvalidCredentialsAndInvalidTransportNeverCreateSessions()
    {
        var verificationLog = new PasswordVerificationLog();
        using var factory = CreateFactory(verificationLog: verificationLog);
        using var client = factory.CreateClient();
        var registration = await RegisterAsync(client);
        var hashesBeforeDenial = verificationLog.CreatedHashes.ToArray();
        var wrongPassword = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(Guid.NewGuid(), registration.Email, "WrongPassword123!"));
        var unknownEmail = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(Guid.NewGuid(), $"aiman.{Guid.NewGuid():N}@example.invalid", "Password123!"));
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        Assert.Equal(await wrongPassword.Content.ReadAsStringAsync(), await unknownEmail.Content.ReadAsStringAsync());
        Assert.True(wrongPassword.Headers.CacheControl?.NoStore);
        var verifications = verificationLog.Verifications.ToArray();
        Assert.Equal(2, verifications.Length);
        using var accountScope = factory.Services.CreateScope();
        var accounts = accountScope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var accountHash = await accounts.Users.Where(user => user.Email == registration.Email)
            .Select(user => user.PasswordHash).SingleAsync();
        Assert.Equal(accountHash, verifications[0].Hash);
        Assert.Contains(verifications[1].Hash, hashesBeforeDenial);
        Assert.NotEqual(accountHash, verifications[1].Hash);
        Assert.All(verifications, verification => Assert.Equal(PasswordVerificationResult.Failed, verification.Result));
        Assert.Equal(hashesBeforeDenial.Length, verificationLog.CreatedHashes.Count);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/auth/login", new StringContent("{", Encoding.UTF8, "application/json"))).StatusCode);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var userId = await context.Users.Where(user => user.Email == registration.Email).Select(user => user.Id).SingleAsync();
        Assert.False(await context.AccountSessions.AnyAsync(session => session.UserId == userId));
    }

    [Fact]
    public async Task TokenMustBeSignedUnexpiredAndBoundToItsPersistedUser()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var registration = await RegisterAsync(client);
        var session = await LoginAsync(client, registration);
        var decoded = new JwtSecurityTokenHandler().ReadJwtToken(session.AccessToken);
        var settings = factory.Services.GetRequiredService<IOptions<JwtSettings>>().Value;
        var now = DateTime.UtcNow;
        var claims = decoded.Claims.ToArray();
        var forgedOwner = claims.Select(claim => claim.Type == JwtRegisteredClaimNames.Sub
            ? new Claim(claim.Type, Guid.NewGuid().ToString()) : claim).ToArray();
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Secret));
        var invalidTokens = new[]
        {
            IssueToken(settings, claims, now.AddMinutes(-2), now.AddMinutes(-1), signingKey),
            IssueToken(settings, forgedOwner, now.AddMinutes(-1), now.AddMinutes(1), signingKey),
            IssueToken(settings, claims.Where(claim => claim.Type != JwtRegisteredClaimNames.Jti), now.AddMinutes(-1), now.AddMinutes(1), signingKey),
            IssueToken(settings, claims, now.AddMinutes(-1), now.AddMinutes(1), new SymmetricSecurityKey(Encoding.UTF8.GetBytes(new string('x', 64))))
        };
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/account")).StatusCode);

        foreach (var token in invalidTokens)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await client.GetAsync("/api/account");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
        }
    }

    [Fact]
    public async Task PersistedExpiryAndChangedSecurityStampRejectOtherwiseValidTokens()
    {
        var clock = new SessionClock();
        using var factory = CreateFactory(clock);
        using var client = factory.CreateClient();
        var registration = await RegisterAsync(client);
        var first = await LoginAsync(client, registration);
        var second = await LoginAsync(client, registration);
        client.DefaultRequestHeaders.Authorization = Bearer(first);
        clock.Now = first.ExpiresAt;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/account")).StatusCode);
        clock.Now = DateTimeOffset.UtcNow;
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/account")).StatusCode);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        await context.Users.Where(user => user.Email == registration.Email)
            .ExecuteUpdateAsync(update => update.SetProperty(user => user.SecurityStamp, Guid.NewGuid().ToString()));

        foreach (var session in new[] { first, second })
        {
            client.DefaultRequestHeaders.Authorization = Bearer(session);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/account")).StatusCode);
        }
    }

    [Fact]
    public async Task InterruptedSessionPersistenceReturnsNoTokenAndFreshLoginWorksAfterRestart()
    {
        RegistrationRequest registration;

        using (var factory = CreateFactory(interruptSession: true))
        using (var client = factory.CreateClient())
        {
            registration = await RegisterAsync(client);
            var interrupted = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(Guid.NewGuid(), registration.Email, registration.Password));
            Assert.Equal(HttpStatusCode.InternalServerError, interrupted.StatusCode);
            Assert.DoesNotContain("accessToken", await interrupted.Content.ReadAsStringAsync());
            using var scope = factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
            var userId = await context.Users.Where(user => user.Email == registration.Email).Select(user => user.Id).SingleAsync();
            Assert.False(await context.AccountSessions.AnyAsync(session => session.UserId == userId));
        }

        using var restarted = CreateFactory();
        using var restartedClient = restarted.CreateClient();
        restartedClient.DefaultRequestHeaders.Authorization = Bearer(await LoginAsync(restartedClient, registration));
        Assert.Equal(HttpStatusCode.OK, (await restartedClient.GetAsync("/api/account")).StatusCode);
    }

    [Fact]
    public async Task RestrictedSqlEnforcesSessionRelationshipLifetimeAndPermanentRevocation()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var registration = await RegisterAsync(client);
        var login = await LoginAsync(client, registration);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(login.AccessToken);
        var id = Guid.Parse(token.Id);
        await using var connection = new NpgsqlConnection(database.RuntimeConnectionString);
        await connection.OpenAsync();
        const string copy = """
            INSERT INTO "AccountSessions" ("Id", "UserId", "SecurityStamp", "CreatedAt", "ExpiresAt", "RevokedAt")
            SELECT @newId, {0}, {1}, "CreatedAt", {2}, {3} FROM "AccountSessions" WHERE "Id" = @id
            """;
        var rejectedStatements = new (string Sql, string State)[]
        {
            (string.Format(System.Globalization.CultureInfo.InvariantCulture, copy, "\"UserId\"", "\"SecurityStamp\"", "\"ExpiresAt\"", "NULL")
                .Replace("@newId", "'00000000-0000-0000-0000-000000000000'::uuid", StringComparison.Ordinal), PostgresErrorCodes.CheckViolation),
            (string.Format(System.Globalization.CultureInfo.InvariantCulture, copy, "@orphan", "\"SecurityStamp\"", "\"ExpiresAt\"", "NULL"), PostgresErrorCodes.ForeignKeyViolation),
            (string.Format(System.Globalization.CultureInfo.InvariantCulture, copy, "\"UserId\"", "\"SecurityStamp\"", "\"CreatedAt\"", "NULL"), PostgresErrorCodes.CheckViolation),
            (string.Format(System.Globalization.CultureInfo.InvariantCulture, copy, "\"UserId\"", "NULL", "\"ExpiresAt\"", "NULL"), PostgresErrorCodes.NotNullViolation),
            (string.Format(System.Globalization.CultureInfo.InvariantCulture, copy, "\"UserId\"", "' '", "\"ExpiresAt\"", "NULL"), PostgresErrorCodes.CheckViolation),
            (string.Format(System.Globalization.CultureInfo.InvariantCulture, copy, "\"UserId\"", "\"SecurityStamp\"", "\"ExpiresAt\"", "\"CreatedAt\" - interval '1 second'"), PostgresErrorCodes.CheckViolation),
            ("INSERT INTO \"AccountSessions\" SELECT * FROM \"AccountSessions\" WHERE \"Id\" = @id", PostgresErrorCodes.UniqueViolation),
            ("UPDATE \"AccountSessions\" SET \"UserId\" = @orphan WHERE \"Id\" = @id", PostgresErrorCodes.CheckViolation),
            ("UPDATE \"AccountSessions\" SET \"ExpiresAt\" = \"ExpiresAt\" + interval '1 hour' WHERE \"Id\" = @id", PostgresErrorCodes.CheckViolation),
            ("DELETE FROM \"AccountSessions\" WHERE \"Id\" = @id", PostgresErrorCodes.InsufficientPrivilege)
        };

        foreach (var (sql, state) in rejectedStatements)
        {
            await AssertSqlRejectedAsync(connection, sql, state, id);
        }

        client.DefaultRequestHeaders.Authorization = Bearer(login);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        await AssertSqlRejectedAsync(connection, "UPDATE \"AccountSessions\" SET \"RevokedAt\" = NULL WHERE \"Id\" = @id",
            PostgresErrorCodes.CheckViolation, id);
        const string relationship = """
            SELECT convalidated AND confdeltype = 'r' AND confrelid = '"AspNetUsers"'::regclass
            FROM pg_constraint WHERE conrelid = '"AccountSessions"'::regclass
                AND conname = 'FK_AccountSessions_AspNetUsers_UserId' AND contype = 'f'
            """;
        await using var check = new NpgsqlCommand(relationship, connection);
        Assert.Equal(true, await check.ExecuteScalarAsync());

        await using var maintenance = new NpgsqlConnection(database.ConnectionString);
        await maintenance.OpenAsync();
        await using var transaction = await maintenance.BeginTransactionAsync();
        var userId = Guid.Parse(token.Subject);
        await using var removeProfile = new NpgsqlCommand("DELETE FROM \"CustomerProfiles\" WHERE \"UserId\" = @user", maintenance, transaction);
        removeProfile.Parameters.AddWithValue("user", userId);
        await removeProfile.ExecuteNonQueryAsync();
        await using var removeUser = new NpgsqlCommand("DELETE FROM \"AspNetUsers\" WHERE \"Id\" = @user", maintenance, transaction);
        removeUser.Parameters.AddWithValue("user", userId);
        var parentDeletion = await Assert.ThrowsAsync<PostgresException>(async () => await removeUser.ExecuteNonQueryAsync());
        Assert.Equal("FK_AccountSessions_AspNetUsers_UserId", parentDeletion.ConstraintName);
        await transaction.RollbackAsync();
    }

    private static async Task AssertSqlRejectedAsync(NpgsqlConnection connection, string sql, string state, Guid id)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("newId", Guid.NewGuid());
        command.Parameters.AddWithValue("orphan", Guid.NewGuid());
        var exception = await Assert.ThrowsAsync<PostgresException>(async () => await command.ExecuteNonQueryAsync());
        Assert.Equal(state, exception.SqlState);
    }

    private WebApplicationFactory<Program> CreateFactory(SessionClock? clock = null, bool interruptSession = false,
        PasswordVerificationLog? verificationLog = null)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                if (verificationLog is not null)
                {
                    services.Configure<PasswordHasherOptions>(options => options.IterationCount = 120000);
                    services.AddScoped<IPasswordHasher<AccountUser>>(provider =>
                    {
                        return new RecordingPasswordHasher(provider.GetRequiredService<IOptions<PasswordHasherOptions>>(), verificationLog);
                    });
                }

                services.RemoveAll<DbContextOptions<DealershipDbContext>>();
                services.RemoveAll<DealershipDbContext>();
                services.AddDbContext<DealershipDbContext>(options =>
                {
                    options.UseNpgsql(database.RuntimeConnectionString);

                    if (interruptSession)
                    {
                        options.AddInterceptors(new SessionFailureInterceptor());
                    }
                });

                if (clock is not null)
                {
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton<TimeProvider>(clock);
                }
            });
        });
    }

    private static string IssueToken(JwtSettings settings, IEnumerable<Claim> claims, DateTime from, DateTime to, SecurityKey key)
    {
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(settings.Issuer, settings.Audience,
            claims, from, to, new SigningCredentials(key, SecurityAlgorithms.HmacSha256)));
    }

    private static AuthenticationHeaderValue Bearer(LoginSession session)
    {
        return new AuthenticationHeaderValue("Bearer", session.AccessToken);
    }

    private static async Task<LoginSession> LoginAsync(HttpClient client, RegistrationRequest registration, Guid? key = null)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(key ?? Guid.NewGuid(), registration.Email, registration.Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        return (await response.Content.ReadFromJsonAsync<LoginSession>())!;
    }

    private static async Task<RegistrationRequest> RegisterAsync(HttpClient client)
    {
        var serial = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000, 1000000);
        var registration = new RegistrationRequest(Guid.NewGuid(), "Nur Aina Abdullah", $"nur.aina.{Guid.NewGuid():N}@example.invalid",
            "Password123!", $"940215{serial}", "0123456789", "18 Jalan Melati, Petaling Jaya, Selangor");
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/register", registration)).StatusCode);
        return registration;
    }

    private sealed class SessionClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow()
        {
            return Now;
        }
    }

    private sealed class SessionFailureInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context?.ChangeTracker.Entries<AccountSession>().Any(entry => entry.State == EntityState.Added) == true)
            {
                throw new InvalidOperationException("Interrupted session persistence.");
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
