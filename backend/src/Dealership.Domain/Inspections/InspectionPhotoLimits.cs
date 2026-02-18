namespace Dealership.Domain.Inspections;

public static class InspectionPhotoLimits
{
    public const int MaximumPerFinding = 4;

    public static int MaximumPerSave => InspectionCatalog.Points.Count * MaximumPerFinding;
}
