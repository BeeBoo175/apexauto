using System.Text.Json.Serialization;
using Dealership.Application.Common;
using Dealership.Domain.Vehicles;

namespace Dealership.Application.Vehicles;

public sealed record ListingDraftRequest(Guid OperationId, long ExpectedVersion,
    [property: JsonConverter(typeof(MyrAmountJsonConverter))] decimal VehiclePrice,
    [property: JsonConverter(typeof(MyrAmountJsonConverter))] decimal RoadTax,
    VehicleTransmission Transmission, VehicleFuel Fuel, string? Description)
{
    public IReadOnlyList<Guid> PhotoIds { get; init; } = [];
}

public sealed record ListingPublishRequest(Guid OperationId, Guid RevisionId, Guid SignedRevisionId);

public sealed record ListingWithdrawRequest(Guid OperationId, Guid PublicationId, string? Reason);

public enum ListingStatus
{
    Succeeded,
    Invalid,
    Forbidden,
    NotFound,
    Conflict,
    Unavailable
}

public sealed record ListingResult(ListingStatus Status, ListingRevision? Draft = null,
    ListingPublication? Publication = null, ListingWithdrawal? Withdrawal = null);

public sealed record ListingManager(bool IsActive, string Role, Guid? BranchId);

public sealed record ListingContext(ListingManager? Actor, bool BranchActive, Vehicle? Vehicle,
    ListingRevision? LatestRevision, ListingPublication? LatestPublication, bool IsWithdrawn,
    Guid? SignedRevisionId, DateTimeOffset Now, ListingRevision? ExistingDraft,
    ListingPublication? ExistingPublication, ListingWithdrawal? ExistingWithdrawal,
    IReadOnlyList<Guid> AvailablePhotoIds);

public interface IListingTransaction : IAsyncDisposable
{
    ListingContext Context { get; }

    Task CompleteAsync(CancellationToken cancellationToken);

    Task<ListingStatus> SaveAsync(ListingRevision revision, IReadOnlyList<Guid> photoIds, CancellationToken cancellationToken);

    Task<ListingStatus> PublishAsync(ListingPublication publication, CancellationToken cancellationToken);

    Task<ListingStatus> WithdrawAsync(ListingWithdrawal withdrawal, CancellationToken cancellationToken);
}

public interface IListingStore
{
    Task<IListingTransaction> LockAsync(Guid actorId, Guid vehicleId, Guid operationId,
        CancellationToken cancellationToken, IReadOnlyList<Guid>? photoIds = null);
}
