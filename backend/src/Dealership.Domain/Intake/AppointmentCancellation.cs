namespace Dealership.Domain.Intake;

public sealed class AppointmentCancellation
{
    private AppointmentCancellation()
    {
    }

    public AppointmentCancellation(IntakeAppointment appointment, DateTimeOffset now)
    {
        if (now < appointment.CreatedAt || now >= appointment.StartsAt)
        {
            throw new InvalidOperationException("Only future appointments can be cancelled.");
        }

        AppointmentId = appointment.Id;
        OwnerId = appointment.OwnerId;
        CancelledAt = now.ToUniversalTime();
    }

    public Guid AppointmentId { get; private set; }
    public Guid OwnerId { get; private set; }
    public DateTimeOffset CancelledAt { get; private set; }
}
