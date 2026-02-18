namespace Dealership.Infrastructure.Inspections;

public sealed class InspectionDraftPhoto
{
    public Guid SaveId { get; init; }
    public Guid InspectionId { get; init; }
    public Guid PhotoId { get; init; }
    public string PointCode { get; init; } = string.Empty;
}
