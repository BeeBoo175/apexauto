using System.Linq.Expressions;
using Dealership.Domain.Customers;

namespace Dealership.Domain.Vehicles;

// A deposit request holds the car while the deposit is paid. Staff booking for a customer ask for it by email and the
// customer has 24 hours; a customer reserving for themselves has a short payment window. Until the deposit is
// confirmed only this request holds the car; the reservation, its sale and the deposit start at confirmation.
public sealed class ReservationDepositRequest
{
    public static readonly TimeSpan Validity = TimeSpan.FromHours(24);
    public static readonly TimeSpan PaymentWindow = TimeSpan.FromMinutes(15);

    private ReservationDepositRequest()
    {
    }

    public ReservationDepositRequest(Guid id, CustomerProfile customer, ListingPublication publication,
        Guid? promoId, Guid? bookedByStaffId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(customer);
        ArgumentNullException.ThrowIfNull(publication);
        if (id == Guid.Empty || promoId == Guid.Empty || bookedByStaffId == Guid.Empty ||
            now.Offset != TimeSpan.Zero || now < publication.RecordedAt || now > DateTimeOffset.MaxValue.Add(-Validity))
        {
            throw new ArgumentException("A deposit request requires its customer, approved listing, staff member and UTC time.");
        }

        Id = id;
        CustomerId = customer.UserId;
        VehicleId = publication.VehicleId;
        BranchId = publication.BranchId;
        PublicationId = publication.Id;
        PromoId = promoId;
        BookedByStaffId = bookedByStaffId;
        CreatedAt = now;
        ExpiresAt = now.Add(bookedByStaffId is null ? PaymentWindow : Validity);
    }

    // Also the operation ID of the reservation that confirmation creates, so the two replay together.
    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid VehicleId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid PublicationId { get; private set; }
    public Guid? PromoId { get; private set; }
    // Null when the customer reserved for themselves and paid within the short window.
    public Guid? BookedByStaffId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConfirmedAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }

    // Held for the customer: not confirmed, cancelled or lapsed, so no one else may reserve the car.
    public bool IsPending(DateTimeOffset now) => ConfirmedAt is null && CancelledAt is null && now < ExpiresAt;

    // The same rule as IsPending for queries; every SQL caller composes this one expression.
    public static Expression<Func<ReservationDepositRequest, bool>> PendingAt(DateTimeOffset now) =>
        request => request.ConfirmedAt == null && request.CancelledAt == null && request.ExpiresAt > now;
}
