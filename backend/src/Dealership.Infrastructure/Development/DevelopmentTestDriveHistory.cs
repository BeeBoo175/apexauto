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
// Branch managers handle most of them; every other completed drive goes to an agent, whose history queue shows it.
public sealed class DevelopmentTestDriveHistory(DealershipDbContext database, AppointmentSchedule schedule)
{
    private const int FirstFreeListing = 12;

    private static readonly DevelopmentTestDriveHistoryKind[][] Plan =
    [
        [DevelopmentTestDriveHistoryKind.Completed, DevelopmentTestDriveHistoryKind.NotCompleted],
        [DevelopmentTestDriveHistoryKind.Completed, DevelopmentTestDriveHistoryKind.CustomerCancelled,
            DevelopmentTestDriveHistoryKind.Completed, DevelopmentTestDriveHistoryKind.NotCompleted],
        [DevelopmentTestDriveHistoryKind.Completed, DevelopmentTestDriveHistoryKind.StaffCancelled],
        [DevelopmentTestDriveHistoryKind.NotCompleted, DevelopmentTestDriveHistoryKind.NoShow,
            DevelopmentTestDriveHistoryKind.Completed, DevelopmentTestDriveHistoryKind.Completed],
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

    public static Guid HandlerFor(DevelopmentTestDriveHistoryItem item)
    {
        var branchId = item.Listing.Intake.BranchId;
        var agentHandles = item.Kind == DevelopmentTestDriveHistoryKind.Completed && item.Sequence % 2 == 0;
        return agentHandles
            ? DevelopmentFixtureManifest.AgentFor(branchId, item.Id).Id
            : DevelopmentFixtureManifest.Staff.Single(row =>
                row.BranchId == branchId && row.Role == AccountRoles.BranchManager).Id;
    }

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
        var slot = await FindSlotAsync(item.Listing.Intake.BranchId, bookedAt, now, item.Sequence + position,
            cancellationToken);
        if (slot is null)
        {
            // A listing published too recently has no slot that has safely passed, so it has no test-drive history.
            return;
        }

        var vehicle = await database.Vehicles.AsNoTracking()
            .SingleAsync(row => row.IntakeAppointmentId == item.Listing.Id, cancellationToken);
        var customerId = await database.CustomerProfiles.AsNoTracking()
            .Where(row => row.RegistrationKey == item.CustomerKey).Select(row => row.UserId)
            .SingleAsync(cancellationToken);
        var email = await database.Users.AsNoTracking().Where(row => row.Id == customerId)
            .Select(row => row.Email!).SingleAsync(cancellationToken);
        var managerId = HandlerFor(item);
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

    // Picks a free past slot after the booking, spread over the days since so history never piles onto one day.
    private async Task<AppointmentSlot?> FindSlotAsync(Guid branchId, DateTimeOffset bookedAt, DateTimeOffset now,
        int spread, CancellationToken cancellationToken)
    {
        var window = schedule.GetDateWindow(bookedAt);
        var occupied = await DevelopmentTestDriveSlots.ActiveStartsAsync(database, branchId, cancellationToken);
        var intakes = await database.IntakeAppointments.AsNoTracking()
            .Where(item => item.BranchId == branchId && item.CancelledAt == null)
            .Select(item => new { item.StartsAt, item.EndsAt })
            .ToArrayAsync(cancellationToken);
        var candidates = new List<AppointmentSlot>();
        for (var date = window.FirstDate; date <= window.LastDate; date = date.AddDays(1))
        {
            candidates.AddRange(schedule.GetSlots(date, bookedAt, TestDriveBooking.Duration).Where(slot =>
                slot.EndsAt + TimeSpan.FromHours(2) <= now && !occupied.Contains(slot.StartsAt) &&
                !intakes.Any(intake => intake.StartsAt < slot.EndsAt && slot.StartsAt < intake.EndsAt)));
        }

        return candidates.Count == 0 ? null : candidates[spread * 7 % candidates.Count];
    }
}
