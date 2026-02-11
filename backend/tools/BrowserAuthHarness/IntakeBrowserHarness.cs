using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Dealership.Infrastructure.Persistence;
using Dealership.IntegrationTests;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.BrowserAuthHarness;

internal static class IntakeBrowserHarness
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length > 0)
        {
            return await RunSelectedAsync(args);
        }

        var specifications = new[]
        {
            "intake-ui-seller.spec.ts"
        };
        foreach (var specification in specifications)
        {
            // Mutating workflows need separate data; each desktop/mobile pair shares its own fixture.
            var result = await RunSelectedAsync([specification]);
            if (result != 0)
            {
                return result;
            }
        }
        return 0;
    }

    private static async Task<int> RunSelectedAsync(string[] args)
    {
        var repository = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../.."));
        var evidence = Path.Combine(repository, ".verifications", "intake-browser", $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(evidence);
        await using var fixture = new IntakePhotoFixture();
        await fixture.InitializeAsync();
        await IntakeBrowserSeed.SeedAsync(fixture);
        await IntakeBrowserSeed.SeedArrivalsAsync(fixture);
        File.WriteAllBytes(Path.Combine(evidence, "upload.jpg"), IntakePhotoFixture.Image());
        Environment.SetEnvironmentVariable("ASPNETCORE_URLS", "http://127.0.0.1:0");
        Environment.SetEnvironmentVariable("AllowedHosts", "localhost;127.0.0.1");

        foreach (var project in new[] { "desktop-auth", "mobile-auth" })
        {
            var directory = Path.Combine(evidence, project, "selected");
            Directory.CreateDirectory(directory);
            File.Copy(Path.Combine(evidence, "upload.jpg"), Path.Combine(directory, "upload.jpg"));
            File.WriteAllText(Path.Combine(directory, "fault.txt"), string.Empty);
            File.WriteAllText(Path.Combine(directory, "assignment.txt"), string.Empty);
            File.WriteAllText(Path.Combine(directory, "counts.json"), "{}");
            var state = new BrowserEvidence(directory);
            using var factory = fixture.CreateFactory().WithWebHostBuilder(builder =>
            {
                builder.UseContentRoot(Path.Combine(repository, "backend", "src", "Dealership.Api"));
                builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ForwardedHeaders:KnownProxies:0"] = "127.0.0.1",
                    ["ForwardedHeaders:KnownProxies:1"] = "::1"
                }));
                builder.ConfigureServices(services =>
                {
                    services.AddSingleton<IStartupFilter>(state);
                    services.AddSingleton<IStartupFilter>(new IntakeAssignmentProbe(directory));
                    services.AddSingleton<IStartupFilter>(new IntakeExportProbe(directory));
                });
            });
            factory.UseKestrel(0);
            using var client = factory.CreateClient();
            using (var references = factory.Services.CreateScope())
            {
                var records = await references.ServiceProvider.GetRequiredService<DealershipDbContext>()
                    .IntakeAppointments.Select(item => new { item.Id, item.BranchId, item.RegistrationPlate, item.StartsAt }).ToArrayAsync();
                File.WriteAllText(Path.Combine(directory, "references.json"), JsonSerializer.Serialize(records, JsonSerializerOptions.Web));
            }
            using var port = new TcpListener(IPAddress.Loopback, 0);
            port.Start();
            var nextOrigin = $"http://127.0.0.1:{((IPEndPoint)port.LocalEndpoint).Port}";
            port.Stop();
            await using var ingress = await TrustedIngress.StartAsync(nextOrigin);
            var start = new ProcessStartInfo("node") { WorkingDirectory = Path.Combine(repository, "frontend"), UseShellExecute = false };
            foreach (var argument in new[] { "node_modules/@playwright/test/cli.js", "test", "--config=playwright.config.ts", $"--project={project}" }.Concat(args))
            {
                start.ArgumentList.Add(argument);
            }
            start.Environment["API_BASE_URL"] = client.BaseAddress!.GetLeftPart(UriPartial.Authority);
            start.Environment["APP_BASE_URL"] = TrustedIngress.Origin(ingress);
            start.Environment["AUTH_NEXT_ORIGIN"] = nextOrigin;
            start.Environment["TRUSTED_CLIENT_IP_HEADER"] = TrustedIngress.ClientHeader;
            start.Environment["AUTH_EVIDENCE_DIR"] = directory;
            start.Environment["AUTH_INTAKE_UI"] = "1";
            start.Environment.Remove("NO_COLOR");
            using var browser = Process.Start(start) ?? throw new InvalidOperationException("Intake browser did not start.");
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(12));
            try
            {
                await browser.WaitForExitAsync(timeout.Token);
            }
            finally
            {
                if (!browser.HasExited)
                {
                    browser.Kill(true);
                }
            }
            if (browser.ExitCode != 0)
            {
                return browser.ExitCode;
            }
            using var scope = factory.Services.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
            var receiptFile = Path.Combine(directory, "check-in.json");
            if (File.Exists(receiptFile))
            {
                var receipt = JsonSerializer.Deserialize<Dealership.Application.Intake.CheckInReceipt>(File.ReadAllText(receiptFile), JsonSerializerOptions.Web)!;
                var saved = await database.AppointmentCheckIns.SingleAsync(item => item.AppointmentId == receipt.AppointmentId);
                var declared = await database.SellerValuations.SingleAsync(item => item.AppointmentId == receipt.AppointmentId);
                if (saved.VehicleId != receipt.VehicleId || saved.StaffId != receipt.StaffId || !saved.Matches(receipt.Evidence) ||
                    declared.MileageKilometres != 90000 || saved.MileageKilometres != 92000 ||
                    await database.AppointmentCheckInMails.CountAsync(item => item.Id == receipt.AppointmentId) != 1)
                {
                    throw new InvalidOperationException("Browser check-in did not match its single persisted receipt and retained declaration.");
                }
            }
            var correctionFile = Path.Combine(directory, "no-show-correction.json");
            if (File.Exists(correctionFile))
            {
                var receipt = JsonSerializer.Deserialize<Dealership.Application.Intake.CheckInReceipt>(
                    File.ReadAllText(correctionFile), JsonSerializerOptions.Web)!;
                var saved = await database.AppointmentCheckIns.AsNoTracking()
                    .SingleAsync(item => item.AppointmentId == receipt.AppointmentId);
                if (receipt.Correction is null || saved.CorrectionReason != receipt.Correction.Reason ||
                    saved.CorrectionRecordedAt != receipt.Correction.RecordedAt || saved.CheckedInAt != receipt.CheckedInAt ||
                    saved.StaffId != receipt.StaffId || saved.VehicleId != receipt.VehicleId || !saved.Matches(receipt.Evidence) ||
                    await database.Vehicles.CountAsync(item => item.IntakeAppointmentId == receipt.AppointmentId) != 1 ||
                    await database.AppointmentCheckInMails.CountAsync(item => item.Id == receipt.AppointmentId) != 1)
                {
                    throw new InvalidOperationException("No-show correction did not match its single immutable check-in bundle.");
                }
            }
            File.WriteAllText(Path.Combine(directory, "persistence.json"), JsonSerializer.Serialize(new
            {
                appointments = await database.IntakeAppointments.CountAsync(),
                photos = await database.IntakePhotos.CountAsync(),
                valuations = await database.SellerValuations.CountAsync(),
                cancellations = await database.AppointmentCancellations.CountAsync(),
                checkIns = await database.AppointmentCheckIns.CountAsync()
            }));
        }
        Console.WriteLine($"Intake browser evidence: {evidence}");
        return 0;
    }

}
