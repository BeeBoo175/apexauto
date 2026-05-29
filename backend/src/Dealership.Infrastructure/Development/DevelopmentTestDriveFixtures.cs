using Dealership.Application.TestDrives;
using Dealership.Domain.Common;
using Dealership.Domain.Intake;
using Dealership.Domain.TestDrives;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Development;

public sealed class DevelopmentTestDriveFixtures(DealershipDbContext database, TestDriveBookingService bookings,
    TestDriveCancellationService cancellations, InspectionSchedule schedule)
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
        var slot = Enumerable.Range(0, 30).SelectMany(day =>
            schedule.GetSlots(date.AddDays(day), now, TestDriveBooking.Duration)).FirstOrDefault();
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
}
