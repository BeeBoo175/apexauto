using System.Linq.Expressions;
using Dealership.Domain.Photos;

namespace Dealership.Domain.Accounts;

// One small square thumbnail per account. Each replacement is a new immutable object so storage never overwrites;
// replaced and removed pictures keep their row because the runtime role cannot delete. An upload stays pending,
// and the previous picture stays current, until the new object is confirmed.
public sealed class AccountPicture : IPhotoObject
{
    public const int ThumbnailEdge = 256;

    // The one picture other people see: confirmed and neither replaced nor removed.
    public static readonly Expression<Func<AccountPicture, bool>> Current =
        picture => picture.RemovedAt == null && picture.AvailableAt != null;

    private AccountPicture()
    {
    }

    public AccountPicture(Guid id, Guid userId, PhotoContent content, DateTimeOffset createdAt, TimeSpan lifetime)
    {
        if (id == Guid.Empty || userId == Guid.Empty || !content.IsValid || createdAt.Offset != TimeSpan.Zero ||
            lifetime <= TimeSpan.Zero)
        {
            throw new ArgumentException("An account picture requires an owner, a valid thumbnail, a UTC time and a lifetime.");
        }

        Id = id;
        UserId = userId;
        ObjectKey = $"accounts/pictures/{id:N}";
        ContentType = content.ContentType;
        Bytes = content.Bytes;
        Sha256 = content.Sha256;
        CreatedAt = createdAt;
        ExpiresAt = createdAt + lifetime;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string ObjectKey { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long Bytes { get; private set; }

    public string Sha256 { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? AvailableAt { get; private set; }

    public DateTimeOffset? RemovedAt { get; private set; }

    public bool Matches(Guid userId, PhotoContent content)
    {
        return UserId == userId && ContentType == content.ContentType && Bytes == content.Bytes &&
            Sha256 == content.Sha256;
    }

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    public void ConfirmAvailable(DateTimeOffset now)
    {
        if (now < CreatedAt)
        {
            throw new InvalidOperationException("A picture cannot be confirmed before it was created.");
        }

        AvailableAt ??= now;
    }

    public void MarkRemoved(DateTimeOffset now)
    {
        if (AvailableAt is null || now < AvailableAt)
        {
            throw new InvalidOperationException("Only a confirmed picture can be removed.");
        }

        RemovedAt ??= now;
    }
}
