using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Dealership.Application.Accounts;
using Dealership.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace Dealership.IntegrationTests;

public sealed class CustomerRegistrationTests(RegistrationDatabase database) : IClassFixture<RegistrationDatabase>
{
    [Fact]
    public async Task RestrictedSqlRejectsEveryDotNetWhitespaceAtEmailBoundaries()
    {
        using var services = database.CreateServices();
        var request = NewRequest();
        await RegisterDirectAsync(services, request);
        var other = NewRequest();
        await RegisterDirectAsync(services, other);
        var otherId = await FindUserIdAsync(other.RegistrationKey);
        const string updateEmail = "UPDATE \"AspNetUsers\" SET \"Email\" = @email, \"NormalizedEmail\" = upper(@email) WHERE \"Id\" = @id";
        await using var connection = new NpgsqlConnection(database.RuntimeConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(updateEmail, connection);
        command.Parameters.AddWithValue("id", otherId);
        var emailParameter = command.Parameters.Add("email", NpgsqlTypes.NpgsqlDbType.Text);
        var whitespaceCharacters = Enumerable.Range(char.MinValue, char.MaxValue + 1)
            .Select(value => (char)value).Where(char.IsWhiteSpace);

        foreach (var whitespace in whitespaceCharacters)
        {
            foreach (var candidate in new[] { whitespace + request.Email, request.Email + whitespace })
            {
                emailParameter.Value = candidate;
                var failure = await Record.ExceptionAsync(async () => await command.ExecuteNonQueryAsync());
                var boundary = candidate!.StartsWith(whitespace) ? "leading" : "trailing";
                Assert.True(failure is PostgresException { SqlState: PostgresErrorCodes.CheckViolation },
                    $"Expected email CHECK rejection for {boundary} U+{(int)whitespace:X4}.");
            }
        }
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var savedEmail = await context.Users.Where(user => user.Id == otherId).Select(user => user.Email).SingleAsync();
        Assert.Equal(other.Email, savedEmail);
    }

    [Fact]
    public async Task RegistrationCreatesOneUnverifiedCustomerAndHashWithoutReturningPrivateData()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var original = NewRequest() with { Email = $"farid.o'hassan.{Guid.NewGuid():N}@example.invalid" };
        var request = original with { Email = $"\t\u00A0{original.Email}\u202F\r\n" };
        var response = await client.PostAsJsonAsync("/api/auth/register", request);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync());
        Assert.False(response.Headers.Contains("Set-Cookie"));
        Assert.True(response.Headers.CacheControl?.NoStore);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var profile = await context.CustomerProfiles.SingleAsync(profile => profile.RegistrationKey == request.RegistrationKey);
        var user = await context.Users.SingleAsync(user => user.Id == profile.UserId);
        Assert.Equal(request.LegalName, profile.LegalName);
        Assert.Equal(request.Nric, profile.Nric);
        Assert.False(user.EmailConfirmed);
        Assert.Equal(AccountRoles.Customer, user.Role);
        Assert.Null(user.BranchId);
        Assert.True(user.IsActive);
        Assert.NotEqual(request.Password, user.PasswordHash);
        Assert.NotNull(user.PasswordHash);
        Assert.Equal(original.Email, user.Email);
        Assert.Equal(original.Email!.ToUpperInvariant(), user.NormalizedEmail);
    }

    [Theory]
    [InlineData("Password123 ")]
    public async Task PasswordPolicyRejectsInvalidPasswordsWithoutPartialUsers(string password)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var request = NewRequest() with { Password = password };
        var response = await client.PostAsJsonAsync("/api/auth/register", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        Assert.False(await context.Users.AnyAsync(user => user.Email == request.Email));
    }

    [Fact]
    public async Task InterruptedProfileWriteRollsBackUserAndRetryAfterRestartSucceeds()
    {
        var request = NewRequest();

        using (var interrupted = CreateFactory(interruptProfileWrite: true))
        using (var client = interrupted.CreateClient())
        {
            Assert.Equal(HttpStatusCode.InternalServerError,
                (await client.PostAsJsonAsync("/api/auth/register", request)).StatusCode);
        }

        using var restarted = CreateFactory();
        using var scope = restarted.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        Assert.False(await context.Users.AnyAsync(user => user.Email == request.Email));
        Assert.False(await context.CustomerProfiles.AnyAsync(profile => profile.RegistrationKey == request.RegistrationKey));
        using var restartedClient = restarted.CreateClient();
        Assert.Equal(HttpStatusCode.NoContent, (await restartedClient.PostAsJsonAsync("/api/auth/register", request)).StatusCode);
    }

    [Fact]
    public async Task SameRequestReplaysAfterNewApplicationStartupAndChangedPayloadConflicts()
    {
        var request = NewRequest();

        using (var first = CreateFactory())
        using (var client = first.CreateClient())
        {
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/register", request)).StatusCode);
        }

        using var restarted = CreateFactory();
        using var restartedClient = restarted.CreateClient();
        Assert.Equal(HttpStatusCode.NoContent, (await restartedClient.PostAsJsonAsync("/api/auth/register", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await restartedClient.PostAsJsonAsync("/api/auth/register", request with { Password = "Different123!" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await restartedClient.PostAsJsonAsync("/api/auth/register", request with { LegalName = "Nur Aisyah Abdullah" })).StatusCode);
    }

    [Fact]
    public async Task ConcurrentNormalizedEmailAndNricConflictsLeaveOnlyOneCompleteAccount()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var request = NewRequest();
        var responses = await Task.WhenAll(
            client.PostAsJsonAsync("/api/auth/register", request),
            client.PostAsJsonAsync("/api/auth/register", request with
            {
                RegistrationKey = Guid.NewGuid(),
                Email = $"\t\u00A0{request.Email!.ToUpperInvariant()}\u202F\r\n",
                Nric = NewNric()
            }));
        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.NoContent));
        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
        var nricRequest = NewRequest();
        var nricResponses = await Task.WhenAll(
            client.PostAsJsonAsync("/api/auth/register", nricRequest),
            client.PostAsJsonAsync("/api/auth/register", nricRequest with { RegistrationKey = Guid.NewGuid(), Email = NewRequest().Email, Username = TestUsernames.Next() }));
        Assert.Equal(1, nricResponses.Count(response => response.StatusCode == HttpStatusCode.NoContent));
        Assert.Equal(1, nricResponses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        Assert.Equal(await context.Users.CountAsync(), await context.CustomerProfiles.CountAsync());
    }

    [Fact]
    public async Task ConcurrentIdenticalReplayCreatesOneCustomer()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var request = NewRequest();
        var responses = await Task.WhenAll(Enumerable.Range(0, 4)
            .Select(_ => client.PostAsJsonAsync("/api/auth/register", request)));
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.NoContent, response.StatusCode));
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        Assert.Equal(1, await context.CustomerProfiles.CountAsync(profile => profile.RegistrationKey == request.RegistrationKey));
    }

    [Fact]
    public async Task RestrictedSqlRejectsOrphansInvalidNricDuplicateIdentityAndUnnormalizedEmail()
    {
        using var services = database.CreateServices();
        await using var connection = new NpgsqlConnection(database.RuntimeConnectionString);
        await connection.OpenAsync();
        const string sql = "SELECT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = '\"AspNetUsers\"'::regclass AND conname = 'CK_Users_EmailNormalization' AND contype = 'c' AND convalidated) AND EXISTS (SELECT 1 FROM pg_index WHERE indrelid = '\"AspNetUsers\"'::regclass AND indexrelid = '\"EmailIndex\"'::regclass AND indisunique AND indisvalid)";
        await using var command = new NpgsqlCommand(sql, connection);
        Assert.Equal(true, await command.ExecuteScalarAsync());

        var request = NewRequest();
        await RegisterDirectAsync(services, request);
        await AssertSqlRejectedAsync("INSERT INTO \"CustomerProfiles\" (\"UserId\", \"RegistrationKey\", \"LegalName\", \"Nric\", \"Mobile\", \"ResidentialAddress\") VALUES (@id, @key, 'Farid Hassan', @nric, '0123456789', '24 Jalan Kenanga, Shah Alam')",
            PostgresErrorCodes.ForeignKeyViolation, Guid.NewGuid(), Guid.NewGuid(), NewNric());
        var userId = await FindUserIdAsync(request.RegistrationKey);
        await AssertSqlRejectedAsync("UPDATE \"CustomerProfiles\" SET \"Nric\" = 'bad' WHERE \"UserId\" = @id",
            PostgresErrorCodes.CheckViolation, userId);
        await AssertSqlRejectedAsync("UPDATE \"CustomerProfiles\" SET \"Mobile\" = E'\\t' WHERE \"UserId\" = @id",
            PostgresErrorCodes.CheckViolation, userId);
        await AssertSqlRejectedAsync("UPDATE \"AspNetUsers\" SET \"Email\" = ' untrimmed@example.invalid ' WHERE \"Id\" = @id",
            PostgresErrorCodes.CheckViolation, userId);
        await AssertSqlRejectedAsync("DELETE FROM \"AspNetUsers\" WHERE \"Id\" = @id",
            PostgresErrorCodes.InsufficientPrivilege, userId);

        var other = NewRequest();
        await RegisterDirectAsync(services, other);
        var otherId = await FindUserIdAsync(other.RegistrationKey);
        await AssertSqlRejectedAsync("UPDATE \"CustomerProfiles\" SET \"Nric\" = @nric WHERE \"UserId\" = @id",
            PostgresErrorCodes.UniqueViolation, otherId, nric: request.Nric);
        await AssertSqlRejectedAsync("UPDATE \"CustomerProfiles\" SET \"RegistrationKey\" = @key WHERE \"UserId\" = @id",
            PostgresErrorCodes.UniqueViolation, otherId, request.RegistrationKey);
        await AssertSqlRejectedAsync("UPDATE \"AspNetUsers\" SET \"Email\" = @email, \"NormalizedEmail\" = upper(@email) WHERE \"Id\" = @id",
            PostgresErrorCodes.UniqueViolation, otherId, email: request.Email);
        await AssertSqlRejectedAsync("DELETE FROM \"AspNetUsers\" WHERE \"Id\" = @id",
            PostgresErrorCodes.ForeignKeyViolation, userId, maintenance: true);
    }

    [Fact]
    public async Task InvalidProfileMapsFieldErrorsWithoutPersistingAnAccount()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var request = NewRequest() with
        {
            Nric = "９００１０１０１０１２３",
            LegalName = "  ",
            RegistrationKey = Guid.Empty
        };
        var response = await client.PostAsJsonAsync("/api/auth/register", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = json.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("nric", out _));
        Assert.True(errors.TryGetProperty("legalName", out _));
        Assert.True(errors.TryGetProperty("registrationKey", out _));
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        Assert.False(await context.Users.AnyAsync(user => user.Email == request.Email));
    }

    [Fact]
    public async Task UsernameConflictIgnoresCaseAndLeavesNoSecondAccount()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var first = NewRequest() with { Username = "Farid.Hassan_88" };
        var second = NewRequest() with { Username = "FARID.hassan_88" };
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/register", first)).StatusCode);

        var response = await client.PostAsJsonAsync("/api/auth/register", second);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("errors").TryGetProperty("username", out _));
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        Assert.False(await context.Users.AnyAsync(user => user.Email == second.Email));
        Assert.Equal("Farid.Hassan_88", (await context.Users.SingleAsync(user => user.Email == first.Email)).AccountUsername);
    }

    [Fact]
    public async Task TransportRejectsMalformedWrongMediaAndOversizedBodies()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/auth/register", new StringContent("{", Encoding.UTF8, "application/json"))).StatusCode);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, (await client.PostAsync("/api/auth/register", new StringContent("{}"))).StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await client.PostAsync("/api/auth/register", new StringContent(new string('x', 17000), Encoding.UTF8, "application/json"))).StatusCode);
    }

    private static async Task RegisterDirectAsync(ServiceProvider services, RegistrationRequest request)
    {
        using var scope = services.CreateScope();
        var registration = scope.ServiceProvider.GetRequiredService<RegistrationService>();
        var result = await registration.RegisterAsync(request, CancellationToken.None);
        Assert.Equal(RegistrationStatus.Succeeded, result.Status);
    }

    private WebApplicationFactory<Program> CreateFactory(bool interruptProfileWrite = false)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<DealershipDbContext>>();
                services.RemoveAll<DealershipDbContext>();
                services.AddDbContext<DealershipDbContext>(options =>
                {
                    options.UseNpgsql(database.RuntimeConnectionString);

                    if (interruptProfileWrite)
                    {
                        options.AddInterceptors(new RegistrationFailureInterceptor());
                    }
                });
            });
        });
    }

    private async Task<Guid> FindUserIdAsync(Guid registrationKey)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT \"UserId\" FROM \"CustomerProfiles\" WHERE \"RegistrationKey\" = @key", connection);
        command.Parameters.AddWithValue("key", registrationKey);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    private async Task AssertSqlRejectedAsync(string sql, string state, Guid id, Guid? key = null,
        string? nric = null, string? email = null, bool maintenance = false)
    {
        await using var connection = new NpgsqlConnection(maintenance ? database.ConnectionString : database.RuntimeConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("key", key ?? Guid.NewGuid());
        command.Parameters.AddWithValue("nric", nric ?? NewNric());
        command.Parameters.AddWithValue("email", email ?? "farid.hassan@example.invalid");
        var exception = await Assert.ThrowsAsync<PostgresException>(async () => await command.ExecuteNonQueryAsync());
        Assert.Equal(state, exception.SqlState);
    }

    private static RegistrationRequest NewRequest()
    {
        return new RegistrationRequest(Guid.NewGuid(), "Farid Hassan", $"farid.hassan.{Guid.NewGuid():N}@example.invalid",
            "Password123!", NewNric(), "0123456789", "24 Jalan Kenanga, Shah Alam, Selangor", TestUsernames.Next());
    }

    private static string NewNric()
    {
        var year = System.Security.Cryptography.RandomNumberGenerator.GetInt32(70, 100);
        var month = System.Security.Cryptography.RandomNumberGenerator.GetInt32(1, 13);
        var day = System.Security.Cryptography.RandomNumberGenerator.GetInt32(1, 29);
        var state = System.Security.Cryptography.RandomNumberGenerator.GetInt32(1, 17);
        var serial = System.Security.Cryptography.RandomNumberGenerator.GetInt32(1000, 10000);
        return $"{year:00}{month:00}{day:00}{state:00}{serial:0000}";
    }
}
