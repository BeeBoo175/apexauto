using Dealership.Domain.Accounts;
using Dealership.Domain.Photos;

namespace Dealership.Application.Accounts;

public enum AccountPictureStatus
{
    Available,
    Pending,
    DeletionPending,
    Deleted,
    NotFound,
    Forbidden,
    Conflict,
    Invalid
}

public sealed record AccountPictureReceipt(Guid PictureId, string ContentType, long Bytes, DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt, DateTimeOffset? AvailableAt, string Status, string? RemovalReason,
    DateTimeOffset? RemovalRequestedAt, DateTimeOffset? DeletedAt);

public sealed record AccountPictureResult(AccountPictureStatus Status, AccountPictureReceipt? Receipt = null);

public sealed record AccountPicturePreparation(AccountPictureStatus Status, AccountPicture? Picture = null,
    AccountPictureRemoval? Removal = null);

public sealed record AccountPicturePolicy(TimeSpan PendingLifetime);

public interface IAccountPictureStore
{
    // The confirmed picture other people see; a pending upload is never current.
    Task<AccountPicture?> FindAsync(Guid userId, CancellationToken cancellationToken);

    Task<AccountPicturePreparation> FindAsync(Guid userId, Guid pictureId, CancellationToken cancellationToken);

    // Records the pending upload under the client-chosen id; a retry with the same contents returns its state.
    Task<AccountPicturePreparation> PrepareAsync(Guid userId, Guid pictureId, PhotoContent content, TimeSpan lifetime,
        CancellationToken cancellationToken);

    // Promotes the pending picture and retires the previous current one with its removal receipt in one commit.
    Task<AccountPicturePreparation> ConfirmAsync(Guid userId, Guid pictureId, CancellationToken cancellationToken);

    // Removes exactly the picture the caller saw; a replacement is never touched. An Administrator removing another
    // account's picture is recorded in the same commit as an account event.
    Task<AccountPicturePreparation> RemoveAsync(Guid userId, Guid pictureId, Guid actorId,
        CancellationToken cancellationToken);
}
