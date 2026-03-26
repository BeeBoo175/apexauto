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
    PromoUnavailable,
    Unavailable
}

public sealed record ReservationResult(ReservationStatus Status, Reservation? Reservation = null);

public sealed record ReservationCustomer(bool IsActive, bool EmailConfirmed, string Role, string? Email);

public sealed record ReservationContext(ReservationCustomer? Customer, CustomerProfile? Profile,
    ListingPublication? Publication, ListingRevision? Listing, PurchaseSettings? Settings, PromoCode? Promo,
    bool CustomerHasHold, bool VehicleHasHold, DateTimeOffset Now, Reservation? Existing, bool IsReleaseOperation);

public interface IReservationTransaction : IAsyncDisposable
{
    ReservationContext Context { get; }

    Task<ReservationStatus> CommitAsync(Reservation? reservation, CancellationToken cancellationToken);
}

public interface IReservationStore
{
    Task<IReservationTransaction> LockAsync(Guid customerId, Guid vehicleId, ReservationRequest request,
        CancellationToken cancellationToken);
}
