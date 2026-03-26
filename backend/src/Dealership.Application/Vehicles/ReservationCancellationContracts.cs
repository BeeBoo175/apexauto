using Dealership.Domain.Vehicles;

namespace Dealership.Application.Vehicles;

public sealed record ReservationCancellationRequest(Guid OperationId, string? Reason);

public sealed record ReservationCancellationResult(ReservationStatus Status, ReservationRelease? Release = null);

public sealed record ReservationActor(bool IsActive, string Role, Guid? BranchId);

public sealed record ReservationCancellationContext(ReservationActor? Actor, bool BranchActive,
    Reservation? Reservation, ReservationRelease? Release, ReservationRelease? Existing,
    bool IsHoldOperation, DateTimeOffset Now);

public interface IReservationCancellationTransaction : IAsyncDisposable
{
    ReservationCancellationContext Context { get; }

    Task<ReservationStatus> CommitAsync(ReservationRelease? release, CancellationToken cancellationToken);
}

public interface IReservationCancellationStore
{
    Task<IReservationCancellationTransaction> LockAsync(Guid actorId, Guid reservationId, Guid operationId,
        CancellationToken cancellationToken);
}
