using Dealership.Application.Common;
using Dealership.Domain.Vehicles;

namespace Dealership.Application.Vehicles;

public sealed record ListingHistoryQuery(int Page = 1, int PageSize = Pagination.DefaultPageSize)
{
    public bool IsValid => Pagination.IsValid(Page, PageSize);
}

public sealed record ListingHistoryEntry(Guid Id, string Kind, Guid RevisionId, long Version,
    Guid ActorId, string ActorIdentity, DateTimeOffset RecordedAt, string? Reason);

public sealed record ListingHistoryPage(Guid VehicleId, Guid BranchId,
    IReadOnlyList<ListingHistoryEntry> Items, int TotalCount, int Page, int PageSize);

public sealed record ListingRevisionRead(Guid VehicleId, Guid BranchId, ListingRevision Revision,
    ListingSignedReport? SignedReport, ListingPublication? Publication, ListingWithdrawal? Withdrawal);

public sealed record ListingHistoryResult(ListingStatus Status, ListingHistoryPage? History = null);

public sealed record ListingRevisionReadResult(ListingStatus Status, ListingRevisionRead? Snapshot = null);

public interface IListingHistoryStore
{
    Task<ListingHistoryResult> ReadAsync(Guid actorId, Guid vehicleId, ListingHistoryQuery query,
        CancellationToken cancellationToken);

    Task<ListingRevisionReadResult> RevisionAsync(Guid actorId, Guid vehicleId, Guid revisionId,
        CancellationToken cancellationToken);
}
