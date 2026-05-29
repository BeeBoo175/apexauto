using Dealership.Domain.TestDrives;

namespace Dealership.Application.TestDrives;

public sealed record TestDriveBookingRequest(Guid OperationId, Guid PublicationId, DateTimeOffset StartsAt);

public enum TestDriveBookingStatus
{
    Ready,
    Succeeded,
    Invalid,
    Forbidden,
    Conflict,
    VehicleUnavailable,
    CustomerHasBooking,
    SlotUnavailable
}

public sealed record TestDriveBookingResult(TestDriveBookingStatus Status, TestDriveBooking? Booking = null);

public sealed record TestDriveBookingContext(TestDriveBookingStatus Status, DateTimeOffset Now,
    Guid BranchId = default, bool CustomerHasBooking = false, bool SlotOccupied = false,
    TestDriveBooking? Existing = null);

public interface ITestDriveBookingTransaction : IAsyncDisposable
{
    TestDriveBookingContext Context { get; }

    Task<TestDriveBookingResult> CommitAsync(TestDriveBooking booking, CancellationToken cancellationToken);
}

public interface ITestDriveBookingStore
{
    Task<ITestDriveBookingTransaction> LockAsync(Guid customerId, Guid vehicleId,
        TestDriveBookingRequest request, CancellationToken cancellationToken);
}
