using Dealership.Application.Intake;
using Dealership.Domain.Intake;

namespace Dealership.Infrastructure.Development;

public sealed class DevelopmentFixturePhotos(IPhotoImageValidator validator, IPhotoStorage storage, IntakePhotoPolicy policy)
{
    public async Task<IReadOnlyList<IntakePhoto>> CreateAsync(Guid ownerId, Guid appointmentId,
        DateTimeOffset bookedAt, CancellationToken cancellationToken)
    {
        var photos = new List<IntakePhoto>();
        foreach (var angle in Enum.GetValues<PhotoAngle>())
        {
            using var content = Open(angle);
            var validated = await validator.ValidateAsync(content, "image/jpeg", cancellationToken)
                ?? throw new InvalidOperationException("The development photo must pass canonical image validation.");
            var photo = new IntakePhoto(DevelopmentIntakeManifest.PhotoId(appointmentId, angle), ownerId,
                angle, validated, bookedAt, policy.UnsubmittedLifetime);
            content.Position = 0;
            await storage.PutAsync(photo, content, cancellationToken);
            photo.ConfirmAvailable(bookedAt);
            photos.Add(photo);
        }

        return photos;
    }

    public static Stream Open(PhotoAngle angle)
    {
        if (!Enum.IsDefined(angle))
        {
            throw new ArgumentOutOfRangeException(nameof(angle));
        }

        var name = $"Dealership.Infrastructure.Development.Media.myvi-{angle.ToString().ToLowerInvariant()}.jpg";
        return typeof(DevelopmentFixturePhotos).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException("The Wikimedia development photo resource is missing.");
    }
}
