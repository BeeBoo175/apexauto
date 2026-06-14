using Dealership.Application.Accounts;
using Dealership.Application.Inspections;
using Dealership.Application.TestDrives;
using Dealership.Domain.Common;
using Dealership.Domain.Intake;
using Dealership.Domain.TestDrives;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Development;

public sealed class DevelopmentTestDriveReopeningFixtures(DealershipDbContext database,
    TestDriveBookingService bookings, InspectionReopeningService reopenings, AppointmentSchedule schedule)
{
    public static DevelopmentInspectionScenario Scenario => DevelopmentInspectionScenarios.PublishedAt(0, 4);

    public static Guid BookingId => Scenario.OperationId("test-drive-reopening-booking");
    public static Guid ReopeningId => Scenario.OperationId("test-drive-inspection-reopening");
    public const string Reason = "Recheck vehicle condition before the next customer demonstration";

    public async Task<DevelopmentFixtureReport> RunAsync(bool seed, CancellationToken cancellationToken)
    {
        const string label = "Reinspection-cancelled test drive";
        var scenario = Scenario;
        var customerKey = DevelopmentFixtureManifest.Customers[1].Registration.RegistrationKey;
        var customerId = await database.CustomerProfiles.Where(item => item.RegistrationKey == customerKey)
            .Select(item => (Guid?)item.UserId).SingleOrDefaultAsync(cancellationToken);
        var vehicleId = await database.Vehicles.Where(item => item.IntakeAppointmentId == scenario.Id)
            .Select(item => (Guid?)item.Id).SingleOrDefaultAsync(cancellationToken);
        if (customerId is null || vehicleId is null)
        {
            return new(label, "customer/vehicle missing; preserved", true);
        }

        var publicationId = scenario.OperationId("listing-publication");
        var booking = await database.TestDriveBookings.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == BookingId, cancellationToken);
        if (booking is null && seed)
        {
            var occupied = await DevelopmentTestDriveSlots.ActiveStartsAsync(database,
                scenario.Intake.BranchId, cancellationToken);
            var slot = (await DevelopmentTestDriveSlots.UpcomingAsync(database, schedule, cancellationToken))
                .FirstOrDefault(item => !occupied.Contains(item.StartsAt));
            if (slot is null)
            {
                return new(label, "schedule unavailable; preserved", true);
            }

            booking = (await bookings.BookAsync(customerId.Value, vehicleId.Value,
                new(BookingId, publicationId, slot.StartsAt), cancellationToken)).Booking;
        }

        if (booking is null || booking.CustomerId != customerId || booking.VehicleId != vehicleId ||
            booking.PublicationId != publicationId || booking.BranchId != scenario.Intake.BranchId)
        {
            return new(label, "booking missing/drift; preserved", true);
        }

        var cancellation = await database.TestDriveCancellations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.BookingId == BookingId, cancellationToken);
        if (cancellation is not null && (cancellation.Kind != TestDriveCancellationKind.InspectionReopened ||
            cancellation.InspectionReopeningId != ReopeningId))
        {
            return new(label, "cancellation drift; preserved", true);
        }

        var managerId = ManagerId();
        if (seed && !await database.InspectionReopenings.AnyAsync(item => item.Id == ReopeningId, cancellationToken))
        {
            await reopenings.ReopenAsync(managerId, scenario.Id, new(ReopeningId, 1, 1, Reason), cancellationToken);
        }

        cancellation = await database.TestDriveCancellations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.BookingId == BookingId, cancellationToken);
        var matches = await MatchesReopeningAsync(database, scenario.Id, cancellationToken) &&
            cancellation is not null && cancellation.Kind == TestDriveCancellationKind.InspectionReopened &&
            cancellation.CustomerId == customerId && cancellation.InspectionReopeningId == ReopeningId &&
            cancellation.StaffId is null && cancellation.ReservationId is null && cancellation.ListingWithdrawalId is null &&
            cancellation.Reason == "Vehicle requires reinspection";
        return new(label, matches ? "reopened; system receipt preserved" : "reopening/cancellation missing/drift; preserved", !matches);
    }

    internal static async Task<bool> MatchesReopeningAsync(DealershipDbContext database, Guid inspectionId,
        CancellationToken cancellationToken)
    {
        if (inspectionId != Scenario.Id)
        {
            return false;
        }

        var managerId = ManagerId();
        var signedId = Scenario.Sign().OperationId;
        return await database.InspectionReopenings.AnyAsync(item => item.Id == ReopeningId &&
            item.InspectionId == inspectionId && item.BranchId == Scenario.Intake.BranchId && item.ActorId == managerId &&
            item.SignedRevisionId == signedId && item.AssignmentVersion == 1 && item.ExpectedRevision == 1 &&
            item.Revision == 2 && item.AfterSaveVersion == 1 && item.Reason == Reason, cancellationToken) &&
            await database.InspectionDrafts.AnyAsync(item => item.Id == inspectionId && !item.IsSigned &&
                item.Revision == 2 && item.ReopenedAfterSaveVersion == 1 && item.AssignmentVersion == 1 &&
                item.TechnicianId == DevelopmentFixtureManifest.PrimaryTechnician(Scenario.Intake.BranchId).Id &&
                database.Vehicles.Any(vehicle => vehicle.Id == item.VehicleId && vehicle.Status == VehicleStatus.PendingInspection),
                cancellationToken);
    }

    private static Guid ManagerId() => DevelopmentFixtureManifest.Staff.Single(item =>
        item.BranchId == Scenario.Intake.BranchId && item.Role == AccountRoles.BranchManager).Id;
}
