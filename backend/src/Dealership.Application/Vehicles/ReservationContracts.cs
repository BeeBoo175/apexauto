using Dealership.Domain.Customers;
using Dealership.Domain.Vehicles;

namespace Dealership.Application.Vehicles;

public sealed record ReservationRequest(Guid OperationId, Guid PublicationId, Guid? PromoId);

public enum ReservationStatus
{
    Succeeded,
    Invalid,
    Forbidden,
    NotFound,
    Conflict,
    CustomerHasReservation,
    VehicleReserved,
    PromoUnavailable,
    Unavailable,
    // A deposit request that was cancelled or whose 24 hours have passed.
    DepositRequestLapsed,
    // The deposit's bill was paid before the cancellation, so the reservation stays.
    DepositAlreadyPaid
}

public sealed record ReservationResult(ReservationStatus Status, Reservation? Reservation = null);

public sealed record DepositRequestResult(ReservationStatus Status, ReservationDepositRequest? Request = null);

public sealed record ReservationCustomer(bool IsActive, bool EmailConfirmed, string Role, string? Email);

public sealed record ReservationContext(ReservationCustomer? Customer, CustomerProfile? Profile,
    ListingPublication? Publication, ListingRevision? Listing, PurchaseSettings? Settings, PromoCode? Promo,
    bool CustomerHasHold, bool VehicleHasHold, DateTimeOffset Now, Reservation? Existing, bool IsReleaseOperation,
    ReservationDepositRequest? ExistingRequest = null,
    // The customer's own request for this car that is still waiting for its payment, under a different operation.
    ReservationDepositRequest? OwnPendingRequest = null);

public interface IReservationTransaction : IAsyncDisposable
{
    ReservationContext Context { get; }

    Task<ReservationStatus> CommitAsync(Reservation? reservation, CancellationToken cancellationToken);

    // Records the deposit request and its mail; with no request it only keeps the reconciliation done while locking.
    Task<ReservationStatus> CommitRequestAsync(ReservationDepositRequest? request, CancellationToken cancellationToken);
}

public interface IReservationDepositRequestStore
{
    // Null unless the token is well formed and signed for an existing request.
    Task<ReservationDepositRequest?> FindByTokenAsync(string token, CancellationToken cancellationToken);

    Task<ReservationDepositRequest?> FindAsync(Guid requestId, CancellationToken cancellationToken);

    // Idempotent: a request that is already confirmed stays confirmed.
    Task MarkConfirmedAsync(Guid requestId, DateTimeOffset confirmedAt, CancellationToken cancellationToken);

    // Releases the car of a request that is still pending; false when it was already confirmed, cancelled or lapsed.
    Task<bool> CancelAsync(Guid requestId, DateTimeOffset cancelledAt, CancellationToken cancellationToken);
}

public interface IReservationStore
{
    Task<IReservationTransaction> LockAsync(Guid customerId, Guid vehicleId, ReservationRequest request,
        CancellationToken cancellationToken);
}
