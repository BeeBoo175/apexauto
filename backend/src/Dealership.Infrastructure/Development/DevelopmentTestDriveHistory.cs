using Dealership.Application.Accounts;
using Dealership.Domain.Common;
using Dealership.Domain.Intake;
using Dealership.Domain.TestDrives;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.TestDrives;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Development;

public enum DevelopmentTestDriveHistoryKind
{
    Completed,
    NotCompleted,
    NoShow,
    CustomerCancelled,
    StaffCancelled
}

public sealed record DevelopmentTestDriveHistoryItem(DevelopmentInspectionScenario Listing,
    DevelopmentTestDriveHistoryKind Kind, int Sequence)
{
    public Guid Id => Listing.OperationId($"test-drive-history-{Sequence}-{Kind}");

    // A no-show stays outstanding for its customer, so each branch gives its own customer that last booking.
    public Guid CustomerKey => (Kind == DevelopmentTestDriveHistoryKind.NoShow
        ? DevelopmentFixtureManifest.Customers[6 + BranchIndex]
        : DevelopmentFixtureManifest.Customers[1 + Sequence % 5]).Registration.RegistrationKey;

    private int BranchIndex => DevelopmentFixtureManifest.Branches.ToList()
        .FindIndex(branch => branch.Id == Listing.Intake.BranchId);
}

// Builds finished test drives of dated listings at their historical times inside the snapshot transaction, mirroring
// the canonical booking, claim, attendance and cancellation commands, which can only stamp the database clock.
// Branch managers handle them, so the agents' working queues keep only the paged volume scenarios.
public sealed class DevelopmentTestDriveHistory(DealershipDbContext database, AppointmentSchedule schedule)
{
    private const int FirstFreeListing = 12;

    private static readonly DevelopmentTestDriveHistoryKind[][] Plan =
    [
        [DevelopmentTestDriveHistoryKind.Completed, DevelopmentTestDriveHistoryKind.NotCompleted],
        [DevelopmentTestDriveHistoryKind.Completed, DevelopmentTestDriveHistoryKind.CustomerCancelled],
        [DevelopmentTestDriveHistoryKind.Completed, DevelopmentTestDriveHistoryKind.StaffCancelled],
        [DevelopmentTestDriveHistoryKind.NotCompleted, DevelopmentTestDriveHistoryKind.NoShow],
        [DevelopmentTestDriveHistoryKind.Completed]
    ];

    public static IReadOnlyList<DevelopmentTestDriveHistoryItem> All { get; } = Array.AsReadOnly(
        DevelopmentFixtureManifest.Branches.SelectMany(branch =>
        {
            var listings = DevelopmentInspectionScenarios.All.Where(item => item.Intake.BranchId == branch.Id &&
                item.PublishListing && !DevelopmentIntakeManifest.SalesExamples.Contains(item.Intake)).ToArray();
            var sequence = 0;
            return Plan.SelectMany((kinds, index) => kinds.Select(kind =>
                new DevelopmentTestDriveHistoryItem(listings[FirstFreeListing + index], kind, sequence++)));
        }).ToArray());

    public static IReadOnlyList<DevelopmentTestDriveHistoryItem> For(DevelopmentInspectionScenario listing) =>
        [.. All.Where(item => item.Listing.Id == listing.Id)];

    public async Task AddAsync(IReadOnlyList<DevelopmentTestDriveHistoryItem> items, DateTimeOffset publishedAt,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        var gap = now - publishedAt;
        for (var position = 0; position < items.Count; position++)
        {
            var bookedAt = DevelopmentReservationHistory.Instant(publishedAt, gap, 1 + position * 2, 10);
            await AddOneAsync(items[position], bookedAt, position, now, cancellationToken);
        }
    }

    private async Task AddOneAsync(DevelopmentTestDriveHistoryItem item, DateTimeOffset bookedAt, int position,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        var slot = FindSlot(bookedAt, item.Sequence + position);
        if (slot.EndsAt + TimeSpan.FromHours(2) > now)
        {
            // A slot that has not safely passed cannot be history; a recently published listing simply has none.
            return;
        }

        var vehicle = await database.Vehicles.AsNoTracking()
            .SingleAsync(row => row.IntakeAppointmentId == item.Listing.Id, cancellationToken);
        var customerId = await database.CustomerProfiles.AsNoTracking()
            .Where(row => row.RegistrationKey == item.CustomerKey).Select(row => row.UserId)
            .SingleAsync(cancellationToken);
        var email = await database.Users.AsNoTracking().Where(row => row.Id == customerId)
            .Select(row => row.Email!).SingleAsync(cancellationToken);
        var managerId = DevelopmentFixtureManifest.Staff.Single(row =>
            row.BranchId == vehicle.BranchId && row.Role == AccountRoles.BranchManager).Id;
        var booking = new TestDriveBooking(item.Id, customerId, vehicle.Id, vehicle.BranchId,
            item.Listing.OperationId("listing-publication"), slot, bookedAt);
        database.TestDriveBookings.Add(booking);
        database.TestDriveMailOutbox.Add(new TestDriveMail(booking, email));
        await database.SaveChangesAsync(cancellationToken);

        if (item.Kind == DevelopmentTestDriveHistoryKind.CustomerCancelled)
        {
            // Customers cancel only what nobody handles yet.
            var cancelled = new TestDriveCancellation(
                item.Listing.OperationId($"test-drive-history-{item.Sequence}-cancel"), booking, customerId,
                bookedAt + TimeSpan.FromHours(1));
            database.TestDriveCancellations.Add(cancelled);
            database.TestDriveMailOutbox.Add(new TestDriveMail(cancelled, email));
            await database.SaveChangesAsync(cancellationToken);
            return;
        }

        var claim = new TestDriveAssignment(item.Listing.OperationId($"test-drive-history-{item.Sequence}-claim"), booking,
            managerId, WorkOwnershipKind.Claimed, managerId, null, null, bookedAt + TimeSpan.FromMinutes(10));
        database.TestDriveAssignments.Add(claim);
        await database.SaveChangesAsync(cancellationToken);
        await RecordAttendanceAsync(item, booking, claim, managerId, slot, email, cancellationToken);
    }

    private async Task RecordAttendanceAsync(DevelopmentTestDriveHistoryItem item, TestDriveBooking booking,
        TestDriveAssignment claim, Guid managerId, AppointmentSlot slot, string email,
        CancellationToken cancellationToken)
    {
        switch (item.Kind)
        {
            case DevelopmentTestDriveHistoryKind.Completed:
                var start = new TestDriveStart(item.Listing.OperationId($"test-drive-history-{item.Sequence}-start"),
                    booking, claim, managerId, true, true, slot.StartsAt + TimeSpan.FromMinutes(2));
                database.TestDriveStarts.Add(start);
                await database.SaveChangesAsync(cancellationToken);
                database.TestDriveOutcomes.Add(new TestDriveOutcome(
                    item.Listing.OperationId($"test-drive-history-{item.Sequence}-outcome"), booking, claim, managerId,
                    start, TestDriveOutcomeKind.Completed, true, null,
                    "Customer drove the vehicle and was satisfied with the handling.",
                    start.StartedAt + TimeSpan.FromMinutes(24)));
                break;
            case DevelopmentTestDriveHistoryKind.NotCompleted:
                database.TestDriveOutcomes.Add(new TestDriveOutcome(
                    item.Listing.OperationId($"test-drive-history-{item.Sequence}-outcome"), booking, claim, managerId,
                    null, TestDriveOutcomeKind.NotCompleted, true, "Customer could not present a valid driving licence.",
                    null, slot.StartsAt + TimeSpan.FromMinutes(5)));
                break;
            case DevelopmentTestDriveHistoryKind.StaffCancelled:
                var cancelled = TestDriveCancellation.ByStaff(
                    item.Listing.OperationId($"test-drive-history-{item.Sequence}-cancel"), booking, managerId,
                    "Vehicle is unavailable for viewing because of workshop scheduling.",
                    claim.RecordedAt + TimeSpan.FromHours(1));
                database.TestDriveCancellations.Add(cancelled);
                database.TestDriveMailOutbox.Add(new TestDriveMail(cancelled, email));
                break;
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    // Spreads bookings over distinct open days after the booking time so branch slots never collide.
    private AppointmentSlot FindSlot(DateTimeOffset bookedAt, int spread)
    {
        var window = schedule.GetDateWindow(bookedAt);
        var skip = spread * 2;
        for (var date = window.FirstDate; date <= window.LastDate; date = date.AddDays(1))
        {
            var slots = schedule.GetSlots(date, bookedAt, TestDriveBooking.Duration);
            if (slots.Count == 0)
            {
                continue;
            }

            if (skip-- > 0)
            {
                continue;
            }

            return slots[spread % slots.Count];
        }

        throw new InvalidOperationException("The configured schedule cannot place a historical test drive.");
    }
}
