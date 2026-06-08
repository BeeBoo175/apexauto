using Dealership.Application.Accounts;
using Dealership.Application.Branches;
using Dealership.Application.Intake;
using Dealership.Application.Vehicles;
using Dealership.Domain.Vehicles;
using Dealership.Domain.Common;
using Dealership.Domain.Intake;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Intake;
using Dealership.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dealership.IntegrationTests;

internal sealed class CheckInTestData : IDisposable
{
    public static CheckInEvidence Evidence { get; } = new("PL1BT65R84B123456", 92000, true, true, true, true, "Owner present");
    private readonly DateOnly _date;
    private readonly TimeProvider _clock;

    private CheckInTestData(IntakePhotoFixture fixture, AppointmentSchedule? schedule, double? unsubmittedLifetimeHours)
    {
        _date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, DealershipLocale.Malaysia).DateTime);
        _clock = new FixedClock(TimeZoneInfo.ConvertTimeToUtc(_date.ToDateTime(TimeOnly.MinValue), DealershipLocale.Malaysia));
        Factory = fixture.CreateFactory(clock: _clock, unsubmittedLifetimeHours: unsubmittedLifetimeHours)
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            // Canonical bookings need a deterministic same-day slot on any test weekday, even after closing.
            services.RemoveAll<AppointmentSchedule>();
            services.AddSingleton(schedule ?? new AppointmentSchedule(new TimeOnly(9, 0), new TimeOnly(18, 0),
                90, 30, 0, Enum.GetValues<DayOfWeek>()));
        }));
    }

    public WebApplicationFactory<Program> Factory { get; }
    public Guid OwnerId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid StaffId { get; private set; }
    public string CustomerEmail { get; private set; } = string.Empty;
    public string StaffEmail { get; private set; } = string.Empty;

    public static async Task<CheckInTestData> CreateAsync(IntakePhotoFixture fixture, AppointmentSchedule? schedule = null,
        double? unsubmittedLifetimeHours = null)
    {
        var data = new CheckInTestData(fixture, schedule, unsubmittedLifetimeHours);

        try
        {
            await data.ProvisionAsync();
            return data;
        }
        catch
        {
            data.Dispose();
            throw;
        }
    }

    private async Task ProvisionAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        CustomerEmail = $"nur.aina.{Guid.NewGuid():N}@example.invalid";
        var registration = new RegistrationRequest(Guid.NewGuid(), "Nur Aina Abdullah", CustomerEmail,
            "Password123!", $"940215{Random.Shared.Next(100000, 1000000)}", "0123456789",
            "18 Jalan Melati, Petaling Jaya, Selangor");
        Assert.Equal(RegistrationStatus.Succeeded,
            (await services.GetRequiredService<RegistrationService>().RegisterAsync(registration, CancellationToken.None)).Status);
        var database = services.GetRequiredService<DealershipDbContext>();
        OwnerId = await database.Users.Where(user => user.Email == CustomerEmail).Select(user => user.Id).SingleAsync();
        var challenge = await database.EmailVerificationChallenges.SingleAsync(item => item.UserId == OwnerId);
        var token = services.GetRequiredService<EmailVerificationToken>().Create(challenge);
        Assert.Equal(VerificationStatus.Accepted,
            (await services.GetRequiredService<EmailVerificationService>().ConfirmAsync(token, CancellationToken.None)).Status);
        BranchId = Guid.NewGuid();
        await services.GetRequiredService<BranchProvisioningService>().ProvisionBranchAsync(
            DevelopmentFixtureManifest.Branches[0] with { Id = BranchId }, CancellationToken.None);
        StaffId = Guid.NewGuid();
        StaffEmail = $"aina.sofia.{Guid.NewGuid():N}@example.invalid";
        await services.GetRequiredService<StaffProvisioningService>().ProvisionStaffAsync(new StaffProvisioningRequest(
            StaffId, StaffEmail, "Password123!", AccountRoles.SalesAgent, BranchId),
            CancellationToken.None);
    }

    public async Task<AppointmentReceipt> BookAsync(int slot = 0, string plate = "WYY 8821", int dateOffset = 0,
        Guid? branchId = null)
    {
        using var scope = Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        // Reuse this scenario scope's socket while canonical photo services retain their own transaction boundaries.
        await services.GetRequiredService<DealershipDbContext>().Database.OpenConnectionAsync();
        var photoIds = new List<Guid>();

        foreach (var angle in Enum.GetValues<PhotoAngle>())
        {
            var id = Guid.NewGuid();
            using var image = new MemoryStream(IntakePhotoFixture.Image());
            var uploaded = await services.GetRequiredService<IntakePhotoService>()
                .UploadAsync(OwnerId, id, angle, "image/jpeg", image, CancellationToken.None);
            Assert.Equal(PhotoStatus.Available, uploaded.Status);
            photoIds.Add(id);
        }

        var make = VehicleTaxonomy.Makes[0];
        var model = make.Models[0];
        var vehicle = new ValuationRequest(make.Name, model.Name, model.Trims[0].Name, 2020, 90000);
        var quote = services.GetRequiredService<ValuationService>().Calculate(vehicle).Quote!;
        var startsAt = services.GetRequiredService<AppointmentSchedule>()
            .GetSlots(_date.AddDays(dateOffset), _clock.GetUtcNow())[slot].StartsAt;
        var request = new BookingRequest(Guid.NewGuid(), branchId ?? BranchId, startsAt, plate, vehicle, quote, photoIds);
        var result = await services.GetRequiredService<IntakeBookingService>().BookAsync(OwnerId, request, CancellationToken.None);
        Assert.Equal(BookingStatus.Succeeded, result.Status);
        return result.Receipt!;
    }

    public async Task ClaimAsync(Guid appointmentId, Guid? staffId = null)
    {
        using var scope = Factory.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<AppointmentAssignmentService>()
            .ExecuteAsync(staffId ?? StaffId, appointmentId, AppointmentAssignmentKind.Claimed,
                new(Guid.NewGuid(), 0, null, null), CancellationToken.None);
        Assert.Equal(AppointmentAssignmentStatus.Succeeded, result.Status);
        Assert.Equal(staffId ?? StaffId, result.Current!.HandlerId);
    }

    public async Task<AppointmentCheckIn> SaveBundleAsync(Guid appointmentId, CheckInEvidence? evidence = null)
    {
        await ClaimAsync(appointmentId);
        using var scope = Factory.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<AppointmentCheckInService>()
            .CheckInAsync(StaffId, appointmentId, evidence ?? Evidence, CancellationToken.None);
        Assert.Equal(CheckInStatus.Succeeded, result.Status);
        var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        return await database.AppointmentCheckIns.AsNoTracking().SingleAsync(item => item.AppointmentId == appointmentId);
    }

    public void Dispose()
    {
        Factory.Dispose();
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
