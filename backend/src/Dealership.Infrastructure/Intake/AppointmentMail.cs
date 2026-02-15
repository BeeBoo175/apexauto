using Dealership.Infrastructure.Mail;

namespace Dealership.Infrastructure.Intake;

public enum AppointmentMailKind
{
    Confirmation,
    Cancellation,
    CheckIn
}

public sealed class AppointmentMail : IMailDeliveryState
{
    private AppointmentMail()
    {
    }

    public AppointmentMail(Guid appointmentId, AppointmentMailKind kind, string recipientEmail, DateTimeOffset dueAt)
    {
        if (appointmentId == Guid.Empty || !Enum.IsDefined(kind) ||
            string.IsNullOrWhiteSpace(recipientEmail) || recipientEmail.Length > 256)
        {
            throw new ArgumentException("Appointment mail requires an appointment, supported purpose and recipient.");
        }

        Id = Guid.NewGuid();
        AppointmentId = appointmentId;
        Kind = kind;
        CheckInAppointmentId = kind == AppointmentMailKind.CheckIn ? appointmentId : null;
        RecipientEmail = recipientEmail;
        NextDeliveryAt = dueAt;
    }

    public Guid Id { get; private set; }

    public Guid AppointmentId { get; private set; }

    public AppointmentMailKind Kind { get; private set; }

    public Guid? CheckInAppointmentId { get; private set; }

    public string RecipientEmail { get; private set; } = string.Empty;

    public int DeliveryAttempts { get; set; }

    public DateTimeOffset NextDeliveryAt { get; set; }

    public Guid? DeliveryClaimId { get; set; }

    public DateTimeOffset? DeliveryClaimExpiresAt { get; set; }

    public DateTimeOffset? DeliveredAt { get; set; }
}
