using System.Numerics;

namespace Dealership.Domain.Inspections;

public enum InspectionSeverity
{
    Advisory,
    MinorDefect,
    MajorDefect,
    CriticalDisqualifier
}

public enum InspectionGrade
{
    A,
    B,
    C
}

public sealed record InspectionAssessment
{
    internal InspectionAssessment(decimal estimatedRepairCost, InspectionGrade? grade)
    {
        EstimatedRepairCost = estimatedRepairCost;
        Grade = grade;
    }

    public decimal EstimatedRepairCost { get; }
    public InspectionGrade? Grade { get; }
    public bool IsRejected => Grade is null;
}

public static class InspectionGrading
{
    public static InspectionAssessment Calculate(IReadOnlyCollection<InspectionDraftEntry> entries)
    {
        InspectionDraftEntry.ValidateSet(entries);
        var total = 0m;
        var worstSeverity = InspectionSeverity.Advisory;

        foreach (var finding in entries)
        {
            if (finding.Kind != InspectionEntryKind.Finding)
            {
                continue;
            }

            if (finding.EstimatedRepairCost > decimal.MaxValue - total)
            {
                throw new ArgumentOutOfRangeException(nameof(entries), "Total repair estimate exceeds monetary precision.");
            }

            var nextTotal = total + finding.EstimatedRepairCost;
            if (MinorUnits(nextTotal) != MinorUnits(total) + MinorUnits(finding.EstimatedRepairCost))
            {
                throw new ArgumentOutOfRangeException(nameof(entries), "Total repair estimate loses minor-unit precision.");
            }

            total = nextTotal;
            if (finding.Severity!.Value > worstSeverity)
            {
                worstSeverity = finding.Severity!.Value;
            }
        }

        if (worstSeverity == InspectionSeverity.CriticalDisqualifier || total > 15000m)
        {
            return new InspectionAssessment(total, null);
        }

        if (worstSeverity == InspectionSeverity.MajorDefect || total >= 2500m)
        {
            return new InspectionAssessment(total, InspectionGrade.C);
        }

        if (worstSeverity == InspectionSeverity.MinorDefect || total >= 500m)
        {
            return new InspectionAssessment(total, InspectionGrade.B);
        }

        return new InspectionAssessment(total, InspectionGrade.A);
    }

    private static BigInteger MinorUnits(decimal amount)
    {
        var whole = decimal.Truncate(amount);
        return new BigInteger(whole) * 100 + new BigInteger((amount - whole) * 100m);
    }
}
