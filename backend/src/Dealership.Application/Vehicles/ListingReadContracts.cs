using Dealership.Application.Common;
using Dealership.Domain.Vehicles;

namespace Dealership.Application.Vehicles;

public sealed record ListingReadFilter(Guid? BranchId = null, int Page = 1,
    int PageSize = Pagination.DefaultPageSize, string Search = "", string Sort = "", string Direction = "asc",
    string Inspection = "", string Publication = "", decimal? MinimumPrice = null, decimal? MaximumPrice = null,
    decimal? MinimumPublishedPrice = null, decimal? MaximumPublishedPrice = null,
    DateTimeOffset? ActivityFrom = null, DateTimeOffset? ActivityUntil = null,
    string Registration = "", string Vehicle = "")
{
    public bool IsValid => BranchId != Guid.Empty && Pagination.IsValid(Page, PageSize) && ListSearch.IsValid(Search) &&
        Sort is "" or "registration" or "vehicle" or "branch" or "inspection" or "price" or "publication" or "publishedPrice" or "activity" &&
        Direction is "asc" or "desc" &&
        Inspection is "" or "PendingInspection" or "Rejected" or "Available" &&
        Publication is "" or "unprepared" or "draft" or "published" or "changes" or "unavailable" or "withdrawn" &&
        MoneyRange.IsValid(MinimumPrice, MaximumPrice) && MoneyRange.IsValid(MinimumPublishedPrice, MaximumPublishedPrice) &&
        ListDateRange.IsValid(ActivityFrom, ActivityUntil) && ListSearch.IsValid(Registration) && ListSearch.IsValid(Vehicle);
}

public sealed record ListingManagerItem(Guid VehicleId, Guid BranchId, string BranchName, string RegistrationPlate,
    string Make, string Model, string Trim, int ManufactureYear, int MileageKilometres, VehicleStatus VehicleStatus,
    Guid? RevisionId, long Version, decimal? VehiclePrice, Guid? PublicationId, Guid? PublishedRevisionId,
    bool IsWithdrawn, bool IsPublic, decimal? ApprovedVehiclePrice = null,
    DateTimeOffset? SavedAt = null, DateTimeOffset? ApprovedAt = null, DateTimeOffset? WithdrawnAt = null,
    bool IsReserved = false, bool HasCurrentPublicationInspection = false);

// Publication tab totals for the manager's branch scope, independent of search and column filters.
public sealed record ListingCounts(int All, int Unprepared, int Draft, int Published, int Changes, int Unavailable,
    int Withdrawn);

public sealed record ListingManagerPage(IReadOnlyList<ListingManagerItem> Items, int Total, int Page, int PageSize,
    ListingCounts? Counts = null);

public sealed record ListingSignedReport(Guid InspectionId, long Revision);

// Who has the car on hold: the buyer, and the agent handling it once one has claimed the hold.
public sealed record ListingHold(string Buyer, string? Agent, DateTimeOffset ExpiresAtUtc);

public sealed record ListingWorkspace(ListingManagerItem Vehicle, ListingRevision? Draft,
    ListingRevision? ApprovedRevision, ListingPublication? Publication, ListingWithdrawal? Withdrawal,
    Guid? CurrentSignedRevisionId, ListingSignedReport? CurrentSignedReport, string Vin = "", ListingHold? Hold = null);

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
