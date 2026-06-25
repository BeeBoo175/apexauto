using Dealership.Domain.Photos;

namespace Dealership.Application.Photos;

public interface IPhotoImageValidator
{
    Task<PhotoContent?> ValidateAsync(Stream content, string contentType, CancellationToken cancellationToken);
}

public interface IPhotoStorage
{
    Task PutAsync(IPhotoObject photo, Stream content, CancellationToken cancellationToken);

    Task CopyToAsync(IPhotoObject photo, Stream destination, CancellationToken cancellationToken);

    Task DeleteAsync(IPhotoObject photo, CancellationToken cancellationToken);
}

public sealed class PhotoStorageException(Exception? innerException = null)
    : Exception("Photo storage is temporarily unavailable.", innerException);

public interface IPhotoThumbnailer
{
    // Writes a square JPEG centred on the source and returns its description, or null when the source cannot be decoded.
    Task<PhotoContent?> CreateAsync(Stream source, Stream destination, int edge, CancellationToken cancellationToken);
}
