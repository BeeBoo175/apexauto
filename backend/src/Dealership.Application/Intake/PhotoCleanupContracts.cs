namespace Dealership.Application.Intake;

public interface IPhotoTransferLock
{
    Task<IAsyncDisposable> AcquireAsync(Guid photoId, CancellationToken cancellationToken);

    Task<IAsyncDisposable?> TryAcquireAsync(Guid photoId, CancellationToken cancellationToken);
}
