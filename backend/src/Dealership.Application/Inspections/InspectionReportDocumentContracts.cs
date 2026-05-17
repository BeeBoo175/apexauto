namespace Dealership.Application.Inspections;

public sealed record InspectionReportDocumentResult(InspectionAssignmentStatus Status, byte[]? Content = null);

public interface IInspectionReportDocumentRenderer
{
    // Evidence is keyed by photo ID; a retained photo that cannot be read is absent and shown as unavailable.
    byte[] Render(InspectionReport report, IReadOnlyDictionary<Guid, byte[]> evidence);
}
