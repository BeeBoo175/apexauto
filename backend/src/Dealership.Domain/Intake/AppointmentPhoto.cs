namespace Dealership.Domain.Intake;

public sealed class AppointmentPhoto
{
    private AppointmentPhoto()
    {
    }

    public AppointmentPhoto(Guid appointmentId, Guid ownerId, BookingPhotoSelection photo)
    {
        if (appointmentId == Guid.Empty || ownerId == Guid.Empty || photo.Id == Guid.Empty || !Enum.IsDefined(photo.Angle))
        {
            throw new ArgumentException("An attachment requires valid appointment, owner, photo and angle.");
        }

        AppointmentId = appointmentId;
        OwnerId = ownerId;
        PhotoId = photo.Id;
        Angle = photo.Angle;
    }

    public Guid AppointmentId { get; private set; }
    public Guid OwnerId { get; private set; }
    public Guid PhotoId { get; private set; }
    public PhotoAngle Angle { get; private set; }
}
