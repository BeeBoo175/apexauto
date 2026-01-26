using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text.Json;
using Dealership.Infrastructure.Persistence;
using Dealership.IntegrationTests;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using Dealership.Application.Accounts;
using Dealership.Infrastructure.Development;
using Dealership.Application.Branches;

namespace Dealership.BrowserAuthHarness;

internal static class BrowserHarness
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Contains("--recovery", StringComparer.Ordinal))
        {
            return await RecoveryBrowserHarness.RunAsync(args.Where(argument => argument != "--recovery").ToArray());
        }

        var repository = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../.."));
        var frontend = Path.Combine(repository, "frontend");
        var evidence = Path.Combine(repository, ".verifications", "browser-auth", $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(evidence);
        var state = new BrowserEvidence(evidence);
        File.WriteAllText(Path.Combine(evidence, "counts.json"), "{}");
        File.WriteAllText(Path.Combine(evidence, "fault.txt"), string.Empty);
        File.Delete(Path.Combine(evidence, "revoked-token.txt"));
        File.WriteAllText(Path.Combine(evidence, "profiles.json"), "[]");
        File.WriteAllText(Path.Combine(evidence, "confirmed-emails.json"), "[]");
        var database = new RegistrationDatabase();
        var guestPageFixtures = args.Contains("--guest-pages", StringComparer.Ordinal);
        args = args.Where(argument => argument != "--guest-pages").ToArray();
        await database.InitializeAsync();
        Environment.SetEnvironmentVariable("ASPNETCORE_URLS", "http://127.0.0.1:0");
        Environment.SetEnvironmentVariable("AllowedHosts", "localhost;127.0.0.1");

        try
        {
            foreach (var project in new[] { "desktop-auth", "mobile-auth" })
            {
                var exitCode = await RunBrowserAsync(project, args, database, state, evidence, frontend, guestPageFixtures);

                if (exitCode != 0)
                {
                    return exitCode;
                }
            }

            await VerifyPersistenceAsync(database, evidence);
            return 0;
        }
        finally
        {
            await database.DisposeAsync();
            File.Delete(Path.Combine(evidence, "fault.txt"));
            File.Delete(Path.Combine(evidence, "revoked-token.txt"));
        }
    }

    private static async Task<int> RunBrowserAsync(string project, string[] args, RegistrationDatabase database,
        BrowserEvidence state, string evidence, string frontend, bool guestPageFixtures)
    {
        using var factory = new WebApplicationFactory<global::Program>().WithWebHostBuilder(builder =>
        {
            builder.UseContentRoot(Path.Combine(Path.GetDirectoryName(frontend)!, "backend", "src", "Dealership.Api"));
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ForwardedHeaders:KnownProxies:0"] = "127.0.0.1",
                    ["ForwardedHeaders:KnownProxies:1"] = "::1"
                }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<DealershipDbContext>>();
                services.RemoveAll<DealershipDbContext>();
                services.AddDbContext<DealershipDbContext>(options => options.UseNpgsql(database.RuntimeConnectionString));
                services.AddSingleton<IStartupFilter>(state);
            });
        });
        factory.UseKestrel(0);
        using var client = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
        {
            var registration = scope.ServiceProvider.GetRequiredService<RegistrationService>();
            var branch = DevelopmentFixtureManifest.Branches[0];
            var branchResult = await scope.ServiceProvider.GetRequiredService<BranchProvisioningService>()
                .ProvisionBranchAsync(branch, CancellationToken.None);

            if (!branchResult.MatchesRequestedState)
            {
                throw new InvalidOperationException("Canonical demo branch did not match its request.");
            }

            var staffService = scope.ServiceProvider.GetRequiredService<StaffProvisioningService>();
            var confirmedFile = Path.Combine(evidence, "confirmed-emails.json");
            var confirmedStaff = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(confirmedFile))!;
            foreach (var staff in DevelopmentFixtureManifest.Staff.Where(staff => staff.BranchId == branch.Id || staff.BranchId == null))
            {
                var staffResult = await staffService.ProvisionStaffAsync(staff, CancellationToken.None);
                if (!staffResult.MatchesRequestedState)
                {
                    throw new InvalidOperationException("Canonical demo staff did not match its request.");
                }

                if (!confirmedStaff.Contains(staff.Email!, StringComparer.OrdinalIgnoreCase))
                {
                    confirmedStaff.Add(staff.Email!);
                }
            }

            File.WriteAllText(confirmedFile, JsonSerializer.Serialize(confirmedStaff));
            if (guestPageFixtures)
            {
                var staff = scope.ServiceProvider.GetRequiredService<StaffProvisioningService>();
                var provisioned = await staff.ProvisionStaffAsync(new StaffProvisioningRequest(
                    Guid.Parse("4ccabff9-76e5-4636-80cb-d2f204241a2b"), "guest.staff@example.invalid",
                    "Password123!", AccountRoles.Administrator, null), CancellationToken.None);

                if (!provisioned.MatchesRequestedState)
                {
                    throw new InvalidOperationException("Guest-page staff fixture did not match its canonical request.");
                }

                var confirmedPath = Path.Combine(evidence, "confirmed-emails.json");
                var confirmed = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(confirmedPath))!;
                if (!confirmed.Contains("guest.staff@example.invalid", StringComparer.OrdinalIgnoreCase))
                {
                    confirmed.Add("guest.staff@example.invalid");
                    File.WriteAllText(confirmedPath, JsonSerializer.Serialize(confirmed));
                }
            }

            var customer = DevelopmentFixtureManifest.Customers[0].Registration;
            var result = await registration.RegisterAsync(customer, CancellationToken.None);

            if (result.Status != RegistrationStatus.Succeeded)
            {
                throw new InvalidOperationException("Canonical demo customer registration failed.");
            }

            var profilePath = Path.Combine(evidence, "profiles.json");
            var profiles = JsonSerializer.Deserialize<List<ExpectedProfile>>(File.ReadAllText(profilePath))!;

            if (!profiles.Any(profile => profile.RegistrationKey == customer.RegistrationKey))
            {
                profiles.Add(new ExpectedProfile(customer.RegistrationKey, customer.LegalName!, customer.Email!, customer.Nric!));
                File.WriteAllText(profilePath, JsonSerializer.Serialize(profiles));
            }
        }

        var revokedTokenPath = Path.Combine(evidence, "revoked-token.txt");

        if (File.Exists(revokedTokenPath))
        {
            using var restartedRequest = new HttpRequestMessage(HttpMethod.Get, "/api/account");
            restartedRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", File.ReadAllText(revokedTokenPath));
            using var restartedResponse = await client.SendAsync(restartedRequest);

            if (restartedResponse.StatusCode != HttpStatusCode.Unauthorized)
            {
                throw new InvalidOperationException("Revoked browser session survived API restart.");
            }

            Console.WriteLine("Restarted real API rejects the revoked desktop session.");
        }
        var start = new ProcessStartInfo("node")
        {
            WorkingDirectory = frontend,
            UseShellExecute = false
        };
        start.ArgumentList.Add("node_modules/@playwright/test/cli.js");
        start.ArgumentList.Add("test");
        start.ArgumentList.Add("--config=playwright.config.ts");
        start.ArgumentList.Add($"--project={project}");

        if (project == "desktop-auth")
        {
            start.ArgumentList.Add("--project=transport-auth");
        }

        foreach (var argument in args)
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment["API_BASE_URL"] = client.BaseAddress!.GetLeftPart(UriPartial.Authority);
        using var portProbe = new TcpListener(IPAddress.Loopback, 0);
        portProbe.Start();
        var frontendPort = ((IPEndPoint)portProbe.LocalEndpoint).Port;
        portProbe.Stop();
        var nextOrigin = $"http://127.0.0.1:{frontendPort}";
        await using var ingress = await TrustedIngress.StartAsync(nextOrigin);
        start.Environment["APP_BASE_URL"] = TrustedIngress.Origin(ingress);
        start.Environment["AUTH_NEXT_ORIGIN"] = nextOrigin;
        start.Environment["TRUSTED_CLIENT_IP_HEADER"] = TrustedIngress.ClientHeader;
        start.Environment["AUTH_EVIDENCE_DIR"] = evidence;
        start.Environment.Remove("NO_COLOR");
        File.WriteAllText(Path.Combine(evidence, "fault.txt"), string.Empty);
        using var browser = Process.Start(start) ?? throw new InvalidOperationException("Browser runner did not start.");
        await browser.WaitForExitAsync();
        return browser.ExitCode;
    }

    private static async Task VerifyPersistenceAsync(RegistrationDatabase database, string evidence)
    {
        var options = new DbContextOptionsBuilder<DealershipDbContext>().UseNpgsql(database.RuntimeConnectionString).Options;
        await using var context = new DealershipDbContext(options);
        var profiles = await context.CustomerProfiles.CountAsync();
        var sessions = await context.AccountSessions.CountAsync();
        var revoked = await context.AccountSessions.CountAsync(session => session.RevokedAt != null);

        var expected = JsonSerializer.Deserialize<ExpectedProfile[]>(File.ReadAllText(Path.Combine(evidence, "profiles.json")))
            ?? throw new InvalidOperationException("Browser profile evidence is missing.");
        var confirmedEmails = JsonSerializer.Deserialize<string[]>(
            File.ReadAllText(Path.Combine(evidence, "confirmed-emails.json")))
            ?? throw new InvalidOperationException("Browser verification evidence is missing.");
        var actualConfirmedEmails = await context.Users.Where(user => user.EmailConfirmed)
            .Select(user => user.Email).ToListAsync();

        if (profiles != expected.Length || expected.Length == 0 || sessions == 0 || revoked == 0 ||
            actualConfirmedEmails.Count != confirmedEmails.Length ||
            actualConfirmedEmails.Any(email => !confirmedEmails.Contains(email, StringComparer.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Browser journeys did not persist the expected accounts, verification and session revocations.");
        }

        foreach (var profile in expected)
        {
            if (!await context.CustomerProfiles.AnyAsync(customer => customer.RegistrationKey == profile.RegistrationKey &&
                customer.LegalName == profile.LegalName && customer.Nric == profile.Nric &&
                context.Users.Any(user => user.Id == customer.UserId && user.Email == profile.Email)))
            {
                throw new InvalidOperationException("Persisted customer identity does not match its confirmed browser receipt.");
            }
        }

        Console.WriteLine("Restricted PostgreSQL read confirms profiles, verification, durable sessions and revocation receipts.");
    }
}

internal sealed record ExpectedProfile(Guid RegistrationKey, string LegalName, string Email, string Nric);

internal sealed class BrowserEvidence(string directory) : IStartupFilter
{
    private readonly ConcurrentDictionary<string, int> _counts = new();
    private readonly object _snapshotLock = new();

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.Use(async (context, continuation) =>
            {
                var path = context.Request.Path.Value ?? string.Empty;
                var fault = File.ReadAllText(Path.Combine(directory, "fault.txt"));

                if (fault == "unavailable")
                {
                    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                    await context.Response.WriteAsJsonAsync(new { title = "Unavailable" });
                    return;
                }

                _counts.AddOrUpdate(path, 1, (_, count) => count + 1);

                lock (_snapshotLock)
                {
                    File.WriteAllText(Path.Combine(directory, "counts.json"), JsonSerializer.Serialize(_counts));
                }

                if (fault == "invalid-login" && path == "/api/auth/login")
                {
                    await context.Response.WriteAsJsonAsync(new { accessToken = "invalid", expiresAt = DateTimeOffset.UtcNow.AddHours(1) });
                    return;
                }

                if (fault == "slow-login" && path == "/api/auth/login")
                {
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync("{");
                    await context.Response.Body.FlushAsync();
                    await Task.Delay(TimeSpan.FromSeconds(15), context.RequestAborted);
                    return;
                }

                await continuation();
            });
            next(app);
        };
    }
}
