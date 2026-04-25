using Dealership.Application.Photos;
using Dealership.Application.Intake;
using Dealership.Domain.Intake;

namespace Dealership.Infrastructure.Development;

public sealed class DevelopmentFixturePhotos(IPhotoImageValidator validator, IPhotoStorage storage, IntakePhotoPolicy policy)
{
    // Changed source bytes need new immutable S3 keys, including after a database-only rebuild.
    public const string MediaVersion = "wikimedia-2026-10-30";

    public async Task<IReadOnlyList<IntakePhoto>> CreateAsync(Guid ownerId, Guid appointmentId,
        DateTimeOffset bookedAt, CancellationToken cancellationToken, DevelopmentVehicleDetails? details = null)
    {
        var photos = new List<IntakePhoto>();
        details ??= DevelopmentVehicleCatalogue.For("Perodua", "Myvi");
        foreach (var angle in Enum.GetValues<PhotoAngle>())
        {
            using var content = Open(angle, details);
            var validated = await validator.ValidateAsync(content, details.ContentType(angle), cancellationToken)
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

    public static Stream Open(PhotoAngle angle, DevelopmentVehicleDetails? details = null)
    {
        if (!Enum.IsDefined(angle))
        {
            throw new ArgumentOutOfRangeException(nameof(angle));
        }

        details ??= DevelopmentVehicleCatalogue.For("Perodua", "Myvi");
        var name = $"Dealership.Infrastructure.Development.Media.{details.PhotoFile(angle)}";
        return typeof(DevelopmentFixturePhotos).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException("The Wikimedia development photo resource is missing.");
    }
}
