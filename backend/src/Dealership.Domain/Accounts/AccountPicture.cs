using Dealership.Domain.Photos;

namespace Dealership.Domain.Accounts;

// One small square thumbnail per account. Each replacement is a new immutable object so storage never overwrites;
// replaced and removed pictures keep their row because the runtime role cannot delete.
public sealed class AccountPicture : IPhotoObject
{
    public const int ThumbnailEdge = 256;

    private AccountPicture()
    {
    }

    public AccountPicture(Guid id, Guid userId, PhotoContent content, DateTimeOffset createdAt)
    {
        if (id == Guid.Empty || userId == Guid.Empty || !content.IsValid || createdAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("An account picture requires an owner, a valid thumbnail and a UTC time.");
        }

        Id = id;
        UserId = userId;
        ObjectKey = $"accounts/pictures/{id:N}";
        ContentType = content.ContentType;
        Bytes = content.Bytes;
        Sha256 = content.Sha256;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string ObjectKey { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long Bytes { get; private set; }

    public string Sha256 { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? RemovedAt { get; private set; }

    public void MarkRemoved(DateTimeOffset now)
    {
        if (now < CreatedAt)
        {
            throw new InvalidOperationException("A picture cannot be removed before it was created.");
        }

        RemovedAt ??= now;
    }
}
