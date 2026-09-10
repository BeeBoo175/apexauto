namespace Dealership.Infrastructure.Photos;

// Technical limits shared by the account picture and listing upload cleanup; the existing IntakePhotos
// CleanupEnabled flag still controls whether the worker runs at all.
public sealed class PhotoCleanupSettings
{
    public const int DefaultBatchSize = 10;

    public double PendingUploadLifetimeHours { get; set; } = 24;
    public int BatchSize { get; set; } = DefaultBatchSize;
}
