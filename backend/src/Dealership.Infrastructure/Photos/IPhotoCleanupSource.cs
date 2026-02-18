using Dealership.Domain.Photos;

namespace Dealership.Infrastructure.Photos;

public sealed record PhotoDeletionClaim(IPhotoObject Photo, Guid ClaimId);

public interface IPhotoCleanupSource
{
    Task<IReadOnlyList<Guid>> GetCandidatesAsync(CancellationToken cancellationToken);

    Task<PhotoDeletionClaim?> ClaimAsync(Guid id, CancellationToken cancellationToken);

    Task MarkDeletedAsync(Guid photoId, Guid claimId, CancellationToken cancellationToken);
}
