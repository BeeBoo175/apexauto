namespace Dealership.Domain.Inspections;

public enum InspectionSeverity
{
    Advisory,
    MinorDefect,
    MajorDefect,
    CriticalDisqualifier
}

public sealed record InspectionFinding
{
    public InspectionFinding(string pointCode, InspectionSeverity severity, decimal estimatedRepairCost)
    {
        InspectionCatalog.GetPoint(pointCode);

        if (!Enum.IsDefined(severity))
        {
            throw new ArgumentOutOfRangeException(nameof(severity));
        }

        if (estimatedRepairCost < 0 || decimal.Round(estimatedRepairCost, 2) != estimatedRepairCost)
        {
            throw new ArgumentOutOfRangeException(nameof(estimatedRepairCost),
                "Repair estimates require nonnegative MYR minor-unit precision.");
        }

        PointCode = pointCode;
        Severity = severity;
        EstimatedRepairCost = estimatedRepairCost;
    }

    public string PointCode { get; }
    public InspectionSeverity Severity { get; }
    public decimal EstimatedRepairCost { get; }
}
