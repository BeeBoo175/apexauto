using Dealership.Application.Inspections;

namespace Dealership.Infrastructure.Inspections;

public sealed class InspectionRepairCompletion
{
    private InspectionRepairCompletion()
    {
    }

    public InspectionRepairCompletion(InspectionRepairCompletionReceipt receipt)
    {
        Id = receipt.OperationId;
        InspectionId = receipt.InspectionId;
        BranchId = receipt.BranchId;
        ActorId = receipt.ActorId;
        RequestId = receipt.RequestId;
        ApprovalId = receipt.ApprovalId;
        AssignmentVersion = receipt.AssignmentVersion;
        Revision = receipt.Revision;
        RepairVersion = receipt.RepairVersion;
        Notes = receipt.Notes;
        CompletedAt = receipt.CompletedAt;
    }

    public Guid Id { get; private set; }
    public Guid InspectionId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid ActorId { get; private set; }
    public Guid RequestId { get; private set; }
    public Guid? ApprovalId { get; private set; }
    public long AssignmentVersion { get; private set; }
    public long Revision { get; private set; }
    public long RepairVersion { get; private set; }
    public string Notes { get; private set; } = string.Empty;
    public DateTimeOffset CompletedAt { get; private set; }

    public InspectionRepairCompletionReceipt Receipt() => new(Id, InspectionId, BranchId, ActorId, RequestId,
        ApprovalId, AssignmentVersion, Revision, RepairVersion, Notes, CompletedAt);
}
