using Dealership.Domain.Inspections;
using Dealership.Domain.Vehicles;

namespace Dealership.Application.Inspections;

public sealed record InspectionReport(Guid OperationId, Guid InspectionId, long Revision, Guid BranchId,
    Guid SaveId, long SaveVersion, long AssignmentVersion, InspectionReportVehicle Vehicle,
    Guid TechnicianId, string TechnicianIdentity, string CatalogVersion, InspectionDraftAssessment Assessment,
    IReadOnlyList<InspectionReportPoint> Checklist, DateTimeOffset SignedAt)
{
    public static InspectionReport Sign(Guid operationId, long revision, InspectionDraft draft, Vehicle vehicle,
        string technicianIdentity, InspectionDraftSaveReceipt save, IReadOnlyCollection<InspectionPhoto> photos,
        DateTimeOffset signedAt)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(vehicle);
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(photos);
        if (operationId == Guid.Empty || revision < 1 || string.IsNullOrWhiteSpace(technicianIdentity) ||
            vehicle.Id != draft.VehicleId || vehicle.BranchId != draft.BranchId ||
            vehicle.Status != VehicleStatus.PendingInspection || save.OperationId == Guid.Empty ||
            save.InspectionId != draft.Id || save.ActorId != draft.TechnicianId ||
            save.AssignmentVersion != draft.AssignmentVersion || save.Version <= draft.ReopenedAfterSaveVersion ||
            save.CatalogVersion != InspectionCatalog.Version || signedAt < save.RecordedAt ||
            save.RecordedAt < draft.CreatedAt || save.ExaminedPointCodes is null)
        {
            throw new ArgumentException("Signing requires the current assigned technician's complete attested save and matching vehicle.");
        }

        InspectionChecklist.Attest(save.Entries.ToArray(), save.ExaminedPointCodes.ToArray());
        var assessment = InspectionGrading.Calculate(save.Entries.ToArray());
        if (save.Assessment != new InspectionDraftAssessment(assessment.EstimatedRepairCost, assessment.Grade, assessment.IsRejected))
        {
            throw new ArgumentException("The original assessment must match the selected save.");
        }

        ValidatePhotos(draft, save, photos, signedAt);
        var entries = save.Entries.ToDictionary(entry => entry.PointCode, StringComparer.Ordinal);
        var checklist = InspectionCatalog.Points.Select(point =>
        {
            entries.TryGetValue(point.Code, out var entry);
            var evidence = photos.Where(photo => photo.PointCode == point.Code).OrderBy(photo => photo.Id)
                .Select(photo => new InspectionReportPhoto(photo.Id, photo.ContentType, photo.Bytes,
                    photo.Sha256, photo.Width, photo.Height)).ToArray();
            return new InspectionReportPoint(point.Code, point.Category, point.Description, entry?.Kind,
                entry?.Severity, entry?.EstimatedRepairCost ?? 0m, entry?.Notes, Array.AsReadOnly(evidence));
        }).ToArray();

        return new(operationId, draft.Id, revision, draft.BranchId, save.OperationId, save.Version,
            draft.AssignmentVersion, new(vehicle.Id, vehicle.IntakeAppointmentId, vehicle.IntakeOwnerId,
                vehicle.Vin, vehicle.RegistrationPlate, vehicle.Make, vehicle.Model, vehicle.Trim,
                vehicle.ManufactureYear, vehicle.MileageKilometres, vehicle.CreatedAt), draft.TechnicianId,
            technicianIdentity, save.CatalogVersion, save.Assessment, Array.AsReadOnly(checklist), signedAt);
    }

    private static void ValidatePhotos(InspectionDraft draft, InspectionDraftSaveReceipt save,
        IReadOnlyCollection<InspectionPhoto> photos, DateTimeOffset signedAt)
    {
        var findings = save.Entries.Where(entry => entry.Kind == InspectionEntryKind.Finding)
            .Select(entry => entry.PointCode).ToHashSet(StringComparer.Ordinal);
        if (photos.Any(photo => photo is null) || photos.Count != save.PhotoIds.Count ||
            photos.Select(photo => photo.Id).Distinct().Count() != photos.Count ||
            !photos.Select(photo => photo.Id).ToHashSet().SetEquals(save.PhotoIds) ||
            photos.Any(photo => photo.InspectionId != draft.Id || photo.BranchId != draft.BranchId ||
                photo.AvailableAt is null || photo.AvailableAt > signedAt || !findings.Contains(photo.PointCode)) ||
            photos.GroupBy(photo => photo.PointCode).Any(group => group.Count() > InspectionPhotoLimits.MaximumPerFinding))
        {
            throw new ArgumentException("Signed evidence must exactly match the selected save's available finding photos.");
        }
    }
}

// Snapshot values deliberately avoid current-catalog constructors so historical reports survive rule changes.
public sealed record InspectionReportPoint(string Code, InspectionCategory Category, string Description,
    InspectionEntryKind? Kind, InspectionSeverity? Severity, decimal EstimatedRepairCost, string? Notes,
    IReadOnlyList<InspectionReportPhoto> Photos);

public sealed record InspectionReportPhoto(Guid Id, string ContentType, long Bytes, string Sha256, int Width, int Height);

public sealed record InspectionReportVehicle(Guid Id, Guid IntakeAppointmentId, Guid IntakeOwnerId,
    string Vin, string RegistrationPlate, string Make, string Model, string Trim, int ManufactureYear,
    int MileageKilometres, DateTimeOffset CheckedInAt);

public sealed record InspectionSignRequest(Guid OperationId, long AssignmentVersion, long SaveVersion, long Revision);

public sealed record InspectionReportResult(InspectionAssignmentStatus Status, InspectionReport? Report = null);

public sealed record InspectionReopenRequest(Guid OperationId, long AssignmentVersion, long Revision, string? Reason);

public sealed record InspectionReopenReceipt(Guid OperationId, Guid InspectionId, Guid BranchId, Guid ActorId,
    Guid SignedRevisionId, long AssignmentVersion, long ExpectedRevision, long Revision,
    long AfterSaveVersion, string Reason, DateTimeOffset RecordedAt);

public sealed record InspectionReopenResult(InspectionAssignmentStatus Status, InspectionReopenReceipt? Receipt = null);

public interface IInspectionReportStore
{
    Task<InspectionReopenResult> ReopenAsync(Guid actorId, Guid inspectionId, InspectionReopenRequest request,
        CancellationToken cancellationToken);

    Task<InspectionReportResult> SignAsync(Guid actorId, Guid inspectionId, InspectionSignRequest request,
        CancellationToken cancellationToken);

    Task<InspectionReportResult> ReadAsync(Guid actorId, Guid inspectionId, long revision, CancellationToken cancellationToken);
}
