using Dealership.Domain.Vehicles;

namespace Dealership.Domain.Inspections;

public sealed class InspectionDraft
{
    private InspectionDraft()
    {
    }

    public InspectionDraft(Guid id, Vehicle vehicle, Guid technicianId, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(vehicle);
        if (id == Guid.Empty || technicianId == Guid.Empty || vehicle.Status != VehicleStatus.PendingInspection)
        {
            throw new ArgumentException("Inspection claim requires a pending vehicle and nonempty identities.");
        }

        Id = id;
        VehicleId = vehicle.Id;
        BranchId = vehicle.BranchId;
        TechnicianId = technicianId;
        CreatedAt = createdAt;
        AssignmentVersion = 1;
    }

    public Guid Id { get; private set; }
    public Guid VehicleId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid TechnicianId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public long AssignmentVersion { get; private set; }
    public long Revision { get; private set; } = 1;
    public bool IsSigned { get; private set; }

    public void Reassign(Guid technicianId, long expectedVersion)
    {
        if (IsSigned || technicianId == Guid.Empty || technicianId == TechnicianId || expectedVersion != AssignmentVersion ||
            AssignmentVersion == long.MaxValue)
        {
            throw new ArgumentException("Reassignment requires a different technician and the current assignment version.");
        }

        TechnicianId = technicianId;
        AssignmentVersion++;
    }
}

public enum InspectionEntryKind
{
    Finding,
    NotFitted
}

public sealed record InspectionDraftEntry
{
    public InspectionDraftEntry(string pointCode, InspectionEntryKind kind, InspectionSeverity? severity,
        decimal estimatedRepairCost, string notes)
    {
        var point = InspectionCatalog.GetPoint(pointCode);
        if (!Enum.IsDefined(kind) || string.IsNullOrWhiteSpace(notes) || notes.Length > 1000 || notes.Any(char.IsControl))
        {
            throw new ArgumentException("Each exception requires a valid disposition and a reason of at most 1000 characters.");
        }

        if (kind == InspectionEntryKind.NotFitted && (severity is not null || estimatedRepairCost != 0 ||
            !point.Description.Contains("where fitted", StringComparison.Ordinal)))
        {
            throw new ArgumentException("Not fitted requires an optional catalog item without severity or repair cost.");
        }

        if (kind == InspectionEntryKind.Finding)
        {
            if (severity is null)
            {
                throw new ArgumentException("A finding requires severity.");
            }

            if (!Enum.IsDefined(severity.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(severity));
            }

            if (estimatedRepairCost < 0 || decimal.Round(estimatedRepairCost, 2) != estimatedRepairCost)
            {
                throw new ArgumentOutOfRangeException(nameof(estimatedRepairCost),
                    "Repair estimates require nonnegative MYR minor-unit precision.");
            }
        }

        PointCode = pointCode;
        Kind = kind;
        Severity = severity;
        EstimatedRepairCost = estimatedRepairCost;
        Notes = notes.Trim();
    }

    public string PointCode { get; }
    public InspectionEntryKind Kind { get; }
    public InspectionSeverity? Severity { get; }
    public decimal EstimatedRepairCost { get; }
    public string Notes { get; }

    internal static void ValidateSet(IReadOnlyCollection<InspectionDraftEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Any(entry => entry is null))
        {
            throw new ArgumentNullException(nameof(entries), "Inspection entries cannot contain null values.");
        }

        if (entries.Count > InspectionCatalog.Points.Count ||
            entries.Select(entry => entry.PointCode).Distinct(StringComparer.Ordinal).Count() != entries.Count)
        {
            throw new ArgumentException("Use at most one exception for each catalog point.");
        }
    }
}

public static class InspectionChecklist
{
    public static IReadOnlyList<string> Attest(IReadOnlyCollection<InspectionDraftEntry> entries,
        IReadOnlyCollection<string> examinedPointCodes)
    {
        ArgumentNullException.ThrowIfNull(examinedPointCodes);
        InspectionDraftEntry.ValidateSet(entries);
        if (examinedPointCodes.Count > InspectionCatalog.Points.Count)
        {
            throw new ArgumentException("Attestation cannot exceed the catalog.", nameof(examinedPointCodes));
        }

        var examined = examinedPointCodes.ToHashSet(StringComparer.Ordinal);
        var notFitted = entries.Where(entry => entry.Kind == InspectionEntryKind.NotFitted)
            .Select(entry => entry.PointCode).ToHashSet(StringComparer.Ordinal);
        var required = InspectionCatalog.Points.Where(point => !notFitted.Contains(point.Code))
            .Select(point => point.Code).ToHashSet(StringComparer.Ordinal);
        if (examined.Count != examinedPointCodes.Count || !examined.SetEquals(required))
        {
            throw new ArgumentException("Examine every fitted catalog point exactly once; retain reasons for Not fitted exceptions.",
                nameof(examinedPointCodes));
        }

        return Array.AsReadOnly(examined.Order(StringComparer.Ordinal).ToArray());
    }
}
