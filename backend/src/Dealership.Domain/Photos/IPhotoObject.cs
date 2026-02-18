namespace Dealership.Domain.Photos;

public interface IPhotoObject
{
    string ObjectKey { get; }
    string ContentType { get; }
    long Bytes { get; }
    string Sha256 { get; }
}
