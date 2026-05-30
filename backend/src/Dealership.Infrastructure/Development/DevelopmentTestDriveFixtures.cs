using Dealership.Application.TestDrives;
using Dealership.Application.Accounts;
using Dealership.Domain.Common;
using Dealership.Domain.Intake;
using Dealership.Domain.TestDrives;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Intake;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Development;

public sealed class DevelopmentTestDriveFixtures(DealershipDbContext database, TestDriveBookingService bookings,
    TestDriveCancellationService cancellations, TestDriveAssignmentService assignments, InspectionSchedule schedule)
{
    public static IReadOnlyList<DevelopmentInspectionScenario> Scenarios { get; } = Array.AsReadOnly(
        DevelopmentFixtureManifest.Branches.Select(branch => DevelopmentInspectionScenarios.All.First(item =>
            item.PublishListing && item.Intake.BranchId == branch.Id)).ToArray());

    public async Task<IReadOnlyList<DevelopmentFixtureReport>> RunAsync(bool seed, CancellationToken cancellationToken)
    {
        var reports = new List<DevelopmentFixtureReport>();
        for (var index = 0; index < Scenarios.Count; index++)
        {
            reports.Add(await RunOneAsync(seed, Scenarios[index], index, cancellationToken));
        }

        return reports;
    }

    private async Task<DevelopmentFixtureReport> RunOneAsync(bool seed, DevelopmentInspectionScenario scenario,
        int index, CancellationToken cancellationToken)
    {
        var label = $"Test drive ({scenario.Intake.RegistrationPlate})";
        var registrationKey = DevelopmentFixtureManifest.Customers[index + 1].Registration.RegistrationKey;
        var customerId = await database.CustomerProfiles.Where(item => item.RegistrationKey == registrationKey)
            .Select(item => (Guid?)item.UserId).SingleOrDefaultAsync(cancellationToken);
        var vehicleId = await database.Vehicles.Where(item => item.IntakeAppointmentId == scenario.Id)
            .Select(item => (Guid?)item.Id).SingleOrDefaultAsync(cancellationToken);
        if (customerId is null || vehicleId is null)
        {
            return new(label, "customer/vehicle missing; preserved", true);
        }

        var id = scenario.OperationId("test-drive-booking");
        var existing = await database.TestDriveBookings.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        var publicationId = scenario.OperationId("listing-publication");
        if (existing is not null)
        {
            var matches = existing.CustomerId == customerId && existing.VehicleId == vehicleId &&
                existing.PublicationId == publicationId && existing.BranchId == scenario.Intake.BranchId;
            if (!matches)
            {
                return new(label, "booking drift; preserved", true);
            }

            return await ReviewCancellationAsync(seed, index, scenario, existing, label, cancellationToken);
        }

        if (!seed)
        {
            return new(label, "booking missing", true);
        }

        var now = await database.DatabaseNowAsync(cancellationToken);
        var malaysia = TimeZoneInfo.FindSystemTimeZoneById(DealershipLocale.MalaysiaTimeZoneId);
        var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, malaysia).DateTime);
        var slot = await FindSlotAsync(index, scenario.Intake.BranchId, date, now, cancellationToken);
        if (slot is null)
        {
            return new(label, "schedule unavailable; preserved", true);
        }

        var result = await bookings.BookAsync(customerId.Value, vehicleId.Value,
            new(id, publicationId, slot.StartsAt), cancellationToken);
        var succeeded = result.Status == TestDriveBookingStatus.Succeeded;
        if (!succeeded)
        {
            return new(label, "booking unavailable; preserved", true);
        }

        return await ReviewCancellationAsync(seed, index, scenario, result.Booking!, label, cancellationToken);
    }

    private async Task<DevelopmentFixtureReport> ReviewCancellationAsync(bool seed, int index,
        DevelopmentInspectionScenario scenario, TestDriveBooking booking, string label, CancellationToken cancellationToken)
    {
        if (index == 1)
        {
            return await ReviewOwnershipAsync(seed, scenario, booking, label, cancellationToken);
        }

        if (index != 0)
        {
            return new(label, "matches; booked time preserved", false);
        }

        var operationId = scenario.OperationId("test-drive-cancellation");
        var existing = await database.TestDriveCancellations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.BookingId == booking.Id, cancellationToken);
        if (existing is not null)
        {
            var matches = existing.Id == operationId && existing.CustomerId == booking.CustomerId;
            return new(label, matches ? "cancelled; receipt preserved" : "cancellation drift; preserved", !matches);
        }

        if (!seed)
        {
            return new(label, "cancellation missing", true);
        }

        var result = await cancellations.CancelAsync(booking.CustomerId, booking.Id, operationId, cancellationToken);
        var succeeded = result.Status == TestDriveCancellationStatus.Succeeded;
        return new(label, succeeded ? "cancelled" : "cancellation unavailable; preserved", !succeeded);
    }

    private async Task<DevelopmentFixtureReport> ReviewOwnershipAsync(bool seed,
        DevelopmentInspectionScenario scenario, TestDriveBooking booking, string label, CancellationToken cancellationToken)
    {
        var handlerId = DevelopmentFixtureManifest.Staff.Single(item =>
            item.BranchId == booking.BranchId && item.Role == AccountRoles.BranchManager).Id;
        var operationId = scenario.OperationId("test-drive-claim");
        var current = await database.TestDriveAssignments.AsNoTracking()
            .Where(item => item.BookingId == booking.Id).OrderByDescending(item => item.Version)
            .FirstOrDefaultAsync(cancellationToken);
        if (current is not null)
        {
            var matches = current.Id == operationId && current.HandlerId == handlerId &&
                current.ActorId == handlerId && current.Kind == TestDriveAssignmentKind.Claimed && current.Version == 1;
            return new(label, matches ? "claimed; receipt preserved" : "ownership drift; preserved", !matches);
        }

        if (!seed)
        {
            return new(label, "ownership missing", true);
        }

        var result = await assignments.ExecuteAsync(handlerId, booking.Id, TestDriveAssignmentKind.Claimed,
            new(operationId, 0), cancellationToken);
        var succeeded = result.Status == TestDriveAssignmentStatus.Succeeded;
        return new(label, succeeded ? "claimed" : "claim unavailable; preserved", !succeeded);
    }

    private async Task<InspectionSlot?> FindSlotAsync(int index, Guid branchId, DateOnly date,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        var handlerId = DevelopmentFixtureManifest.Staff.Single(item =>
            item.BranchId == branchId && item.Role == AccountRoles.BranchManager).Id;
        var slots = Enumerable.Range(0, 30).SelectMany(day =>
            schedule.GetSlots(date.AddDays(day), now, TestDriveBooking.Duration));
        foreach (var slot in slots)
        {
            if (index != 1 || !await StaffScheduleConflicts.HasOverlapAsync(database, handlerId,
                slot.StartsAt, slot.EndsAt, null, null, cancellationToken))
            {
                return slot;
            }
        }

        return null;
    }
}
