using System.Text.Json;
using Dealership.Application.Inspections;
using Dealership.Domain.Inspections;

namespace Dealership.Infrastructure.Inspections;

public sealed class InspectionDraftSave
{
    private InspectionDraftSave()
    {
    }

    public InspectionDraftSave(Guid branchId, InspectionDraftSaveReceipt receipt)
    {
        Id = receipt.OperationId;
        InspectionId = receipt.InspectionId;
        BranchId = branchId;
        ActorId = receipt.ActorId;
        AssignmentVersion = receipt.AssignmentVersion;
        ExpectedVersion = receipt.ExpectedVersion;
        Version = receipt.Version;
        CatalogVersion = receipt.CatalogVersion;
        EntriesJson = JsonSerializer.Serialize(receipt.Entries, JsonSerializerOptions.Web);
        PhotoIdsJson = JsonSerializer.Serialize(receipt.PhotoIds);
        ExaminedPointCodesJson = receipt.ExaminedPointCodes is null ? null : JsonSerializer.Serialize(receipt.ExaminedPointCodes);
        RecordedAt = receipt.RecordedAt;
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
        var assessment = InspectionGrading.Calculate(entries);
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
        return ActorId == actorId && InspectionId == inspectionId && AssignmentVersion == request.AssignmentVersion &&
            ExpectedVersion == request.ExpectedVersion &&
            JsonSerializer.Deserialize<InspectionDraftEntry[]>(EntriesJson, JsonSerializerOptions.Web)!.SequenceEqual(entries) &&
            JsonSerializer.Deserialize<Guid[]>(PhotoIdsJson)!.SequenceEqual(request.PhotoIds!.Order()) &&
            MatchesAttestation(ExaminedPointCodesJson is null ? null : JsonSerializer.Deserialize<string[]>(ExaminedPointCodesJson),
                request.ExaminedPointCodes);
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
