using Dealership.Application.Accounts;
using Dealership.Domain.TestDrives;

namespace Dealership.Application.TestDrives;

public enum TestDriveHandlingStatus
{
    Succeeded,
    Invalid,
    Forbidden,
    NotFound,
    Conflict,
    Unavailable,
    ScheduleConflict
}

public sealed record TestDriveStartRequest(Guid OperationId, Guid AssignmentId,
    bool CustomerPresent, bool DrivingLicenceChecked);

public sealed record TestDriveOutcomeRequest(Guid OperationId, Guid AssignmentId, string Kind,
    bool CustomerPresent, string? Reason = null, string? Notes = null);

public sealed record TestDriveHandlingResult(TestDriveHandlingStatus Status,
    TestDriveStart? Start = null, TestDriveOutcome? Outcome = null);

public sealed record TestDriveHandlingContext(TestDriveBooking? Booking, BranchStaff? Actor,
    bool BranchActive, bool Cancelled, TestDriveAssignment? Current, TestDriveStart? Start,
    TestDriveOutcome? Outcome, TestDriveStart? ExistingStart, TestDriveOutcome? ExistingOutcome);

public interface ITestDriveHandlingStore
{
    Task<ITestDriveHandlingTransaction> LockAsync(Guid actorId, Guid bookingId, Guid operationId,
        CancellationToken cancellationToken);
}

public interface ITestDriveHandlingTransaction : IAsyncDisposable
{
    TestDriveHandlingContext Context { get; }

    Task<DateTimeOffset> ReadDatabaseTimeAsync(CancellationToken cancellationToken);

    Task<bool> IsVehicleEligibleAsync(CancellationToken cancellationToken);

    Task<bool> HasStartConflictAsync(Guid handlerId, DateTimeOffset startsAt,
        CancellationToken cancellationToken);

    Task CommitStartAsync(TestDriveStart start, CancellationToken cancellationToken);

    Task CommitOutcomeAsync(TestDriveOutcome outcome, CancellationToken cancellationToken);
}
