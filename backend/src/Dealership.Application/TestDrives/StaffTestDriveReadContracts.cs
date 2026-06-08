using Dealership.Application.Accounts;
using Dealership.Application.Common;

namespace Dealership.Application.TestDrives;

public sealed record StaffTestDriveQuery(ListDateRange Dates, int Page, int PageSize, string Queue,
    string Status, Guid? BranchId, Guid? HandlerId, string Search, string Sort, string Direction, string Handler = "")
{
    public static StaffTestDriveQuery? Parse(string? from, string? to, int page, int pageSize,
        string? queue = null, string? status = null, Guid? branchId = null, Guid? handlerId = null,
        string? search = null, string? sort = null, string? direction = null, string? handler = null)
    {
        var dates = ListDateRange.Parse(from, to);
        var text = search?.Trim() ?? string.Empty;
        if (dates is null || !Pagination.IsValid(page, pageSize) || !ListSearch.IsValid(text) || !ListSearch.IsValid(handler ?? "") ||
            branchId == Guid.Empty || handlerId == Guid.Empty ||
            queue is not (null or "" or "ToClaim" or "MyWork" or "History") ||
            status is not (null or "All" or "Booked" or "InProgress" or "Completed" or "NotCompleted" or "NoShow" or "Cancelled") ||
            sort is not (null or "startsAt" or "registrationPlate" or "vehicle" or "customerName" or "branchName" or "status" or "handlerEmail") ||
            direction is not (null or "asc" or "desc"))
        {
            return null;
        }

        return new(dates, page, pageSize, queue ?? string.Empty, status ?? "All", branchId, handlerId,
            text, sort ?? "startsAt", direction ?? "asc", handler?.Trim() ?? "");
    }
}

public sealed record StaffTestDriveItem(Guid Id, Guid VehicleId, Guid BranchId, string BranchName,
    string RegistrationPlate, string Make, string Model, string Trim, int ManufactureYear,
    string CustomerName, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string Status,
    Guid? AssignmentId, long OwnershipVersion, Guid? HandlerId, string? HandlerEmail, bool HandlerEligible);

// Counts use the same branch scope, independently of list dates, search and handler filters.
public sealed record StaffTestDriveCounts(int All, int Booked, int InProgress, int Completed,
    int NotCompleted, int NoShow, int Cancelled, int ToClaim, int MyWork, int History);

public sealed record StaffTestDrivePage(CurrentActor Actor, IReadOnlyList<StaffTestDriveItem> Items,
    int Total, int Page, int PageSize, StaffTestDriveCounts Counts);

public sealed record StaffTestDriveDetail(CurrentActor Actor, StaffTestDriveItem Booking,
    string CustomerMobile, string CustomerEmail, DateTimeOffset CreatedAt,
    TestDriveOwnership? Ownership, TestDriveCancellationView? Cancellation,
    TestDriveStartView? Start, TestDriveOutcomeView? Outcome, string? StartedByEmail, string? CompletedByEmail);

public sealed record TestDriveOwnership(Guid Id, Guid BookingId, long Version, Guid? HandlerId,
    Guid ActorId, string Kind, DateTimeOffset RecordedAt, string? Reason);

public sealed record TestDriveCancellationView(Guid Id, Guid BookingId, string Kind, Guid? StaffId,
    DateTimeOffset RecordedAt, string? Reason);

public sealed record TestDriveStartView(Guid Id, Guid BookingId, Guid AssignmentId, Guid StaffId,
    bool CustomerPresent, bool DrivingLicenceChecked, DateTimeOffset StartedAt);

public sealed record TestDriveOutcomeView(Guid Id, Guid BookingId, Guid AssignmentId, Guid StaffId,
    Guid? StartId, string Kind, bool CustomerPresent, string? Reason, string? Notes, DateTimeOffset EndedAt);

public sealed record TestDriveHandlers(CurrentActor Actor, IReadOnlyList<StaffHandler> Items);

public sealed record StaffTestDriveReadResult<T>(TestDriveReadStatus Status, T? Value = default);

public interface IStaffTestDriveReadStore
{
    Task<StaffTestDriveReadResult<StaffTestDrivePage>> ListAsync(Guid staffId, StaffTestDriveQuery query,
        CancellationToken cancellationToken);

    Task<StaffTestDriveReadResult<StaffTestDriveDetail>> GetAsync(Guid staffId, Guid bookingId,
        CancellationToken cancellationToken);

    Task<StaffTestDriveReadResult<TestDriveHandlers>> HandlersAsync(Guid staffId, Guid bookingId,
        CancellationToken cancellationToken);
}
