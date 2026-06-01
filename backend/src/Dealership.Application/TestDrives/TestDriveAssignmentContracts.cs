using Dealership.Domain.TestDrives;

namespace Dealership.Application.TestDrives;

public sealed record TestDriveStaff(bool IsActive, string Role, Guid? BranchId);

public sealed record TestDriveAssignmentRequest(Guid OperationId, long ExpectedVersion,
    Guid? HandlerId = null, string? Reason = null);

public enum TestDriveAssignmentStatus
{
    Succeeded,
    Invalid,
    Forbidden,
    NotFound,
    Conflict,
    Unavailable,
    ScheduleConflict
}

public sealed record TestDriveAssignmentResult(TestDriveAssignmentStatus Status,
    TestDriveAssignment? Receipt = null, TestDriveAssignment? Current = null);

public sealed record TestDriveAssignmentContext(TestDriveBooking? Booking, TestDriveStaff? Actor,
    TestDriveStaff? Handler, bool BranchActive, bool Cancelled, TestDriveAssignment? Existing,
    TestDriveAssignment? Current, bool Started = false, bool Completed = false);

public interface ITestDriveAssignmentStore
{
    Task<ITestDriveAssignmentTransaction> LockAsync(Guid actorId, Guid bookingId,
        TestDriveAssignmentRequest request, CancellationToken cancellationToken);
}

public interface ITestDriveAssignmentTransaction : IAsyncDisposable
{
    TestDriveAssignmentContext Context { get; }

    Task<DateTimeOffset> ReadDatabaseTimeAsync(CancellationToken cancellationToken);

    Task<bool> HasOverlapAsync(Guid handlerId, CancellationToken cancellationToken);

    Task CommitAsync(TestDriveAssignment assignment, CancellationToken cancellationToken);
}
