using Dealership.Application.Common;
using Dealership.Domain.Vehicles;

namespace Dealership.Application.Vehicles;

public sealed record ListingReadFilter(Guid? BranchId = null, int Page = 1,
    int PageSize = Pagination.DefaultPageSize, string Search = "")
{
    public bool IsValid => BranchId != Guid.Empty && Pagination.IsValid(Page, PageSize) && ListSearch.IsValid(Search);
}

public sealed record ListingManagerItem(Guid VehicleId, Guid BranchId, string BranchName, string RegistrationPlate,
    string Make, string Model, string Trim, int ManufactureYear, int MileageKilometres, VehicleStatus VehicleStatus,
    Guid? RevisionId, long Version, decimal? VehiclePrice, Guid? PublicationId, Guid? PublishedRevisionId,
    bool IsWithdrawn, bool IsPublic);

public sealed record ListingManagerPage(IReadOnlyList<ListingManagerItem> Items, int Total, int Page, int PageSize);

public sealed record ListingWorkspace(ListingManagerItem Vehicle, ListingRevision? Draft,
    ListingRevision? ApprovedRevision, ListingPublication? Publication, ListingWithdrawal? Withdrawal,
    Guid? CurrentSignedRevisionId);

public sealed record ListingPhotoItem(Guid Id, string ContentType, long Bytes, int Width, int Height,
    DateTimeOffset CreatedAt, DateTimeOffset? AvailableAt);

public sealed record ListingPhotoPage(IReadOnlyList<ListingPhotoItem> Items, int Total, int Page, int PageSize);

public sealed record ListingManagerPageResult(ListingStatus Status, ListingManagerPage? Listings = null);

public sealed record ListingWorkspaceResult(ListingStatus Status, ListingWorkspace? Workspace = null);

public sealed record ListingPhotoPageResult(ListingStatus Status, ListingPhotoPage? Photos = null);

public interface IListingReadStore
{
    Task<ListingManagerPageResult> ReadAsync(Guid actorId, ListingReadFilter filter, CancellationToken cancellationToken);

    Task<ListingWorkspaceResult> WorkspaceAsync(Guid actorId, Guid vehicleId, CancellationToken cancellationToken);

    Task<ListingPhotoPageResult> PhotosAsync(Guid actorId, Guid vehicleId, int page, int pageSize,
        CancellationToken cancellationToken);
}
