using System.Numerics;

namespace Dealership.Domain.Inspections;

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
    public static InspectionAssessment Calculate(IReadOnlyCollection<InspectionFinding> findings)
    {
        ArgumentNullException.ThrowIfNull(findings);

        if (findings.Count > InspectionCatalog.Points.Count)
        {
            throw new ArgumentException("An inspection has at most one finding per catalog point.", nameof(findings));
        }

        var pointCodes = new HashSet<string>(StringComparer.Ordinal);
        var total = 0m;
        var worstSeverity = InspectionSeverity.Advisory;

        foreach (var finding in findings)
        {
            ArgumentNullException.ThrowIfNull(finding);

            if (!pointCodes.Add(finding.PointCode))
            {
                throw new ArgumentException("An inspection cannot repeat a catalog point.", nameof(findings));
            }

            if (finding.EstimatedRepairCost > decimal.MaxValue - total)
            {
                throw new ArgumentOutOfRangeException(nameof(findings), "Total repair estimate exceeds monetary precision.");
            }

            var nextTotal = total + finding.EstimatedRepairCost;
            if (MinorUnits(nextTotal) != MinorUnits(total) + MinorUnits(finding.EstimatedRepairCost))
            {
                throw new ArgumentOutOfRangeException(nameof(findings), "Total repair estimate loses minor-unit precision.");
            }

            total = nextTotal;
            if (finding.Severity > worstSeverity)
            {
                worstSeverity = finding.Severity;
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
