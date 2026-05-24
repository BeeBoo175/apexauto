using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Dealership.Application.Accounts;
using Dealership.Application.Intake;
using Dealership.Application.Vehicles;
using Dealership.Domain.Intake;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Development.Maintenance;
using Dealership.Infrastructure.Intake;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dealership.Infrastructure.Development;

public sealed class DevelopmentIntakeFixtures(DealershipDbContext database, DevelopmentFixturePhotos photos,
    InspectionSchedule schedule, PhotoStorageSettings storage, DevelopmentFixtureClock clock,
    IOptions<IntakePhotoSettings> photoSettings, IAmazonS3 objects, DevelopmentIntakeOwnershipFixtures ownership)
{
    public async Task EnsureLocalStorageAsync(CancellationToken cancellationToken)
    {
        storage.Validate();

        if (!new Uri(storage.ServiceURL).IsLoopback)
        {
            throw new InvalidOperationException("Development demo uploads require explicitly configured local object storage.");
        }

        try
        {
            await objects.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = storage.BucketName,
                MaxKeys = 1
            }, cancellationToken);
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound &&
            exception.ErrorCode == "NoSuchBucket")
        {
            await objects.PutBucketAsync(new PutBucketRequest
            {
                BucketName = storage.BucketName,
                UseClientRegion = true
            }, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<DevelopmentFixtureReport>> SeedAsync(DevelopmentFixtureTarget target,
        CancellationToken cancellationToken)
    {
        if (await database.IntakeAppointments.AnyAsync(cancellationToken))
        {
            return [.. await ReviewAppointmentsAsync(cancellationToken),
                .. await ownership.RunAsync(true, cancellationToken)];
        }

        var timeline = clock.Timeline;
        await using (var initialization = await DevelopmentSnapshotInitialization.BeginAsync(database, target, cancellationToken))
        {
            foreach (var moment in timeline)
            {
                await CreateAppointmentAsync(moment, cancellationToken);
            }

            await initialization.CommitAsync(cancellationToken);
        }

        return [.. DevelopmentIntakeManifest.Appointments.Select(fixture => Report(fixture, "created", false)),
            .. await ownership.RunAsync(true, cancellationToken)];
    }

    public async Task<IReadOnlyList<DevelopmentFixtureReport>> ReviewAsync(CancellationToken cancellationToken)
    {
        return [.. await ReviewAppointmentsAsync(cancellationToken),
            .. await ownership.RunAsync(false, cancellationToken)];
    }

    private async Task<IReadOnlyList<DevelopmentFixtureReport>> ReviewAppointmentsAsync(CancellationToken cancellationToken)
    {
        var now = await database.DatabaseNowAsync(cancellationToken);
        var ids = DevelopmentIntakeManifest.Appointments.Select(item => item.Id).ToArray();
        var existing = await database.IntakeAppointments.AsNoTracking().Where(item => ids.Contains(item.Id)).Select(item => new
        {
            item.Id,
            item.StartsAt,
            Cancelled = item.CancelledAt != null,
            CheckedIn = database.AppointmentCheckIns.Any(checkIn => checkIn.AppointmentId == item.Id)
        }).ToDictionaryAsync(item => item.Id, cancellationToken);
        return [.. DevelopmentIntakeManifest.Appointments.Select(fixture =>
        {
            if (!existing.TryGetValue(fixture.Id, out var appointment))
            {
                return Report(fixture, "missing; clear then seed to rebuild", true);
            }

            var state = StaffWorklistStatus.Booked;
            if (appointment.Cancelled)
            {
                state = StaffWorklistStatus.Cancelled;
            }

            if (!appointment.Cancelled && !appointment.CheckedIn && now >= AppointmentDay.EndsAt(appointment.StartsAt))
            {
                state = StaffWorklistStatus.NoShow;
            }

            if (appointment.CheckedIn)
            {
                state = StaffWorklistStatus.CheckedIn;
            }

            return Report(fixture, $"{state}; starts={appointment.StartsAt:O}", state != fixture.Status);
        })];
    }

    private async Task CreateAppointmentAsync(DevelopmentIntakeMoment moment, CancellationToken cancellationToken)
    {
        var fixture = moment.Fixture;
        var ownerId = await database.CustomerProfiles.Where(profile => profile.RegistrationKey == fixture.RegistrationKey)
            .Select(profile => profile.UserId).SingleAsync(cancellationToken);
        var owner = await VerifiedCustomerQuery.ForOwner(database, ownerId, lockUser: true)
            .SingleAsync(cancellationToken);
        if (!await database.Branches.AnyAsync(branch => branch.Id == fixture.BranchId && branch.IsActive, cancellationToken))
        {
            throw new InvalidOperationException("Snapshot appointments require an active branch.");
        }

        var details = DevelopmentVehicleCatalogue.For(fixture.Vehicle.Make!, fixture.Vehicle.Model!, fixture.Vehicle.Trim);
        var prepared = await photos.CreateAsync(ownerId, fixture.Id, moment.BookedAt, cancellationToken, details);
        var candidates = prepared.Select(photo => new BookingPhotoCandidate(photo.Id, ownerId, photo.Angle,
            photo.AvailableAt, photo.ExpiresAt, false, false)).ToArray();
        if (!BookingPhotoSet.TryCreate(ownerId, candidates, moment.BookedAt, out var selected))
        {
            throw new InvalidOperationException("Snapshot booking requires four canonical eligible photos.");
        }

        var slot = schedule.FindSlot(moment.Slot.StartsAt, moment.BookedAt)
            ?? throw new InvalidOperationException("Snapshot booking must obey the configured schedule.");
        var quote = new ValuationService(new SnapshotClock(moment.BookedAt)).Calculate(fixture.Vehicle).Quote
            ?? throw new InvalidOperationException("Snapshot vehicle must have a canonical valuation at booking time.");
        var vehicle = fixture.Vehicle;
        var valuation = new SellerValuation(vehicle.Make!, vehicle.Model!,
            vehicle.Trim!, vehicle.ManufactureYear!.Value, vehicle.MileageKilometres!.Value,
            quote.MinValuation, quote.MaxValuation, quote.Currency, quote.ValuationYear);
        var appointment = new IntakeAppointment(fixture.Id, ownerId, fixture.BranchId, fixture.RegistrationPlate,
            slot, moment.BookedAt, valuation);
        database.IntakePhotos.AddRange(prepared);
        database.IntakeAppointments.Add(appointment);
        database.AppointmentPhotos.AddRange(selected!.Selections.Select(photo => new AppointmentPhoto(fixture.Id, ownerId, photo)));
        database.AppointmentMailOutbox.Add(new AppointmentMail(fixture.Id, AppointmentMailKind.Confirmation,
            owner.Email!, moment.BookedAt));
        await database.SaveChangesAsync(cancellationToken);
        AddTransition(moment, appointment, owner.Email!);
        await database.SaveChangesAsync(cancellationToken);
        database.ChangeTracker.Clear();
    }

    private void AddTransition(DevelopmentIntakeMoment moment, IntakeAppointment appointment, string email)
    {
        if (moment.Fixture.Status == StaffWorklistStatus.Cancelled)
        {
            appointment.Cancel(moment.TransitionAt!.Value,
                TimeSpan.FromDays(photoSettings.Value.CancelledRetentionDays));
            database.AppointmentMailOutbox.Add(new AppointmentMail(appointment.Id, AppointmentMailKind.Cancellation,
                email, moment.TransitionAt.Value));
            return;
        }

        if (moment.Fixture.Status != StaffWorklistStatus.CheckedIn)
        {
            return;
        }

        var staffId = DevelopmentFixtureManifest.Staff.Single(item =>
            item.BranchId == appointment.BranchId && item.Role == AccountRoles.SalesAgent).Id;
        var checkIn = new AppointmentCheckIn(Guid.NewGuid(), appointment, staffId,
            moment.Fixture.CheckIn!, moment.TransitionAt!.Value);
        database.Vehicles.Add(new Vehicle(appointment, checkIn));
        database.AppointmentCheckIns.Add(checkIn);
        database.AppointmentMailOutbox.Add(new AppointmentMail(appointment.Id, AppointmentMailKind.CheckIn,
            email, checkIn.CheckedInAt));
    }

    private static DevelopmentFixtureReport Report(DevelopmentIntakeFixture fixture, string state, bool attention)
    {
        return new DevelopmentFixtureReport($"Intake ({fixture.RegistrationPlate})", state, attention);
    }

    private sealed class SnapshotClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
