using Dealership.Domain.Photos;

namespace Dealership.Application.Photos;

public sealed class PhotoTransferService(IPhotoStorage storage, IPhotoTransferLock transfers)
{
    public async Task<T> UploadAsync<T>(Guid id, Stream content,
        Func<CancellationToken, Task<T>> prepare, Func<T, IPhotoObject?> pendingPhoto,
        Func<CancellationToken, Task<T>> confirm, CancellationToken cancellationToken)
    {
        await using var transfer = await transfers.AcquireAsync(id, cancellationToken);
        var prepared = await prepare(cancellationToken);
        var photo = pendingPhoto(prepared);
        if (photo is null)
        {
            return prepared;
        }

        content.Position = 0;
        await storage.PutAsync(photo, content, cancellationToken);
        return await confirm(cancellationToken);
    }

    public async Task<T> DownloadAsync<T>(Guid id, Stream destination,
        Func<CancellationToken, Task<T>> find, Func<T, IPhotoObject?> availablePhoto,
        CancellationToken cancellationToken)
    {
        await using var transfer = await transfers.AcquireAsync(id, cancellationToken);
        var found = await find(cancellationToken);
        var photo = availablePhoto(found);
        if (photo is null)
        {
            return found;
        }

        await storage.CopyToAsync(photo, destination, cancellationToken);
        // Eligibility may change during provider I/O; callers release bytes only after this recheck.
        return await find(cancellationToken);
    }
}
