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
        PhotoIdsJson = JsonSerializer.Serialize(request.PhotoIds!.Order().ToArray());
        ExaminedPointCodesJson = request.ExaminedPointCodes is null ? null :
            JsonSerializer.Serialize(InspectionChecklist.Attest(entries, request.ExaminedPointCodes));
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
    public string PhotoIdsJson { get; private set; } = "[]";
    public string? ExaminedPointCodesJson { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }

    public InspectionDraftSaveReceipt Receipt()
    {
        var entries = JsonSerializer.Deserialize<InspectionDraftEntry[]>(EntriesJson, JsonSerializerOptions.Web)!;
        var assessment = InspectionDraftEntry.Assess(entries);
        return new(Id, InspectionId, ActorId, AssignmentVersion, ExpectedVersion, Version, CatalogVersion,
            entries, new(assessment.EstimatedRepairCost, assessment.Grade, assessment.IsRejected), RecordedAt)
        {
            PhotoIds = JsonSerializer.Deserialize<Guid[]>(PhotoIdsJson)!,
            ExaminedPointCodes = ExaminedPointCodesJson is null ? null : JsonSerializer.Deserialize<string[]>(ExaminedPointCodesJson)!
        };
    }

    public bool Matches(Guid actorId, Guid inspectionId, InspectionDraftSaveRequest request,
        IReadOnlyList<InspectionDraftEntry> entries)
    {
        var receipt = Receipt();
        return ActorId == actorId && InspectionId == inspectionId && AssignmentVersion == request.AssignmentVersion &&
            ExpectedVersion == request.ExpectedVersion && receipt.Entries.SequenceEqual(entries) &&
            receipt.PhotoIds.SequenceEqual(request.PhotoIds!.Order()) &&
            MatchesAttestation(receipt.ExaminedPointCodes, request.ExaminedPointCodes);
    }

    private static bool MatchesAttestation(IReadOnlyList<string>? saved, IReadOnlyList<string>? requested)
    {
        if (saved is null || requested is null)
        {
            return saved is null && requested is null;
        }

        return saved.SequenceEqual(requested.Order(StringComparer.Ordinal));
    }
}
