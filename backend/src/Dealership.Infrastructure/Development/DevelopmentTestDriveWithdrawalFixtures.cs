using Dealership.Application.Accounts;
using Dealership.Application.TestDrives;
using Dealership.Application.Vehicles;
using Dealership.Domain.Common;
using Dealership.Domain.Intake;
using Dealership.Domain.TestDrives;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Development;

public sealed class DevelopmentTestDriveWithdrawalFixtures(DealershipDbContext database,
    TestDriveBookingService bookings, ListingService listings, InspectionSchedule schedule)
{
    public static DevelopmentInspectionScenario Scenario => DevelopmentTestDriveFixtures.Scenarios[2];
    public static Guid BookingId => Scenario.OperationId("test-drive-withdrawal-booking");
    public static Guid WithdrawalId => Scenario.OperationId("test-drive-listing-withdrawal");
    public const string Reason = "Vehicle temporarily removed from the catalogue";

    public async Task<DevelopmentFixtureReport> RunAsync(bool seed, CancellationToken cancellationToken)
    {
        const string label = "Listing-withdrawn test drive";
        var source = await database.TestDriveBookings.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == DevelopmentTestDriveFixtures.ReservationCancellationBookingId, cancellationToken);
        if (source is null)
        {
            return new(label, "source booking missing; preserved", true);
        }

        var booking = await database.TestDriveBookings.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == BookingId, cancellationToken);
        if (booking is null && seed)
        {
            var now = await database.DatabaseNowAsync(cancellationToken);
            var malaysia = TimeZoneInfo.FindSystemTimeZoneById(DealershipLocale.MalaysiaTimeZoneId);
            var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, malaysia).DateTime);
            var slot = Enumerable.Range(0, 30).SelectMany(day =>
                schedule.GetSlots(date.AddDays(day), now, TestDriveBooking.Duration)).FirstOrDefault();
            if (slot is null)
            {
                return new(label, "schedule unavailable; preserved", true);
            }

            booking = (await bookings.BookAsync(source.CustomerId, source.VehicleId,
                new(BookingId, source.PublicationId, slot.StartsAt), cancellationToken)).Booking;
        }

        if (booking is null || booking.CustomerId != source.CustomerId || booking.VehicleId != source.VehicleId ||
            booking.BranchId != source.BranchId || booking.PublicationId != source.PublicationId)
        {
            return new(label, "booking missing/drift; preserved", true);
        }

        var managerId = DevelopmentFixtureManifest.Staff.Single(item =>
            item.BranchId == source.BranchId && item.Role == AccountRoles.BranchManager).Id;
        var withdrawal = await database.ListingWithdrawals.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == WithdrawalId, cancellationToken);
        var cancellation = await database.TestDriveCancellations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.BookingId == BookingId, cancellationToken);
        if (cancellation is not null && (cancellation.Kind != TestDriveCancellationKind.ListingWithdrawn ||
            cancellation.ListingWithdrawalId != WithdrawalId))
        {
            return new(label, "cancellation drift; preserved", true);
        }

        if (withdrawal is null && seed)
        {
            withdrawal = (await listings.WithdrawAsync(managerId, source.VehicleId,
                new(WithdrawalId, source.PublicationId, Reason), cancellationToken)).Withdrawal;
        }

        cancellation = await database.TestDriveCancellations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.BookingId == BookingId, cancellationToken);
        var matches = withdrawal is not null && withdrawal.ActorId == managerId &&
            withdrawal.VehicleId == source.VehicleId && withdrawal.PublicationId == source.PublicationId &&
            withdrawal.BranchId == source.BranchId && withdrawal.Reason == Reason &&
            cancellation is not null && cancellation.Kind == TestDriveCancellationKind.ListingWithdrawn &&
            cancellation.CustomerId == source.CustomerId && cancellation.ListingWithdrawalId == WithdrawalId &&
            cancellation.StaffId is null && cancellation.ReservationId is null && cancellation.Reason == "Listing withdrawn";
        return new(label, matches ? "withdrawn; system receipt preserved" : "withdrawal/cancellation missing/drift; preserved", !matches);
    }
}
