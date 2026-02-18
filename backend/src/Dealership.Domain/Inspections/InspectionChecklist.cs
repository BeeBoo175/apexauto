namespace Dealership.Domain.Inspections;

public static class InspectionChecklist
{
    public static IReadOnlyList<string> Attest(IReadOnlyCollection<InspectionDraftEntry> entries,
        IReadOnlyCollection<string> examinedPointCodes)
    {
        ArgumentNullException.ThrowIfNull(examinedPointCodes);
        _ = InspectionDraftEntry.Assess(entries);
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
