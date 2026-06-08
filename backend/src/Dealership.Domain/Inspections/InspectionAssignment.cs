using Dealership.Domain.Common;

namespace Dealership.Domain.Inspections;

public sealed class InspectionAssignment
{
    private InspectionAssignment()
    {
    }

    public InspectionAssignment(Guid operationId, InspectionDraft draft, Guid actorId,
        Guid? previousTechnicianId, long expectedVersion, string? reason, DateTimeOffset recordedAt)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (operationId == Guid.Empty || actorId == Guid.Empty || expectedVersion < 0 ||
            expectedVersion == long.MaxValue || draft.AssignmentVersion != expectedVersion + 1)
        {
            throw new ArgumentException("Assignment requires valid identities and a consecutive version.");
        }

        reason = NormalizeReason(reason);
        var claim = expectedVersion == 0;
        if (claim && (previousTechnicianId is not null || reason is not null || actorId != draft.TechnicianId) ||
            !claim && (previousTechnicianId is null || previousTechnicianId == Guid.Empty ||
                previousTechnicianId == draft.TechnicianId || reason is null))
        {
            throw new ArgumentException("Claim or reassignment evidence does not match its lifecycle.");
        }

        Id = operationId;
        InspectionId = draft.Id;
        BranchId = draft.BranchId;
        ActorId = actorId;
        TechnicianId = draft.TechnicianId;
        PreviousTechnicianId = previousTechnicianId;
        ExpectedVersion = expectedVersion;
        Version = draft.AssignmentVersion;
        Reason = reason;
        RecordedAt = recordedAt;
    }

    public Guid Id { get; private set; }
    public Guid InspectionId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid ActorId { get; private set; }
    public Guid TechnicianId { get; private set; }
    public Guid? PreviousTechnicianId { get; private set; }
    public long ExpectedVersion { get; private set; }
    public long Version { get; private set; }
    public string? Reason { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }

    public static string? NormalizeReason(string? reason)
    {
        return BoundedText.NormalizeOptional(reason,
            "Reassignment reason must contain up to 1000 non-control characters.");
    }
}
