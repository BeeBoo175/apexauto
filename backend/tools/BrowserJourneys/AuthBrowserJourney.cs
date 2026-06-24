using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Dealership.Infrastructure.Persistence;
using Dealership.IntegrationTests;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using Dealership.Application.Accounts;
using Dealership.Infrastructure.Development;

namespace Dealership.BrowserJourneys;

internal static class AuthBrowserJourney
{
    public static async Task<int> RunAsync(string specification, string[] projects)
    {
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
        await database.InitializeAsync();
        Environment.SetEnvironmentVariable("ASPNETCORE_URLS", "http://127.0.0.1:0");
        Environment.SetEnvironmentVariable("AllowedHosts", "localhost;127.0.0.1");

        try
        {
            foreach (var project in projects)
            {
                var exitCode = await RunBrowserAsync(project, specification, database, state, evidence, frontend);

                if (exitCode != 0)
                {
                    return exitCode;
                }
            }

            await VerifyPersistenceAsync(database, evidence, specification != "auth-transport.spec.ts");
            return 0;
        }
        finally
        {
            await database.DisposeAsync();
            File.Delete(Path.Combine(evidence, "fault.txt"));
            File.Delete(Path.Combine(evidence, "revoked-token.txt"));
        }
    }

    private static async Task<int> RunBrowserAsync(string project, string specification, RegistrationDatabase database,
        BrowserEvidence state, string evidence, string frontend)
    {
        var nextOrigin = BrowserProcess.AllocateNextOrigin();
        await using var ingress = await TrustedIngress.StartAsync(nextOrigin);
        var browserOrigin = TrustedIngress.Origin(ingress);
        var verificationMail = specification == "browser-auth-email-verification.spec.ts";
        if (verificationMail)
        {
            RecoveryBrowserJourney.ValidateInbox();
        }
        using var factory = new WebApplicationFactory<global::Program>().WithWebHostBuilder(builder =>
        {
            builder.UseContentRoot(Path.Combine(Path.GetDirectoryName(frontend)!, "backend", "src", "Dealership.Api"));
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ForwardedHeaders:KnownProxies:0"] = "127.0.0.1",
                    ["ForwardedHeaders:KnownProxies:1"] = "::1",
                    ["APP_BASE_URL"] = browserOrigin,
                    ["Email:Mode"] = verificationMail ? "Loopback" : "Disabled",
                    ["Email:SmtpHost"] = "127.0.0.1",
                    ["Email:SmtpPort"] = "1025",
                    ["Email:SenderEmail"] = "no-reply@apexauto.invalid",
                    ["Email:EnableSsl"] = "false"
                }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<DealershipDbContext>>();
                services.RemoveAll<DealershipDbContext>();
                services.AddDbContext<DealershipDbContext>(options => options.UseNpgsql(database.RuntimeConnectionString));
                services.AddSingleton<IStartupFilter>(state);
                if (verificationMail)
                {
                    services.RemoveAll<Dealership.Infrastructure.Mail.IMailTransport>();
                    services.AddSingleton<Dealership.Infrastructure.Mail.IMailTransport, RecoveryLoopbackMailTransport>();
                }
            });
        });
        factory.UseKestrel(0);
        using var client = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
        {
            var registration = scope.ServiceProvider.GetRequiredService<RegistrationService>();

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
        File.WriteAllText(Path.Combine(evidence, "fault.txt"), string.Empty);
        var selectedProject = specification == "auth-transport.spec.ts" ? "transport" : project;
        return await BrowserProcess.RunAsync(frontend, selectedProject, [specification], evidence,
            client.BaseAddress!, browserOrigin, nextOrigin);
    }

    private static async Task VerifyPersistenceAsync(RegistrationDatabase database, string evidence, bool requireSessionReceipts)
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

        if (profiles != expected.Length || expected.Length == 0 || (requireSessionReceipts && (sessions == 0 || revoked == 0)) ||
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
