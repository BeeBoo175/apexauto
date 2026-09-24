using Dealership.Application.Accounts;
using Dealership.Application.TestDrives;
using Dealership.Domain.Common;
using Dealership.Domain.Intake;
using Dealership.Domain.TestDrives;
using Dealership.Infrastructure.Intake;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Development;

public sealed class DevelopmentTestDriveVolumeFixtures(DealershipDbContext database,
    DevelopmentCustomerFixtures customers, TestDriveBookingService bookings,
    TestDriveAssignmentService assignments, TestDriveCancellationService cancellations, AppointmentSchedule schedule)
{
    public async Task<IReadOnlyList<DevelopmentFixtureReport>> RunAsync(bool seed, CancellationToken cancellationToken)
    {
        var reports = new List<DevelopmentFixtureReport>();
        foreach (var branch in DevelopmentFixtureManifest.Branches)
        {
            var scenarios = DevelopmentTestDriveVolumeManifest.All.Where(item => item.BranchId == branch.Id).ToArray();
            foreach (var customer in scenarios.Select(item => item.Customer).DistinctBy(item => item.Registration.RegistrationKey))
            {
                reports.Add(seed
                    ? await customers.SeedAsync(customer, cancellationToken)
                    : await customers.ReviewAsync(customer, cancellationToken));
            }

            if (reports.Any(item => item.RequiresAttention))
            {
                continue;
            }

            var listing = DevelopmentInspectionScenarios.All.Where(item => item.PublishListing &&
                item.Intake.BranchId == branch.Id && DevelopmentIntakeManifest.CatalogueExamples.Contains(item.Intake))
                .ElementAt(1);
            var vehicleId = await database.Vehicles.Where(item => item.IntakeAppointmentId == listing.Id)
                .Select(item => (Guid?)item.Id).SingleOrDefaultAsync(cancellationToken);
            if (vehicleId is null)
            {
                reports.Add(new($"Test-drive volume ({branch.Name})", "listed vehicle missing; preserved", true));
                continue;
            }

            foreach (var scenario in scenarios)
            {
                reports.Add(await RunOneAsync(seed, scenario, vehicleId.Value,
                    listing.OperationId("listing-publication"), cancellationToken));
            }
        }

        return reports;
    }

    private async Task<DevelopmentFixtureReport> RunOneAsync(bool seed, DevelopmentTestDriveVolumeScenario scenario,
        Guid vehicleId, Guid publicationId, CancellationToken cancellationToken)
    {
        var label = $"Test-drive {scenario.Queue} ({scenario.Customer.Registration.Email}; {scenario.Index + 1})";
        var customerId = await database.CustomerProfiles.Where(item =>
            item.RegistrationKey == scenario.Customer.Registration.RegistrationKey)
            .Select(item => item.UserId).SingleAsync(cancellationToken);
        var handlerId = scenario.HandlerId;
        var bookingId = scenario.OperationId("booking");
        var booking = await database.TestDriveBookings.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == bookingId, cancellationToken);
        if (booking is null && seed)
        {
            var slot = await FindSlotAsync(scenario, handlerId, cancellationToken);
            if (slot is null)
            {
                return new(label, "schedule unavailable; preserved", true);
            }

            var booked = await bookings.BookAsync(customerId, vehicleId,
                new(bookingId, publicationId, slot.StartsAt), cancellationToken);
            booking = booked.Booking;
            if (booking is null)
            {
                return new(label, $"booking {booked.Status} at {slot.StartsAt:O}; preserved", true);
            }
        }

        if (booking is null || booking.CustomerId != customerId || booking.VehicleId != vehicleId ||
            booking.PublicationId != publicationId || booking.BranchId != scenario.BranchId)
        {
            return new(label, "booking missing/drift; preserved", true);
        }

        var ownership = await database.TestDriveAssignments.AsNoTracking().Where(item => item.BookingId == bookingId)
            .OrderByDescending(item => item.Version).FirstOrDefaultAsync(cancellationToken);
        if (ownership is null && scenario.Queue != "ToClaim" && seed)
        {
            await assignments.ExecuteAsync(handlerId, bookingId, WorkOwnershipKind.Claimed,
                new(scenario.OperationId("claim"), 0), cancellationToken);
            ownership = await database.TestDriveAssignments.AsNoTracking().Where(item => item.BookingId == bookingId)
                .OrderByDescending(item => item.Version).FirstOrDefaultAsync(cancellationToken);
        }

        var expectedHandler = scenario.Queue == "ToClaim" ? (Guid?)null : handlerId;
        var matches = ownership?.HandlerId == expectedHandler && (ownership is null ||
            ownership.Id == scenario.OperationId("claim") && ownership.Version == 1 && ownership.ActorId == handlerId &&
            ownership.Kind == WorkOwnershipKind.Claimed);
        if (!matches)
        {
            return new(label, "ownership missing/drift; preserved", true);
        }

        var cancellation = await database.TestDriveCancellations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.BookingId == bookingId, cancellationToken);
        if (cancellation is null && scenario.Queue == "History" && seed)
        {
            cancellation = (await cancellations.CancelAsync(customerId, bookingId,
                scenario.OperationId("cancel"), cancellationToken)).Cancellation;
        }

        var terminalMatches = scenario.Queue == "History"
            ? cancellation is not null && cancellation.Id == scenario.OperationId("cancel") &&
                cancellation.Kind == TestDriveCancellationKind.CustomerCancelled && cancellation.CustomerId == customerId
            : cancellation is null;
        var handled = await database.TestDriveStarts.AnyAsync(item => item.BookingId == bookingId, cancellationToken) ||
            await database.TestDriveOutcomes.AnyAsync(item => item.BookingId == bookingId, cancellationToken);
        return new(label, terminalMatches && !handled ? "matches" : "lifecycle drift; preserved", !terminalMatches || handled);
    }

    // Interleaves the queues and spreads their bookings over the whole horizon, so no day holds a whole queue.
    private async Task<AppointmentSlot?> FindSlotAsync(DevelopmentTestDriveVolumeScenario scenario, Guid handlerId,
        CancellationToken cancellationToken)
    {
        var now = await database.DatabaseNowAsync(cancellationToken);
        var window = schedule.GetDateWindow(now);
        var occupied = await DevelopmentTestDriveSlots.ActiveStartsAsync(database, scenario.BranchId, cancellationToken);
        var free = new List<AppointmentSlot>();
        for (var date = window.FirstDate; date <= window.LastDate; date = date.AddDays(1))
        {
            free.AddRange(schedule.GetSlots(date, now, TestDriveBooking.Duration)
                .Where(slot => !occupied.Contains(slot.StartsAt)));
        }

        var queues = new[] { "History", "MyWork", "ToClaim" };
        var position = scenario.Index * queues.Length + Array.IndexOf(queues, scenario.Queue);
        var total = DevelopmentTestDriveVolumeManifest.RowsPerQueue * queues.Length;
        var start = free.Count * position / total;
        for (var offset = 0; offset < free.Count; offset++)
        {
            var slot = free[(start + offset) % free.Count];
            if (!await StaffScheduleConflicts.HasOverlapAsync(database, handlerId, slot.StartsAt, slot.EndsAt, null, null,
                cancellationToken))
            {
                return slot;
            }
        }

        return null;
    }
}
