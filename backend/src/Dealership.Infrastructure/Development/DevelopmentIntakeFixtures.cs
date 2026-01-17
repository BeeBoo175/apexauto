using System.Globalization;
using Dealership.Application.Intake;
using Dealership.Application.Vehicles;
using Dealership.Domain.Intake;
using Dealership.Infrastructure.Intake;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Development;

public sealed class DevelopmentIntakeFixtures(DealershipDbContext database, DevelopmentFixturePhotos photos,
    IntakeBookingService bookings, IntakeDiscoveryService discovery, ValuationService valuations,
    InspectionSchedule schedule, PhotoStorageSettings storage, TimeProvider clock)
{
    public void EnsureLocalStorage()
    {
        storage.Validate();

        if (!new Uri(storage.ServiceURL).IsLoopback)
        {
            throw new InvalidOperationException("Development demo uploads require explicitly configured local object storage.");
        }
    }

    public async Task<IReadOnlyList<DevelopmentFixtureReport>> SeedAsync(CancellationToken cancellationToken)
    {
        var reports = new List<DevelopmentFixtureReport>();
        var slots = new Dictionary<Guid, Queue<InspectionSlot>>();
        await database.Database.OpenConnectionAsync(cancellationToken);

        try
        {
            foreach (var fixture in DevelopmentIntakeManifest.Appointments)
            {
                reports.Add(await SeedOneAsync(fixture, slots, cancellationToken));
            }
        }
        finally
        {
            await database.Database.CloseConnectionAsync();
        }

        return reports;
    }

    public async Task<IReadOnlyList<DevelopmentFixtureReport>> ReviewAsync(CancellationToken cancellationToken)
    {
        var ids = DevelopmentIntakeManifest.Appointments.Select(item => item.Id).ToArray();
        var existing = await database.IntakeAppointments.AsNoTracking().Where(item => ids.Contains(item.Id))
            .Select(item => new
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
                return Report(fixture, "missing", true);
            }

            var state = "Booked";

            if (appointment.Cancelled)
            {
                state = "Cancelled";
            }

            if (appointment.CheckedIn)
            {
                state = "CheckedIn";
            }

            return Report(fixture, $"{state}; starts={appointment.StartsAt:O}", false);
        }).ToArray();
    }

    private async Task<DevelopmentFixtureReport> SeedOneAsync(DevelopmentIntakeFixture fixture,
        Dictionary<Guid, Queue<InspectionSlot>> slots, CancellationToken cancellationToken)
    {
        var ownerId = await database.CustomerProfiles.Where(profile => profile.RegistrationKey == fixture.RegistrationKey)
            .Select(profile => (Guid?)profile.UserId).SingleOrDefaultAsync(cancellationToken);
        var exists = await database.IntakeAppointments.AnyAsync(item => item.Id == fixture.Id, cancellationToken);

        if (exists)
        {
            // Reconcile immutable evidence before current eligibility, dates or quotes can change.
            var receipt = await AppointmentReceiptReader.ReadAsync(database, fixture.Id, ownerId ?? Guid.Empty, cancellationToken);
            var matches = receipt is not null && receipt.BranchId == fixture.BranchId &&
                receipt.RegistrationPlate == fixture.RegistrationPlate && receipt.Vehicle == fixture.Vehicle &&
                receipt.Photos.Select(photo => photo.Id).Order().SequenceEqual(PhotoIds(fixture.Id).Order());
            return Report(fixture, matches ? "matches" : "drift; preserved", !matches);
        }

        if (ownerId is null || !await VerifiedCustomerQuery.ForOwner(database, ownerId.Value, lockUser: false)
            .AnyAsync(cancellationToken))
        {
            return Report(fixture, "customer unavailable; preserved", true);
        }

        if (!slots.TryGetValue(fixture.BranchId, out var available))
        {
            available = await FindSlotsAsync(fixture.BranchId, cancellationToken);
            slots.Add(fixture.BranchId, available);
        }

        if (available.Count == 0)
        {
            return Report(fixture, "slot unavailable; preserved", true);
        }

        var uploaded = await photos.UploadAsync(ownerId.Value, fixture.Id, cancellationToken);

        if (uploaded != PhotoStatus.Available)
        {
            return Report(fixture, $"media {uploaded}; preserved", true);
        }

        var quote = valuations.Calculate(fixture.Vehicle).Quote
            ?? throw new InvalidOperationException("Development vehicle must have a canonical valuation.");
        var request = new BookingRequest(fixture.Id, fixture.BranchId, available.Dequeue().StartsAt,
            fixture.RegistrationPlate, fixture.Vehicle, quote, PhotoIds(fixture.Id));
        var result = await bookings.BookAsync(ownerId.Value, request, cancellationToken);
        return Report(fixture, result.Status == BookingStatus.Succeeded ? "created" : $"booking {result.Status}; preserved",
            result.Status != BookingStatus.Succeeded);
    }

    private async Task<Queue<InspectionSlot>> FindSlotsAsync(Guid branchId, CancellationToken cancellationToken)
    {
        var window = schedule.GetDateWindow(clock.GetUtcNow());
        var slots = new Queue<InspectionSlot>();

        for (var date = window.FirstDate; date <= window.LastDate; date = date.AddDays(1))
        {
            var result = await discovery.GetSlotsAsync(branchId, date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                cancellationToken);

            if (result.Status != SlotDiscoveryStatus.Succeeded)
            {
                return slots;
            }

            foreach (var slot in result.Offer!.Slots)
            {
                slots.Enqueue(slot);
            }
        }

        return slots;
    }

    private static Guid[] PhotoIds(Guid appointmentId)
    {
        return Enum.GetValues<PhotoAngle>().Select(angle => DevelopmentIntakeManifest.PhotoId(appointmentId, angle)).ToArray();
    }

    private static DevelopmentFixtureReport Report(DevelopmentIntakeFixture fixture, string state, bool attention)
    {
        return new DevelopmentFixtureReport($"Intake ({fixture.RegistrationPlate})", state, attention);
    }
}
