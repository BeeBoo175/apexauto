namespace Dealership.Domain.Inspections;

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
        InspectionCatalog.GetPoint(pointCode);
        if (!Enum.IsDefined(kind) || string.IsNullOrWhiteSpace(notes) || notes.Length > 1000 || notes.Any(char.IsControl))
        {
            throw new ArgumentException("Each exception requires a valid disposition and a reason of at most 1000 characters.");
        }

        if (kind == InspectionEntryKind.NotFitted && (severity is not null || estimatedRepairCost != 0 ||
            !InspectionCatalog.GetPoint(pointCode).Description.Contains("where fitted", StringComparison.Ordinal)))
        {
            throw new ArgumentException("Not fitted requires an optional catalog item without severity or repair cost.");
        }

        if (kind == InspectionEntryKind.Finding)
        {
            _ = new InspectionFinding(pointCode, severity ?? throw new ArgumentException("A finding requires severity."),
                estimatedRepairCost);
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

    public static InspectionAssessment Assess(IReadOnlyCollection<InspectionDraftEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Count > InspectionCatalog.Points.Count || entries.Any(entry => entry is null) ||
            entries.Select(entry => entry.PointCode).Distinct(StringComparer.Ordinal).Count() != entries.Count)
        {
            throw new ArgumentException("Use at most one exception for each catalog point.");
        }

        var findings = entries.Where(entry => entry.Kind == InspectionEntryKind.Finding)
            .Select(entry => new InspectionFinding(entry.PointCode, entry.Severity!.Value, entry.EstimatedRepairCost)).ToArray();
        return InspectionGrading.Calculate(findings);
    }
}
