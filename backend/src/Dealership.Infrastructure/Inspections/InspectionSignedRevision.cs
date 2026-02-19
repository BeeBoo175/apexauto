using System.Text.Json;
using Dealership.Application.Inspections;

namespace Dealership.Infrastructure.Inspections;

public sealed class InspectionSignedRevision
{
    private InspectionSignedRevision()
    {
    }

    public InspectionSignedRevision(InspectionReport report)
    {
        Id = report.OperationId;
        InspectionId = report.InspectionId;
        BranchId = report.BranchId;
        SaveId = report.SaveId;
        TechnicianId = report.TechnicianId;
        AssignmentVersion = report.AssignmentVersion;
        SaveVersion = report.SaveVersion;
        Revision = report.Revision;
        SignedAt = report.SignedAt;
        ReportJson = JsonSerializer.Serialize(report, JsonSerializerOptions.Web);
    }

    public Guid Id { get; private set; }
    public Guid InspectionId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid SaveId { get; private set; }
    public Guid TechnicianId { get; private set; }
    public long AssignmentVersion { get; private set; }
    public long SaveVersion { get; private set; }
    public long Revision { get; private set; }
    public DateTimeOffset SignedAt { get; private set; }
    public string ReportJson { get; private set; } = string.Empty;

    public InspectionReport Report() => JsonSerializer.Deserialize<InspectionReport>(ReportJson, JsonSerializerOptions.Web)!;

    public bool Matches(Guid actorId, Guid inspectionId, InspectionSignRequest request) =>
        TechnicianId == actorId && InspectionId == inspectionId && AssignmentVersion == request.AssignmentVersion &&
        SaveVersion == request.SaveVersion && Revision == request.Revision;
}
