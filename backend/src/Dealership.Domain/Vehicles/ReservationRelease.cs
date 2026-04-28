namespace Dealership.Domain.Vehicles;

public enum ReservationReleaseKind
{
    CustomerCancelled,
    StaffCancelled,
    Expired,
    Withdrawn,
    Reinspection,
    // The deal built on the hold was cancelled, so the deposit is refunded and the vehicle freed.
    DealCancelled
}

public sealed class ReservationRelease
{
    private ReservationRelease()
    {
    }

    public ReservationRelease(Guid id, Reservation reservation, ReservationReleaseKind kind,
        Guid? actorId, string? reason, DateTimeOffset recordedAt)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        if (id == Guid.Empty || !Enum.IsDefined(kind) || !reservation.IsActive ||
            recordedAt.Offset != TimeSpan.Zero || recordedAt < reservation.CreatedAt ||
            recordedAt == DateTimeOffset.MaxValue)
        {
            throw new ArgumentException("Release requires an active reservation and finite current UTC timestamp.");
        }

        if (kind == ReservationReleaseKind.Expired != reservation.HasExpired(recordedAt))
        {
            throw new ArgumentException("Expiry is authoritative at the exact reservation deadline.");
        }

        ValidateActor(reservation.CustomerId, kind, actorId, reason);
        Id = id;
        ReservationId = reservation.Id;
        Kind = kind;
        ActorId = actorId;
        Reason = reason?.Trim();
        RecordedAt = recordedAt;
        Refund = reservation.Deposit;
    }

    public Guid Id { get; private set; }
    public Guid ReservationId { get; private set; }
    public ReservationReleaseKind Kind { get; private set; }
    public Guid? ActorId { get; private set; }
    public string? Reason { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }
    public decimal Refund { get; private set; }

    private static void ValidateActor(Guid customerId, ReservationReleaseKind kind, Guid? actorId, string? reason)
    {
        if (kind == ReservationReleaseKind.CustomerCancelled)
        {
            if (actorId != customerId || reason is not null)
            {
                throw new ArgumentException("Customer cancellation retains its owner and no staff reason.");
            }

            return;
        }

        if (kind is ReservationReleaseKind.StaffCancelled or ReservationReleaseKind.DealCancelled)
        {
            if (actorId is null || actorId == Guid.Empty || string.IsNullOrWhiteSpace(reason) ||
                reason.Trim().Length > 1000 || reason.Any(char.IsControl))
            {
                throw new ArgumentException("Staff cancellation requires an actor and reason of at most 1000 characters.");
            }

            return;
        }

        if (actorId is not null || reason is not null)
        {
            throw new ArgumentException("Automatic release records its cause without a customer or staff command.");
        }
    }
}
