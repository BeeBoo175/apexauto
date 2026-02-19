using Dealership.Application.Inspections;

namespace Dealership.Infrastructure.Inspections;

public sealed class InspectionRepairApproval
{
    private InspectionRepairApproval()
    {
    }

    public InspectionRepairApproval(InspectionRepairApprovalReceipt receipt)
    {
        Id = receipt.OperationId;
        InspectionId = receipt.InspectionId;
        BranchId = receipt.BranchId;
        ActorId = receipt.ActorId;
        RequestId = receipt.RequestId;
        AssignmentVersion = receipt.AssignmentVersion;
        Revision = receipt.Revision;
        RepairVersion = receipt.RepairVersion;
        ApprovedAt = receipt.ApprovedAt;
    }

    public Guid Id { get; private set; }
    public Guid InspectionId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid ActorId { get; private set; }
    public Guid RequestId { get; private set; }
    public long AssignmentVersion { get; private set; }
    public long Revision { get; private set; }
    public long RepairVersion { get; private set; }
    public DateTimeOffset ApprovedAt { get; private set; }

    public bool Matches(Guid actorId, Guid inspectionId, InspectionRepairApprovalRequest request) =>
        ActorId == actorId && InspectionId == inspectionId && RequestId == request.RequestId &&
        AssignmentVersion == request.AssignmentVersion && Revision == request.Revision && RepairVersion == request.RepairVersion;

    public InspectionRepairApprovalReceipt Receipt() => new(Id, InspectionId, BranchId, ActorId, RequestId,
        AssignmentVersion, Revision, RepairVersion, ApprovedAt);
}
