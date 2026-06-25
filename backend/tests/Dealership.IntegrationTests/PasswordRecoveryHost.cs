using Dealership.Application.Accounts;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dealership.IntegrationTests;

internal sealed class PasswordRecoveryHost : IDisposable
{
    public WebApplicationFactory<Program> Factory { get; }

    public HttpClient Client { get; }

    public PasswordRecoveryHost(RegistrationDatabase database, RecoveryClock? clock = null,
        SaveChangesInterceptor? interceptor = null, IMailTransport? sender = null)
    {
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
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

                    if (interceptor is not null)
                    {
                        options.AddInterceptors(interceptor);
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
        Client = Factory.CreateClient();
    }

    public async Task<RegistrationRequest> RegisterAsync()
    {
        var request = new RegistrationRequest(Guid.NewGuid(), "Nur Aina Abdullah",
            $"nur.aina.{Guid.NewGuid():N}@example.invalid", "Password123!",
            $"940215{Random.Shared.Next(100000, 1000000)}", "0123456789", "18 Jalan Melati, Petaling Jaya, Selangor", TestUsernames.Next());
        using var scope = Factory.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<RegistrationService>()
            .RegisterAsync(request, CancellationToken.None);
        Assert.Equal(RegistrationStatus.Succeeded, result.Status);
        return request;
    }

    public async Task RequestAsync(string email)
    {
        using var scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<PasswordRecoveryService>()
            .RequestAsync(new PasswordRecoveryRequest(email), CancellationToken.None);
    }

    public async Task<PasswordResetResult> ResetAsync(string token, string password = "Recovered123!")
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<PasswordRecoveryService>()
            .ResetAsync(new PasswordResetRequest(token, password), CancellationToken.None);
    }

    public async Task<LoginSession?> LoginAsync(RegistrationRequest request, string? password = null)
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AuthenticationService>()
            .LoginAsync(new LoginRequest(Guid.NewGuid(), request.Email, password ?? request.Password), CancellationToken.None);
    }

    public async Task<PasswordResetChallenge> LatestAsync(string email)
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<DealershipDbContext>().PasswordResetChallenges.AsNoTracking()
            .Where(challenge => challenge.RecipientEmail == email).OrderByDescending(challenge => challenge.CreatedAt).FirstAsync();
    }

    public string Token(PasswordResetChallenge challenge) => Factory.Services.GetRequiredService<PasswordResetToken>().Create(challenge);

    public void Dispose()
    {
        Client.Dispose();
        Factory.Dispose();
    }
}

internal sealed class RecoveryClock : TimeProvider
{
    // Leave room for clock advances without making JWT not-before later than the bearer validator's wall clock.
    public DateTimeOffset Now { get; set; } = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()).AddMinutes(-10);

    public override DateTimeOffset GetUtcNow() => Now;
}
