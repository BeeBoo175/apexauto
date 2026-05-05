using Dealership.Domain.Sales;
using Dealership.Domain.Vehicles;

namespace Dealership.Application.Vehicles;

public sealed record ReservationCancellationRequest(Guid OperationId, string? Reason);

public sealed record ReservationCancellationResult(ReservationStatus Status, ReservationRelease? Release = null);

public sealed record ReservationActor(bool IsActive, string Role, Guid? BranchId);

public sealed record ReservationCancellationContext(ReservationActor? Actor, bool BranchActive,
    Reservation? Reservation, ReservationRelease? Release, ReservationRelease? Existing,
    bool IsHoldOperation, DateTimeOffset Now)
{
    // The reservation's sale, locked with it; only an open sale can be cancelled online.
    public SalesOrder? Sale { get; init; }
}

public interface IReservationCancellationTransaction : IAsyncDisposable
{
    ReservationCancellationContext Context { get; }

    // The release is saved before the sale closes, because its guard judges the sale as it stands.
    Task<ReservationStatus> CommitAsync(ReservationRelease? release, Func<SalesOrderEvent>? close,
        CancellationToken cancellationToken);
}

public interface IReservationCancellationStore
{
    Task<IReservationCancellationTransaction> LockAsync(Guid actorId, Guid reservationId, Guid operationId,
        CancellationToken cancellationToken);
}
