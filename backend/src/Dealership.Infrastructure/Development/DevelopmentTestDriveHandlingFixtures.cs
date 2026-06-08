using Dealership.Application.Accounts;
using Dealership.Application.TestDrives;
using Dealership.Domain.Common;
using Dealership.Domain.Intake;
using Dealership.Domain.TestDrives;
using Dealership.Infrastructure.Intake;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Development;

public sealed class DevelopmentTestDriveHandlingFixtures(DealershipDbContext database,
    TestDriveBookingService bookings, TestDriveAssignmentService assignments, TestDriveHandlingService handling,
    AppointmentSchedule schedule)
{
    public static DevelopmentInspectionScenario Scenario => DevelopmentTestDriveFixtures.Scenarios[0];
    public static IReadOnlyList<string> Kinds { get; } = Array.AsReadOnly(new[] { "completed", "not-completed", "active" });

    public static Guid OperationId(string kind, string action) => Scenario.OperationId($"test-drive-{kind}-{action}");

    public async Task<IReadOnlyList<DevelopmentFixtureReport>> RunAsync(bool seed, CancellationToken cancellationToken)
    {
        var reports = new List<DevelopmentFixtureReport>();
        foreach (var kind in Kinds)
        {
            reports.Add(await RunOneAsync(kind, seed, cancellationToken));
        }

        return reports;
    }

    private async Task<DevelopmentFixtureReport> RunOneAsync(string kind, bool seed, CancellationToken cancellationToken)
    {
        var label = $"Test-drive attendance: {kind}";
        var customerKey = DevelopmentFixtureManifest.Customers[0].Registration.RegistrationKey;
        var customerId = await database.CustomerProfiles.Where(item => item.RegistrationKey == customerKey)
            .Select(item => (Guid?)item.UserId).SingleOrDefaultAsync(cancellationToken);
        var vehicleId = await database.Vehicles.Where(item => item.IntakeAppointmentId == Scenario.Id)
            .Select(item => (Guid?)item.Id).SingleOrDefaultAsync(cancellationToken);
        if (customerId is null || vehicleId is null)
        {
            return new(label, "customer/vehicle missing; preserved", true);
        }

        var staffId = DevelopmentFixtureManifest.Staff.Single(item => item.BranchId == Scenario.Intake.BranchId &&
            item.Role == AccountRoles.BranchManager).Id;
        var publicationId = Scenario.OperationId("listing-publication");
        var bookingId = OperationId(kind, "booking");
        var booking = await database.TestDriveBookings.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == bookingId, cancellationToken);
        if (booking is null && seed)
        {
            var slot = await FindSlotAsync(staffId, cancellationToken);
            if (slot is null)
            {
                return new(label, "awaiting a bookable same-day slot; no attendance fabricated", false);
            }

            booking = (await bookings.BookAsync(customerId.Value, vehicleId.Value,
                new(bookingId, publicationId, slot.StartsAt), cancellationToken)).Booking;
            if (booking is null)
            {
                return new(label, "booking unavailable; preserved", true);
            }
        }

        if (booking is null)
        {
            return new(label, "same-day attendance not initialized", false);
        }

        if (booking.CustomerId != customerId || booking.VehicleId != vehicleId ||
            booking.PublicationId != publicationId || booking.BranchId != Scenario.Intake.BranchId ||
            await database.TestDriveCancellations.AnyAsync(item => item.BookingId == booking.Id, cancellationToken))
        {
            return new(label, "booking drift; preserved", true);
        }

        var assignment = await database.TestDriveAssignments.AsNoTracking()
            .Where(item => item.BookingId == booking.Id).OrderByDescending(item => item.Version)
            .FirstOrDefaultAsync(cancellationToken);
        if (assignment is null && seed)
        {
            assignment = (await assignments.ExecuteAsync(staffId, booking.Id, TestDriveAssignmentKind.Claimed,
                new(OperationId(kind, "claim"), 0), cancellationToken)).Receipt;
        }

        if (assignment is null || assignment.Id != OperationId(kind, "claim") || assignment.HandlerId != staffId ||
            assignment.ActorId != staffId || assignment.Kind != TestDriveAssignmentKind.Claimed || assignment.Version != 1)
        {
            return new(label, "ownership missing/drift; preserved", true);
        }

        var start = await database.TestDriveStarts.AsNoTracking()
            .SingleOrDefaultAsync(item => item.BookingId == booking.Id, cancellationToken);
        if (start is null && seed && kind != "not-completed")
        {
            start = (await handling.StartAsync(staffId, booking.Id,
                new(OperationId(kind, "start"), assignment.Id, true, true), cancellationToken)).Start;
        }

        if (kind == "not-completed" && start is not null || kind != "not-completed" &&
            (start is null || start.Id != OperationId(kind, "start") || start.StaffId != staffId ||
                start.AssignmentId != assignment.Id || !start.CustomerPresent || !start.DrivingLicenceChecked))
        {
            return new(label, "start missing/drift; preserved", true);
        }

        var outcome = await database.TestDriveOutcomes.AsNoTracking()
            .SingleOrDefaultAsync(item => item.BookingId == booking.Id, cancellationToken);
        if (kind == "active")
        {
            var occupied = await database.TestDriveOccupations.AnyAsync(item => item.StartId == start!.Id &&
                item.VehicleId == booking.VehicleId && item.OutcomeId == null, cancellationToken);
            var matches = outcome is null && occupied;
            return new(label, matches ? "in progress; occupation retained without expiry" : "active occupation drift; preserved", !matches);
        }

        var outcomeKind = kind == "completed" ? TestDriveOutcomeKind.Completed : TestDriveOutcomeKind.NotCompleted;
        var reason = kind == "not-completed" ? "Customer did not bring a valid driving licence" : null;
        const string notes = "Customer attended the branch appointment";
        if (outcome is null && seed)
        {
            outcome = (await handling.RecordOutcomeAsync(staffId, booking.Id,
                new(OperationId(kind, "outcome"), assignment.Id, outcomeKind, true, reason, notes), cancellationToken)).Outcome;
        }

        var valid = outcome is not null && outcome.Id == OperationId(kind, "outcome") && outcome.StaffId == staffId &&
            outcome.AssignmentId == assignment.Id && outcome.StartId == start?.Id && outcome.Kind == outcomeKind &&
            outcome.CustomerPresent && outcome.Reason == reason && outcome.Notes == notes;
        if (start is not null)
        {
            valid = valid && await database.TestDriveOccupations.AnyAsync(item => item.StartId == start.Id &&
                item.OutcomeId == outcome!.Id && item.VehicleId == booking.VehicleId, cancellationToken);
        }

        return new(label, valid ? "attended outcome retained" : "outcome missing/drift; preserved", !valid);
    }

    private async Task<AppointmentSlot?> FindSlotAsync(Guid staffId, CancellationToken cancellationToken)
    {
        var now = await database.DatabaseNowAsync(cancellationToken);
        var malaysia = DealershipLocale.Malaysia;
        var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, malaysia).DateTime);
        var occupied = await database.TestDriveBookings.Where(item => item.BranchId == Scenario.Intake.BranchId &&
            !database.TestDriveCancellations.Any(cancellation => cancellation.BookingId == item.Id) &&
            !database.TestDriveOutcomes.Any(outcome => outcome.BookingId == item.Id))
            .Select(item => item.StartsAt).ToArrayAsync(cancellationToken);
        foreach (var slot in schedule.GetSlots(date, now, TestDriveBooking.Duration))
        {
            if (!occupied.Contains(slot.StartsAt) && !await StaffScheduleConflicts.HasOverlapAsync(database,
                staffId, slot.StartsAt, slot.EndsAt, null, null, cancellationToken))
            {
                return slot;
            }
        }

        return null;
    }
}
