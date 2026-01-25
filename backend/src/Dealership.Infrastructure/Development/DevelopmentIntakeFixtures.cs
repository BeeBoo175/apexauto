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
    IOptions<IntakePhotoSettings> photoSettings, IAmazonS3 objects)
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
            return await ReviewAsync(cancellationToken);
        }

        var timeline = clock.Timeline;
        await using var initialization = await DevelopmentSnapshotInitialization.BeginAsync(database, target, cancellationToken);
        foreach (var moment in timeline)
        {
            await CreateAppointmentAsync(moment, cancellationToken);
        }

        await initialization.CommitAsync(cancellationToken);
        return DevelopmentIntakeManifest.Appointments.Select(fixture => Report(fixture, "created", false)).ToArray();
    }

    public async Task<IReadOnlyList<DevelopmentFixtureReport>> ReviewAsync(CancellationToken cancellationToken)
    {
        var ids = DevelopmentIntakeManifest.Appointments.Select(item => item.Id).ToArray();
        var existing = await database.IntakeAppointments.AsNoTracking().Where(item => ids.Contains(item.Id)).Select(item => new
        {
            item.Id,
            item.StartsAt,
            Cancelled = database.AppointmentCancellations.Any(cancellation => cancellation.AppointmentId == item.Id),
            CheckedIn = database.AppointmentCheckIns.Any(checkIn => checkIn.AppointmentId == item.Id)
        }).ToDictionaryAsync(item => item.Id, cancellationToken);
        return DevelopmentIntakeManifest.Appointments.Select(fixture =>
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

            if (appointment.CheckedIn)
            {
                state = StaffWorklistStatus.CheckedIn;
            }

            return Report(fixture, $"{state}; starts={appointment.StartsAt:O}", state != fixture.Status);
        }).ToArray();
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

        var prepared = await photos.CreateAsync(ownerId, fixture.Id, moment.BookedAt, cancellationToken);
        var candidates = prepared.Select(photo => new BookingPhotoCandidate(photo.Id, ownerId, photo.Angle,
            photo.AvailableAt, photo.ExpiresAt, false, false)).ToArray();
        if (!BookingPhotoSet.TryCreate(ownerId, candidates, moment.BookedAt, out var selected))
        {
            throw new InvalidOperationException("Snapshot booking requires four canonical eligible photos.");
        }

        var slot = schedule.FindSlot(moment.Slot.StartsAt, moment.BookedAt)
            ?? throw new InvalidOperationException("Snapshot booking must obey the configured schedule.");
        var appointment = new IntakeAppointment(fixture.Id, ownerId, fixture.BranchId, fixture.RegistrationPlate,
            slot, moment.BookedAt);
        var quote = new ValuationService(new SnapshotClock(moment.BookedAt)).Calculate(fixture.Vehicle).Quote
            ?? throw new InvalidOperationException("Snapshot vehicle must have a canonical valuation at booking time.");
        var vehicle = fixture.Vehicle;
        var valuation = new SellerValuation(fixture.Id, ownerId, new ValuedVehicle(vehicle.Make!, vehicle.Model!,
            vehicle.Trim!, vehicle.ManufactureYear!.Value, vehicle.MileageKilometres!.Value,
            quote.MinValuation, quote.MaxValuation, quote.Currency, quote.ValuationYear));
        database.IntakePhotos.AddRange(prepared);
        database.IntakeAppointments.Add(appointment);
        database.SellerValuations.Add(valuation);
        database.AppointmentPhotos.AddRange(selected!.Selections.Select(photo => new AppointmentPhoto(fixture.Id, ownerId, photo)));
        database.AppointmentConfirmations.Add(new AppointmentConfirmation
        {
            Id = fixture.Id,
            RecipientEmail = owner.Email!,
            NextDeliveryAt = moment.BookedAt
        });
        await database.SaveChangesAsync(cancellationToken);
        AddTransition(moment, appointment, valuation, owner.Email!);
        await database.SaveChangesAsync(cancellationToken);
        database.ChangeTracker.Clear();
    }

    private void AddTransition(DevelopmentIntakeMoment moment, IntakeAppointment appointment,
        SellerValuation valuation, string email)
    {
        if (moment.Fixture.Status == StaffWorklistStatus.Cancelled)
        {
            database.AppointmentCancellations.Add(new AppointmentCancellation(appointment, moment.TransitionAt!.Value,
                TimeSpan.FromDays(photoSettings.Value.CancelledRetentionDays)));
            database.AppointmentCancellationMails.Add(new AppointmentCancellationMail
            {
                Id = appointment.Id,
                RecipientEmail = email,
                NextDeliveryAt = moment.TransitionAt.Value
            });
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
        database.Vehicles.Add(new Vehicle(appointment, valuation, checkIn));
        database.AppointmentCheckIns.Add(checkIn);
        database.AppointmentCheckInMails.Add(new AppointmentCheckInMail
        {
            Id = appointment.Id,
            RecipientEmail = email,
            NextDeliveryAt = checkIn.CheckedInAt
        });
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
