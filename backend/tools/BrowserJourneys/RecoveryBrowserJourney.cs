using System.Text.Json;
using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Dealership.IntegrationTests;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dealership.BrowserJourneys;

internal static class RecoveryBrowserJourney
{
    public static async Task<int> RunAsync(string scenario, string[] projects)
    {
        ValidateInbox();
        var repository = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../.."));
        var frontend = Path.Combine(repository, "frontend");
        var evidence = Path.Combine(repository, ".verifications", "password-recovery-browser",
            $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(evidence);
        var database = new RegistrationDatabase();
        await database.InitializeAsync();
        Environment.SetEnvironmentVariable("ASPNETCORE_URLS", "http://127.0.0.1:0");
        Environment.SetEnvironmentVariable("AllowedHosts", "localhost;127.0.0.1");

        try
        {
            foreach (var project in projects)
            {
                var exitCode = await RunScenarioAsync(project, scenario, database, evidence, frontend);

                if (exitCode != 0)
                {
                    return exitCode;
                }
            }

            Console.WriteLine($"Recovery browser evidence: {evidence}");
            return 0;
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    internal static void ValidateInbox()
    {
        var inbox = Environment.GetEnvironmentVariable("BROWSER_MAILPIT_BASE_URL");

        if (!Uri.TryCreate(inbox, UriKind.Absolute, out var address) ||
            address.Scheme != "http" || address.Host is not ("localhost" or "127.0.0.1" or "::1") ||
            address.UserInfo.Length != 0 || address.AbsolutePath != "/" ||
            address.Query.Length != 0 || address.Fragment.Length != 0)
        {
            throw new InvalidOperationException("Recovery browser acceptance requires an explicit loopback Mailpit origin.");
        }
    }

    private static async Task<int> RunScenarioAsync(string project, string scenario,
        RegistrationDatabase database, string evidence, string frontend)
    {
        var directory = Path.Combine(evidence, $"{project}-{scenario}");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "counts.json"), "{}");
        File.WriteAllText(Path.Combine(directory, "fault.txt"), string.Empty);
        var state = new BrowserEvidence(directory);
        RecoveryBrowserAccount account;

        try
        {
            var nextOrigin = BrowserProcess.AllocateNextOrigin();
            await using var ingress = await TrustedIngress.StartAsync(nextOrigin);
            var browserOrigin = TrustedIngress.Origin(ingress);
            using (var factory = CreateFactory(database, frontend, state, browserOrigin))
            using (var client = factory.CreateClient())
            {
                account = await RecoveryBrowserFixture.PrepareAsync(factory.Services, scenario);
                File.WriteAllText(Path.Combine(directory, "recovery.json"), JsonSerializer.Serialize(account));
                var exitCode = await BrowserProcess.RunAsync(frontend, project,
                    ["browser-auth-password-recovery.spec.ts", $"--grep=recovery {scenario}:"],
                    directory, client.BaseAddress!, browserOrigin, nextOrigin,
                    new Dictionary<string, string> { ["BROWSER_RECOVERY_SCENARIO"] = scenario });

                if (exitCode != 0)
                {
                    return exitCode;
                }
            }

            File.Copy(Path.Combine(directory, "counts.json"), Path.Combine(directory, "browser-counts.json"));
            using var restarted = CreateFactory(database, frontend, new BrowserEvidence(directory), browserOrigin);
            using var restartedClient = restarted.CreateClient();
            await RecoveryBrowserPersistence.VerifyAsync(restarted.Services, restartedClient, account, scenario, directory);
            return 0;
        }
        finally
        {
            File.Delete(Path.Combine(directory, "recovery.json"));
            File.Delete(Path.Combine(directory, "recovered-session.txt"));
            File.Delete(Path.Combine(directory, "fault.txt"));
        }
    }

    private static WebApplicationFactory<global::Program> CreateFactory(RegistrationDatabase database,
        string frontend, BrowserEvidence state, string browserOrigin)
    {
        var factory = new WebApplicationFactory<global::Program>().WithWebHostBuilder(builder =>
        {
            builder.UseContentRoot(Path.Combine(Path.GetDirectoryName(frontend)!, "backend", "src", "Dealership.Api"));
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ForwardedHeaders:KnownProxies:0"] = "127.0.0.1",
                    ["ForwardedHeaders:KnownProxies:1"] = "::1",
                    ["APP_BASE_URL"] = browserOrigin,
                    ["RateLimiting:ApiRequestsPerWindow"] = "10000",
                    ["RateLimiting:PictureReadsPerWindow"] = "10000",
                    ["Email:Mode"] = "Loopback",
                    ["Email:SmtpHost"] = "127.0.0.1",
                    ["Email:SmtpPort"] = "1025",
                    ["Email:SenderEmail"] = "no-reply@apexauto.invalid",
                    ["Email:EnableSsl"] = "false",
                    ["IntakePhotos:CleanupEnabled"] = "false"
                }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<DealershipDbContext>>();
                services.RemoveAll<DealershipDbContext>();
                services.AddDbContext<DealershipDbContext>(options => options.UseNpgsql(database.RuntimeConnectionString));
                services.RemoveAll<IMailTransport>();
                services.AddSingleton<IMailTransport, RecoveryLoopbackMailTransport>();
                services.AddSingleton<IStartupFilter>(state);
            });
        });
        factory.UseKestrel(0);
        return factory;
    }

}
