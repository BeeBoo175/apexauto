using Dealership.Infrastructure.Photos;
using Dealership.Application;
using Dealership.Application.Accounts;
using Dealership.Application.Intake;
using Dealership.Infrastructure;
using Dealership.Infrastructure.Development;
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
    public static async Task SeedArrivalsAsync(IntakePhotoFixture fixture, bool workshop = false)
    {
        var malaysia = TimeZoneInfo.FindSystemTimeZoneById(DealershipLocale.MalaysiaTimeZoneId);
        var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, malaysia).DateTime);
        var midnight = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue), malaysia));
        // Canonical snapshot slots may fill today after midnight; arrivals use a separate test-only window.
        var schedule = workshop
            ? new InspectionSchedule(new TimeOnly(4, 0), new TimeOnly(6, 0), 10, 30, 0, Enum.GetValues<DayOfWeek>())
            : new InspectionSchedule(new TimeOnly(6, 0), new TimeOnly(9, 0), 90, 30, 0, Enum.GetValues<DayOfWeek>());
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
        var count = workshop ? 33 : 2;
        for (var index = 0; index < count; index++)
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
            var branchIndex = workshop ? index / 11 : 0;
            var slot = workshop ? index % 11 : index;
            var branch = DevelopmentFixtureManifest.Branches[branchIndex];
            var prefix = DevelopmentIntakeManifest.Appointments.First(item => item.BranchId == branch.Id)
                .RegistrationPlate.Split(' ')[0];
            var registration = workshop ? $"{prefix} {9801 + slot}" : $"VCA {9701 + index}";
            var request = new BookingRequest(Guid.NewGuid(), branch.Id,
                schedule.GetSlots(date, midnight)[slot].StartsAt, registration, vehicle, quote, photos);
            var result = await services.GetRequiredService<IntakeBookingService>().BookAsync(owner, request, CancellationToken.None);
            if (result.Status != BookingStatus.Succeeded)
            {
                throw new InvalidOperationException("Same-day browser arrival booking was not confirmed.");
            }
            var staff = DevelopmentFixtureManifest.Staff.Single(item => item.BranchId == branch.Id &&
                item.Role == AccountRoles.SalesAgent);
            var claimed = await services.GetRequiredService<AppointmentAssignmentService>()
                .ExecuteAsync(staff.Id, result.Receipt!.Id, AppointmentAssignmentKind.Claimed,
                    new(Guid.NewGuid(), 0), CancellationToken.None);
            if (claimed.Status != AppointmentAssignmentStatus.Succeeded || claimed.Current?.HandlerId != staff.Id)
            {
                throw new InvalidOperationException("Same-day browser arrival ownership was not confirmed.");
            }

            if (workshop)
            {
                var checkedIn = await services.GetRequiredService<AppointmentCheckInService>()
                    .CheckInAsync(staff.Id, result.Receipt!.Id,
                        new CheckInEvidence($"PL1BT65R84B9{branchIndex}{slot:D4}", 92000, true, true, true, true,
                            "Owner present for workshop intake"), CancellationToken.None);
                if (checkedIn.Status != CheckInStatus.Succeeded)
                {
                    throw new InvalidOperationException("Workshop browser claimable check-in was not confirmed.");
                }
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
        services.AddIntakeApplication();
        services.AddPhotoApplication();
        services.AddInfrastructure(configuration);
        services.AddDevelopmentFixtures(configuration);
        services.AddS3Storage(configuration);
        services.AddPhotos(configuration);
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
