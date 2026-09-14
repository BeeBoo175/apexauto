using Dealership.Infrastructure.Intake;
using System.Text.Json;
using Dealership.Infrastructure.Persistence;
using Dealership.IntegrationTests;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.BrowserJourneys;

internal static class IntakeBrowserJourney
{
    public static async Task<int> RunAsync(string specification, string[] projects)
    {
        var repository = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../.."));
        var evidence = Path.Combine(repository, ".verifications", "intake-browser", $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(evidence);
        await using var fixture = new IntakePhotoFixture();
        await fixture.InitializeAsync();
        await IntakeBrowserSeed.SeedAsync(fixture);
        if (specification == "intake-ui-staff-erasure.spec.ts")
        {
            await IntakeBrowserSeed.SeedSpareEmployeeAsync(fixture);
        }

        if (!specification.StartsWith("vehicles-ui", StringComparison.Ordinal))
        {
            await IntakeBrowserSeed.SeedArrivalsAsync(fixture, specification == "inspection-ui-lifecycle.spec.ts");
        }
        File.WriteAllBytes(Path.Combine(evidence, "upload.jpg"), IntakePhotoFixture.Image());
        Environment.SetEnvironmentVariable("ASPNETCORE_URLS", "http://127.0.0.1:0");
        Environment.SetEnvironmentVariable("AllowedHosts", "localhost;127.0.0.1");

        foreach (var project in projects)
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
                    ["ForwardedHeaders:KnownProxies:1"] = "::1",
                    ["RateLimiting:ApiRequestsPerWindow"] = "10000",
                    ["RateLimiting:BearerRequestsPerWindow"] = "10000",
                    ["RateLimiting:UserReadBurst"] = "10000",
                    ["RateLimiting:UserWritesPerWindow"] = "10000",
                    ["RateLimiting:PictureReadsPerWindow"] = "10000",
                    ["RateLimiting:LoginAttemptsPerWindow"] = "100",
                    ["RateLimiting:VerificationAttemptsPerWindow"] = "100"
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
            var nextOrigin = BrowserProcess.AllocateNextOrigin();
            await using var ingress = await TrustedIngress.StartAsync(nextOrigin);
            var exitCode = await BrowserProcess.RunAsync(Path.Combine(repository, "frontend"), project,
                [specification], directory, client.BaseAddress!, TrustedIngress.Origin(ingress), nextOrigin,
                new Dictionary<string, string> { ["BROWSER_INTAKE"] = "1" });
            if (exitCode != 0)
            {
                return exitCode;
            }
            using var scope = factory.Services.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
            var receiptFile = Path.Combine(directory, "check-in.json");
            if (File.Exists(receiptFile))
            {
                var receipt = JsonSerializer.Deserialize<Dealership.Application.Intake.CheckInReceipt>(File.ReadAllText(receiptFile), JsonSerializerOptions.Web)!;
                var saved = await database.AppointmentCheckIns.SingleAsync(item => item.AppointmentId == receipt.AppointmentId);
                var declared = (await database.IntakeAppointments.SingleAsync(item => item.Id == receipt.AppointmentId)).Valuation;
                if (saved.VehicleId != receipt.VehicleId || saved.StaffId != receipt.StaffId || !saved.Matches(receipt.Evidence) ||
                    declared.MileageKilometres != 90000 || saved.MileageKilometres != 92000 ||
                    await database.AppointmentMailOutbox.CountAsync(item => item.Kind == AppointmentMailKind.CheckIn && item.AppointmentId == receipt.AppointmentId) != 1)
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
                    await database.AppointmentMailOutbox.CountAsync(item => item.Kind == AppointmentMailKind.CheckIn && item.AppointmentId == receipt.AppointmentId) != 1)
                {
                    throw new InvalidOperationException("No-show correction did not match its single immutable check-in bundle.");
                }
            }
            File.WriteAllText(Path.Combine(directory, "persistence.json"), JsonSerializer.Serialize(new
            {
                appointments = await database.IntakeAppointments.CountAsync(),
                photos = await database.IntakePhotos.CountAsync(),

                cancellations = await database.IntakeAppointments.CountAsync(item => item.CancelledAt != null),
                checkIns = await database.AppointmentCheckIns.CountAsync()
            }));
        }
        Console.WriteLine($"Intake browser evidence: {evidence}");
        return 0;
    }

}
