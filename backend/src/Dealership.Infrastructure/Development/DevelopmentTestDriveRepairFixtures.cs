using Dealership.Application.Inspections;
using Dealership.Application.TestDrives;
using Dealership.Domain.Common;
using Dealership.Domain.Inspections;
using Dealership.Domain.Intake;
using Dealership.Domain.TestDrives;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Development;

public sealed class DevelopmentTestDriveRepairFixtures(DealershipDbContext database,
    TestDriveBookingService bookings, InspectionRepairRequestService repairs, InspectionRepairCompletionService completions, AppointmentSchedule schedule)
{
    public static DevelopmentInspectionScenario Scenario => DevelopmentInspectionScenarios.All.Where(item =>
        item.PublishListing && item.Intake.BranchId == DevelopmentFixtureManifest.Branches[0].Id).ElementAt(1);

    public static Guid BookingId => Scenario.OperationId("test-drive-repair-booking");
    public static Guid RequestId => Scenario.OperationId("test-drive-inspection-repair");
    public static Guid CompletionId => Scenario.OperationId("test-drive-repair-completion");
    public const string Notes = "Bumper surface polished and checked before the next demonstration";

    public async Task<DevelopmentFixtureReport> RunAsync(bool seed, CancellationToken cancellationToken)
    {
        const string label = "Repair-cancelled test drive";
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
            var now = await database.DatabaseNowAsync(cancellationToken);
            var malaysia = DealershipLocale.Malaysia;
            var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, malaysia).DateTime);
            var occupied = await database.TestDriveBookings.Where(item => item.BranchId == scenario.Intake.BranchId &&
                !database.TestDriveCancellations.Any(cancellation => cancellation.BookingId == item.Id))
                .Select(item => item.StartsAt).ToArrayAsync(cancellationToken);
            var slot = Enumerable.Range(0, 30).SelectMany(day =>
                schedule.GetSlots(date.AddDays(day), now, TestDriveBooking.Duration))
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
        if (cancellation is not null && (cancellation.Kind != TestDriveCancellationKind.InspectionRepairRequested ||
            cancellation.InspectionRepairRequestId != RequestId))
        {
            return new(label, "cancellation drift; preserved", true);
        }

        var technicianId = TechnicianId();
        if (await database.InspectionRepairRequests.AnyAsync(item => item.Id == RequestId, cancellationToken) &&
            !await MatchesProgressAsync(database, scenario.Id, cancellationToken))
        {
            return new(label, "repair drift; preserved", true);
        }

        if (seed)
        {
            await repairs.RequestAsync(technicianId, scenario.Id, Request(), cancellationToken);
            await completions.CompleteAsync(technicianId, scenario.Id,
                new(CompletionId, RequestId, 1, 1, 1, Notes), cancellationToken);
        }

        cancellation = await database.TestDriveCancellations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.BookingId == BookingId, cancellationToken);
        var matches = await MatchesProgressAsync(database, scenario.Id, cancellationToken) &&
            cancellation is not null && cancellation.Kind == TestDriveCancellationKind.InspectionRepairRequested &&
            await database.InspectionRepairCompletions.AnyAsync(item => item.Id == CompletionId, cancellationToken) &&
            cancellation.CustomerId == customerId && cancellation.InspectionRepairRequestId == RequestId &&
            cancellation.InspectionReopeningId is null &&
            cancellation.StaffId is null && cancellation.ReservationId is null && cancellation.ListingWithdrawalId is null &&
            cancellation.Reason == "Vehicle requires repairs";
        return new(label, matches ? "repaired; system receipt preserved" : "repair/cancellation missing/drift; preserved", !matches);
    }

    internal static async Task<bool> MatchesProgressAsync(DealershipDbContext database, Guid inspectionId,
        CancellationToken cancellationToken)
    {
        if (inspectionId != Scenario.Id)
        {
            return false;
        }

        var technicianId = TechnicianId();
        var expected = Request();
        var receipt = await database.InspectionRepairRequests.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == RequestId, cancellationToken);
        if (receipt is null || receipt.BranchId != Scenario.Intake.BranchId ||
            receipt.SignedRevisionId != Scenario.Sign().OperationId ||
            !receipt.Receipt().Matches(technicianId, inspectionId, expected,
            new(expected.Items, expected.Budget)))
        {
            return false;
        }

        var completion = await database.InspectionRepairCompletions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.RequestId == RequestId, cancellationToken);
        var expectedCompletion = new InspectionRepairCompletionRequest(CompletionId, RequestId, 1, 1, 1, Notes);
        var completionMatches = completion is null || completion.Id == CompletionId &&
            completion.BranchId == Scenario.Intake.BranchId && completion.ApprovalId is null &&
            completion.Receipt().Matches(technicianId, inspectionId, expectedCompletion);
        return completionMatches && await database.InspectionDrafts.AnyAsync(item => item.Id == inspectionId &&
            item.IsSigned && item.Revision == 1 && item.RepairVersion == 1 && item.AssignmentVersion == 1 &&
            item.TechnicianId == technicianId, cancellationToken);
    }

    private static InspectionRepairRequest Request() =>
        new(RequestId, 1, 1, 0, [new("EP-004", "Polish the recorded bumper surface mark")], 0);

    private static Guid TechnicianId() => DevelopmentFixtureManifest.PrimaryTechnician(Scenario.Intake.BranchId).Id;
}
