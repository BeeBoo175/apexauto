using System.Text.Json;
using Dealership.Application.Inspections;
using Dealership.Domain.Inspections;

namespace Dealership.Infrastructure.Inspections;

public sealed class InspectionDraftSave
{
    private InspectionDraftSave()
    {
    }

    public InspectionDraftSave(Guid inspectionId, Guid branchId, Guid actorId, InspectionDraftSaveRequest request,
        IReadOnlyList<InspectionDraftEntry> entries, DateTimeOffset recordedAt)
    {
        Id = request.OperationId;
        InspectionId = inspectionId;
        BranchId = branchId;
        ActorId = actorId;
        AssignmentVersion = request.AssignmentVersion;
        ExpectedVersion = request.ExpectedVersion;
        Version = request.ExpectedVersion + 1;
        CatalogVersion = InspectionCatalog.Version;
        EntriesJson = JsonSerializer.Serialize(entries, JsonSerializerOptions.Web);
        RecordedAt = recordedAt;
    }

    public Guid Id { get; private set; }
    public Guid InspectionId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid ActorId { get; private set; }
    public long AssignmentVersion { get; private set; }
    public long ExpectedVersion { get; private set; }
    public long Version { get; private set; }
    public string CatalogVersion { get; private set; } = string.Empty;
    public string EntriesJson { get; private set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; private set; }

    public InspectionDraftSaveReceipt Receipt()
    {
        var entries = JsonSerializer.Deserialize<InspectionDraftEntry[]>(EntriesJson, JsonSerializerOptions.Web)!;
        var assessment = InspectionDraftEntry.Assess(entries);
        return new(Id, InspectionId, ActorId, AssignmentVersion, ExpectedVersion, Version, CatalogVersion,
            entries, new(assessment.EstimatedRepairCost, assessment.Grade, assessment.IsRejected), RecordedAt);
    }

    public bool Matches(Guid actorId, Guid inspectionId, InspectionDraftSaveRequest request,
        IReadOnlyList<InspectionDraftEntry> entries)
    {
        return ActorId == actorId && InspectionId == inspectionId && AssignmentVersion == request.AssignmentVersion &&
            ExpectedVersion == request.ExpectedVersion && Receipt().Entries.SequenceEqual(entries);
    }
}
