namespace Dealership.Domain.Intake;

public sealed record BookingPhotoCandidate(Guid Id, Guid OwnerId, PhotoAngle Angle,
    DateTimeOffset? AvailableAt, DateTimeOffset ExpiresAt, bool AlreadyAttached, bool RemovalPending);

public sealed record BookingPhotoSelection(Guid Id, PhotoAngle Angle);

public sealed class BookingPhotoSet
{
    private BookingPhotoSet(IEnumerable<BookingPhotoCandidate> photos)
    {
        Selections = Array.AsReadOnly(photos.OrderBy(photo => photo.Angle)
            .Select(photo => new BookingPhotoSelection(photo.Id, photo.Angle)).ToArray());
    }

    public IReadOnlyList<BookingPhotoSelection> Selections { get; }

    public static bool TryCreate(Guid ownerId, IReadOnlyCollection<BookingPhotoCandidate>? photos,
        DateTimeOffset now, out BookingPhotoSet? selection)
    {
        selection = null;

        if (ownerId == Guid.Empty || photos is not { Count: 4 } ||
            photos.Any(photo => !IsEligible(photo, ownerId, now)) ||
            photos.Select(photo => photo.Id).Distinct().Count() != 4 ||
            photos.Select(photo => photo.Angle).Distinct().Count() != 4)
        {
            return false;
        }

        selection = new BookingPhotoSet(photos);
        return true;
    }

    private static bool IsEligible(BookingPhotoCandidate photo, Guid ownerId, DateTimeOffset now)
    {
        return photo.Id != Guid.Empty && photo.OwnerId == ownerId && Enum.IsDefined(photo.Angle) &&
            photo.AvailableAt is not null && photo.AvailableAt <= now && now < photo.ExpiresAt &&
            !photo.AlreadyAttached && !photo.RemovalPending;
    }
}
