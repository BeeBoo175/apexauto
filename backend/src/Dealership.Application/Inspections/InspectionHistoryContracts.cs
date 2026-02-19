namespace Dealership.Application.Inspections;

public sealed record InspectionHistoryQuery(int Page = 1, int PageSize = 25)
{
    public bool IsValid => Page > 0 && PageSize is 10 or 25 or 50;
}

public sealed record InspectionRevisionSummary(Guid OperationId, long Revision, long SaveVersion, long AssignmentVersion,
    Guid TechnicianId, string TechnicianIdentity, string CatalogVersion, InspectionDraftAssessment Assessment,
    DateTimeOffset SignedAt, InspectionReopenReceipt? Reopening);

public sealed record InspectionRevisionHistory(Guid InspectionId, long CurrentRevision, bool IsSigned,
    IReadOnlyList<InspectionRevisionSummary> Items, int TotalCount, int Page, int PageSize);

public sealed record InspectionRepairHistoryEntry(InspectionRepairReceipt Request, InspectionRepairApprovalReceipt? Approval);

public sealed record InspectionRepairHistory(Guid InspectionId, long Revision,
    IReadOnlyList<InspectionRepairHistoryEntry> Items, int TotalCount, int Page, int PageSize);

public sealed record InspectionRevisionHistoryResult(InspectionAssignmentStatus Status, InspectionRevisionHistory? History = null);

public sealed record InspectionRepairHistoryResult(InspectionAssignmentStatus Status, InspectionRepairHistory? History = null);

public interface IInspectionHistoryStore
{
    Task<InspectionRevisionHistoryResult> RevisionsAsync(Guid actorId, Guid inspectionId, InspectionHistoryQuery query,
        CancellationToken cancellationToken);

    Task<InspectionRepairHistoryResult> RepairsAsync(Guid actorId, Guid inspectionId, long revision, InspectionHistoryQuery query,
        CancellationToken cancellationToken);
}
