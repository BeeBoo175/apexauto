using System.Text.Json;
using Dealership.Application.Inspections;
using Dealership.Domain.Inspections;

namespace Dealership.Infrastructure.Inspections;

public sealed class InspectionRepairRequestRecord
{
    private InspectionRepairRequestRecord()
    {
    }

    public InspectionRepairRequestRecord(InspectionRepairReceipt receipt)
    {
        Id = receipt.OperationId;
        InspectionId = receipt.InspectionId;
        BranchId = receipt.BranchId;
        ActorId = receipt.ActorId;
        SignedRevisionId = receipt.SignedRevisionId;
        AssignmentVersion = receipt.AssignmentVersion;
        Revision = receipt.Revision;
        ExpectedVersion = receipt.ExpectedVersion;
        Version = receipt.Version;
        ItemsJson = JsonSerializer.Serialize(receipt.Items, JsonSerializerOptions.Web);
        Budget = receipt.Budget;
        RequestedAt = receipt.RequestedAt;
    }

    public Guid Id { get; private set; }
    public Guid InspectionId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid ActorId { get; private set; }
    public Guid SignedRevisionId { get; private set; }
    public long AssignmentVersion { get; private set; }
    public long Revision { get; private set; }
    public long ExpectedVersion { get; private set; }
    public long Version { get; private set; }
    public string ItemsJson { get; private set; } = string.Empty;
    public decimal Budget { get; private set; }
    public DateTimeOffset RequestedAt { get; private set; }

    public InspectionRepairReceipt Receipt() => new(Id, InspectionId, BranchId, ActorId, SignedRevisionId,
        AssignmentVersion, Revision, ExpectedVersion, Version,
        Array.AsReadOnly(JsonSerializer.Deserialize<InspectionRepairItem[]>(ItemsJson, JsonSerializerOptions.Web)!), Budget, RequestedAt);
}
