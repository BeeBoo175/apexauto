using Dealership.Application.Intake;
using Dealership.Domain.Intake;

namespace Dealership.Infrastructure.Development;

public sealed class DevelopmentFixturePhotos(IntakePhotoService photos)
{
    public async Task<PhotoStatus> UploadAsync(Guid ownerId, Guid appointmentId, CancellationToken cancellationToken)
    {
        foreach (var angle in Enum.GetValues<PhotoAngle>())
        {
            using var content = Open(angle);
            var result = await photos.UploadAsync(ownerId, DevelopmentIntakeManifest.PhotoId(appointmentId, angle),
                angle, "image/jpeg", content, cancellationToken);

            if (result.Status != PhotoStatus.Available)
            {
                return result.Status;
            }
        }

        return PhotoStatus.Available;
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
