using Dealership.Application.Inspections;

namespace Dealership.Infrastructure.Inspections;

public sealed class InspectionReopening
{
    private InspectionReopening()
    {
    }

    public InspectionReopening(InspectionReopenReceipt receipt)
    {
        Id = receipt.OperationId;
        InspectionId = receipt.InspectionId;
        BranchId = receipt.BranchId;
        ActorId = receipt.ActorId;
        SignedRevisionId = receipt.SignedRevisionId;
        AssignmentVersion = receipt.AssignmentVersion;
        ExpectedRevision = receipt.ExpectedRevision;
        Revision = receipt.Revision;
        AfterSaveVersion = receipt.AfterSaveVersion;
        Reason = receipt.Reason;
        RecordedAt = receipt.RecordedAt;
    }

    public Guid Id { get; private set; }
    public Guid InspectionId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid ActorId { get; private set; }
    public Guid SignedRevisionId { get; private set; }
    public long AssignmentVersion { get; private set; }
    public long ExpectedRevision { get; private set; }
    public long Revision { get; private set; }
    public long AfterSaveVersion { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; private set; }

    public bool Matches(Guid actorId, Guid inspectionId, InspectionReopenRequest request) =>
        ActorId == actorId && InspectionId == inspectionId && AssignmentVersion == request.AssignmentVersion &&
        ExpectedRevision == request.Revision && Reason == request.Reason;

    public InspectionReopenReceipt Receipt() => new(Id, InspectionId, BranchId, ActorId, SignedRevisionId,
        AssignmentVersion, ExpectedRevision, Revision, AfterSaveVersion, Reason, RecordedAt);
}
