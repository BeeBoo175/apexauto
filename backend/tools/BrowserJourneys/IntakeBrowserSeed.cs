using Dealership.Application;
using Dealership.Application.Intake;
using Dealership.Infrastructure;
using Dealership.Infrastructure.Configuration;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Intake;
using Dealership.Infrastructure.Storage;
using Dealership.IntegrationTests;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Microsoft.AspNetCore.Hosting;
using Dealership.Application.Vehicles;
using Dealership.Domain.Intake;
using Dealership.Domain.Common;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dealership.BrowserJourneys;

internal static class IntakeBrowserSeed
{
    public static async Task SeedArrivalsAsync(IntakePhotoFixture fixture)
    {
        var malaysia = TimeZoneInfo.FindSystemTimeZoneById(DealershipLocale.MalaysiaTimeZoneId);
        var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, malaysia).DateTime);
        var midnight = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue), malaysia));
        // Canonical snapshot slots may fill today after midnight; arrivals use a separate test-only window.
        var schedule = new InspectionSchedule(new TimeOnly(6, 0), new TimeOnly(9, 0), 90, 30, 0, Enum.GetValues<DayOfWeek>());
        using var factory = fixture.CreateFactory(clock: new ArrivalClock(midnight)).WithWebHostBuilder(builder =>
        {
            builder.UseContentRoot(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../backend/src/Dealership.Api")));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<InspectionSchedule>();
                services.AddSingleton(schedule);
            });
        });
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var database = services.GetRequiredService<DealershipDbContext>();
        var email = DevelopmentFixtureManifest.Customers[1].Registration.Email;
        var owner = await database.Users.Where(user => user.Email == email).Select(user => user.Id).SingleAsync();
        for (var index = 0; index < 2; index++)
        {
            var photos = new List<Guid>();
            foreach (var angle in Enum.GetValues<PhotoAngle>())
            {
                var id = Guid.NewGuid();
                using var image = new MemoryStream(IntakePhotoFixture.Image());
                var upload = await services.GetRequiredService<IntakePhotoService>()
                    .UploadAsync(owner, id, angle, "image/jpeg", image, CancellationToken.None);
                if (upload.Status != PhotoStatus.Available)
                {
                    throw new InvalidOperationException("Same-day browser arrival photo was not confirmed.");
                }
                photos.Add(id);
            }
            var vehicle = new ValuationRequest("Proton", "Saga", "Standard", 2020, 90000);
            var quote = services.GetRequiredService<ValuationService>().Calculate(vehicle).Quote!;
            var request = new BookingRequest(Guid.NewGuid(), DevelopmentFixtureManifest.Branches[0].Id,
                schedule.GetSlots(date, midnight)[index].StartsAt, $"VCA {9701 + index}", vehicle, quote, photos);
            var result = await services.GetRequiredService<IntakeBookingService>().BookAsync(owner, request, CancellationToken.None);
            if (result.Status != BookingStatus.Succeeded)
            {
                throw new InvalidOperationException("Same-day browser arrival booking was not confirmed.");
            }
        }
    }

    public static async Task SeedAsync(IntakePhotoFixture fixture)
    {
        var connection = fixture.Database.ConnectionString;
        var target = DevelopmentFixtureTarget.Validate("Development", new NpgsqlConnectionStringBuilder(connection).Database, connection);
        var configuration = new ConfigurationBuilder().AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"))
            .AddEnvironmentVariables().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connection,
                ["AWS:BucketName"] = fixture.BucketName,
                ["IntakePhotos:CleanupEnabled"] = "false"
            }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration);
        services.AddSingleton(InspectionScheduleConfiguration.Read(configuration));
        services.AddSingleton<DevelopmentFixtureClock>();
        services.AddSingleton<TimeProvider>(provider => provider.GetRequiredService<DevelopmentFixtureClock>());
        services.AddScoped<IntakeDiscoveryService>();
        services.AddScoped<IntakeBookingService>();
        services.AddS3Storage(configuration);
        services.AddIntakeMedia(configuration);
        services.AddScoped<DevelopmentFixtureRunner>();
        services.AddScoped<DevelopmentInspectionFixtures>();
        services.AddScoped<DevelopmentCustomerFixtures>();
        services.AddScoped<DevelopmentIntakeFixtures>();
        services.AddScoped<DevelopmentFixturePhotos>();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        await using var scope = provider.CreateAsyncScope();
        var reports = await scope.ServiceProvider.GetRequiredService<DevelopmentFixtureRunner>().SeedAsync(target, CancellationToken.None);
        if (reports.Any(report => report.RequiresAttention))
        {
            throw new InvalidOperationException("Canonical browser fixture initialization reported drift.");
        }
    }

    private sealed class ArrivalClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
