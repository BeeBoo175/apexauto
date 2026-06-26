using Dealership.Domain.Accounts;

namespace Dealership.Application.Accounts;

public enum AccountPictureStatus
{
    Stored,
    Available,
    Removed,
    NotFound,
    Forbidden,
    Invalid
}

public sealed record AccountPictureResult(AccountPictureStatus Status, Guid? PictureId = null, string? ContentType = null);

public interface IAccountPictureStore
{
    Task<AccountPicture?> FindAsync(Guid userId, CancellationToken cancellationToken);

    // Stores the picture as the account's only one and returns the one it replaced.
    Task<AccountPicture?> ReplaceAsync(AccountPicture picture, CancellationToken cancellationToken);

    // Marks the current picture removed; an Administrator removing another account's picture is recorded in the
    // same commit as an account event.
    Task<AccountPicture?> RemoveAsync(Guid userId, Guid actorId, CancellationToken cancellationToken);
}
