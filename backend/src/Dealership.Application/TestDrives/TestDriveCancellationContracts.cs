using Dealership.Domain.TestDrives;

namespace Dealership.Application.TestDrives;

public enum TestDriveCancellationStatus
{
    Ready,
    Succeeded,
    Invalid,
    Forbidden,
    NotFound,
    Conflict,
    TooLate
}

public sealed record TestDriveCancellationResult(TestDriveCancellationStatus Status, TestDriveCancellation? Cancellation = null);

public sealed record TestDriveCancellationContext(TestDriveCancellationStatus Status, DateTimeOffset Now,
    TestDriveBooking? Booking = null, TestDriveCancellation? Existing = null, bool Cancelled = false);

public interface ITestDriveCancellationTransaction : IAsyncDisposable
{
    TestDriveCancellationContext Context { get; }

    Task<TestDriveCancellationResult> CommitAsync(TestDriveCancellation cancellation, CancellationToken cancellationToken);
}

public interface ITestDriveCancellationStore
{
    Task<ITestDriveCancellationTransaction> LockAsync(Guid customerId, Guid bookingId, Guid operationId,
        CancellationToken cancellationToken);
}
