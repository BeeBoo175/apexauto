using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
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

namespace Dealership.BrowserAuthHarness;

internal static class RecoveryBrowserHarness
{
    public static async Task<int> RunAsync(string[] args)
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
            foreach (var project in new[] { "desktop-auth", "mobile-auth" })
            {
                foreach (var scenario in new[] { "request", "reset", "fragment" })
                {
                    var exitCode = await RunScenarioAsync(project, scenario, args, database, evidence, frontend);

                    if (exitCode != 0)
                    {
                        return exitCode;
                    }
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

    private static void ValidateInbox()
    {
        var inbox = Environment.GetEnvironmentVariable("AUTH_MAILPIT_BASE_URL");

        if (!Uri.TryCreate(inbox, UriKind.Absolute, out var address) ||
            address.Scheme != "http" || address.Host is not ("localhost" or "127.0.0.1" or "::1") ||
            address.UserInfo.Length != 0 || address.AbsolutePath != "/" ||
            address.Query.Length != 0 || address.Fragment.Length != 0)
        {
            throw new InvalidOperationException("Recovery browser acceptance requires an explicit loopback Mailpit origin.");
        }
    }

    private static async Task<int> RunScenarioAsync(string project, string scenario, string[] args,
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
            var nextOrigin = AllocateNextOrigin();
            await using var ingress = await TrustedIngress.StartAsync(nextOrigin);
            var browserOrigin = TrustedIngress.Origin(ingress);
            using (var factory = CreateFactory(database, frontend, state, browserOrigin))
            using (var client = factory.CreateClient())
            {
                account = await RecoveryBrowserFixture.PrepareAsync(factory.Services, scenario);
                File.WriteAllText(Path.Combine(directory, "recovery.json"), JsonSerializer.Serialize(account));
                var exitCode = await RunPlaywrightAsync(project, scenario, args, directory, frontend, client,
                    browserOrigin, nextOrigin);

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

    private static string AllocateNextOrigin()
    {
        using var portProbe = new TcpListener(IPAddress.Loopback, 0);
        portProbe.Start();
        var frontendPort = ((IPEndPoint)portProbe.LocalEndpoint).Port;
        return $"http://127.0.0.1:{frontendPort}";
    }

    private static async Task<int> RunPlaywrightAsync(string project, string scenario, string[] args,
        string evidence, string frontend, HttpClient client, string browserOrigin, string nextOrigin)
    {
        var start = new ProcessStartInfo("node") { WorkingDirectory = frontend, UseShellExecute = false };
        start.ArgumentList.Add("node_modules/@playwright/test/cli.js");
        start.ArgumentList.Add("test");
        start.ArgumentList.Add("--config=playwright.config.ts");
        start.ArgumentList.Add($"--project={project}");
        start.ArgumentList.Add("e2e/browser-auth-password-recovery.spec.ts");
        start.ArgumentList.Add($"--grep=recovery {scenario}:");

        foreach (var argument in args)
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment["API_BASE_URL"] = client.BaseAddress!.GetLeftPart(UriPartial.Authority);
        start.Environment["APP_BASE_URL"] = browserOrigin;
        start.Environment["AUTH_NEXT_ORIGIN"] = nextOrigin;
        start.Environment["TRUSTED_CLIENT_IP_HEADER"] = TrustedIngress.ClientHeader;
        start.Environment["AUTH_EVIDENCE_DIR"] = evidence;
        start.Environment["AUTH_RECOVERY_SCENARIO"] = scenario;
        start.Environment.Remove("NO_COLOR");
        start.Environment.Remove("FORCE_COLOR");
        using var browser = Process.Start(start) ?? throw new InvalidOperationException("Recovery browser runner did not start.");
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(5));

        try
        {
            await browser.WaitForExitAsync(deadline.Token);
            return browser.ExitCode;
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            browser.Kill(entireProcessTree: true);
            await browser.WaitForExitAsync();
            throw new TimeoutException("Recovery browser scenario exceeded its five-minute deadline.");
        }
    }
}
