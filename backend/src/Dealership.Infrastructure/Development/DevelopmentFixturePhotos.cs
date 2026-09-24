using Dealership.Application.Intake;
using Dealership.Application.Photos;
using Dealership.Domain.Intake;
using Dealership.Domain.Photos;

namespace Dealership.Infrastructure.Development;

public sealed class DevelopmentFixturePhotos(IPhotoImageValidator validator, IPhotoStorage storage, IntakePhotoPolicy policy)
{
    // Changed source bytes need new immutable S3 keys, including after a database-only rebuild.
    public const string MediaVersion = "wikimedia-2026-10-30";

    // The seed reuses a few dozen distinct source files, so each is decoded and validated once.
    private readonly Dictionary<string, PhotoContent> validatedByFile = [];

    public async Task<IReadOnlyList<IntakePhoto>> CreateAsync(Guid ownerId, Guid appointmentId,
        DateTimeOffset bookedAt, CancellationToken cancellationToken, DevelopmentVehicleDetails? details = null)
    {
        var photos = new List<IntakePhoto>();
        var uploads = new List<Task>();
        details ??= DevelopmentVehicleCatalogue.For("Perodua", "Myvi");
        foreach (var angle in Enum.GetValues<PhotoAngle>())
        {
            var validated = await ValidatedAsync(angle, details, cancellationToken);
            var photo = new IntakePhoto(DevelopmentIntakeManifest.PhotoId(appointmentId, angle), ownerId,
                angle, validated, bookedAt, policy.UnsubmittedLifetime);
            uploads.Add(StoreAsync(photo, angle, details, cancellationToken));
            photos.Add(photo);
        }

        await Task.WhenAll(uploads);
        foreach (var photo in photos)
        {
            photo.ConfirmAvailable(bookedAt);
        }

        return photos;
    }

    // Callers upload the photos of one object concurrently; each upload reads its own stream.
    public async Task StoreAsync(IPhotoObject photo, PhotoAngle angle, DevelopmentVehicleDetails? details,
        CancellationToken cancellationToken)
    {
        using var content = Open(angle, details);
        await storage.PutAsync(photo, content, cancellationToken);
    }

    public async Task<PhotoContent> ValidatedAsync(PhotoAngle angle, DevelopmentVehicleDetails? details,
        CancellationToken cancellationToken)
    {
        details ??= DevelopmentVehicleCatalogue.For("Perodua", "Myvi");
        var file = details.PhotoFile(angle);
        if (validatedByFile.TryGetValue(file, out var cached))
        {
            return cached;
        }

        using var content = Open(angle, details);
        var validated = await validator.ValidateAsync(content, details.ContentType(angle), cancellationToken)
            ?? throw new InvalidOperationException("The development photo must pass canonical image validation.");
        validatedByFile[file] = validated;
        return validated;
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
