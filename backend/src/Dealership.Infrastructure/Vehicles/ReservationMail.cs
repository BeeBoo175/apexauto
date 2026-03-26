using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Mail;

namespace Dealership.Infrastructure.Vehicles;

public sealed class ReservationMail : IMailDeliveryState
{
    private ReservationMail()
    {
    }

    public ReservationMail(Reservation reservation)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        Id = Guid.NewGuid();
        ReservationId = reservation.Id;
        NextDeliveryAt = reservation.CreatedAt;
    }

    public Guid Id { get; private set; }

    public Guid ReservationId { get; private set; }

    public int DeliveryAttempts { get; set; }

    public DateTimeOffset NextDeliveryAt { get; set; }

    public Guid? DeliveryClaimId { get; set; }

    public DateTimeOffset? DeliveryClaimExpiresAt { get; set; }

    public DateTimeOffset? DeliveredAt { get; set; }
}
