using Dealership.Application.Accounts;
using Dealership.Domain.Inspections;
using Dealership.Domain.Vehicles;

namespace Dealership.Application.Inspections;

public sealed class InspectionDraftSaveService(IInspectionDraftSaveStore saves)
{
    public async Task<InspectionDraftSaveResult> SaveAsync(Guid actorId, Guid inspectionId,
        InspectionDraftSaveRequest request, CancellationToken cancellationToken)
    {
        if (actorId == Guid.Empty || inspectionId == Guid.Empty || request.OperationId == Guid.Empty ||
            request.AssignmentVersion < 1 || request.ExpectedVersion is < 0 or long.MaxValue ||
            request.Entries is null || request.Entries.Count > InspectionCatalog.Points.Count || request.PhotoIds is null ||
            request.PhotoIds.Count > InspectionPhotoLimits.MaximumPerSave || request.PhotoIds.Contains(Guid.Empty) ||
            request.PhotoIds.Distinct().Count() != request.PhotoIds.Count)
        {
            return new(InspectionAssignmentStatus.Invalid);
        }

        InspectionDraftEntry[] entries;
        InspectionAssessment assessment;
        IReadOnlyList<string>? examinedPointCodes = null;
        try
        {
            if (request.Entries.Any(entry => entry is null))
            {
                return new(InspectionAssignmentStatus.Invalid);
            }

            entries = [.. request.Entries.Select(entry => new InspectionDraftEntry(entry.PointCode, entry.Kind,
                entry.Severity, entry.EstimatedRepairCost, entry.Notes)).OrderBy(entry => entry.PointCode, StringComparer.Ordinal)];
            assessment = InspectionGrading.Calculate(entries);
            if (request.ExaminedPointCodes is not null)
            {
                examinedPointCodes = InspectionChecklist.Attest(entries, request.ExaminedPointCodes);
            }
        }
        catch (ArgumentException)
        {
            return new(InspectionAssignmentStatus.Invalid);
        }

        await using var transaction = await saves.LockAsync(actorId, inspectionId, request.OperationId, cancellationToken);
        var context = transaction.Context;
        var actor = context.Actor;

        if (actor is not { IsActive: true, BranchId: not null, Role: AccountRoles.WorkshopTechnician } || !context.BranchActive)
        {
            return new(InspectionAssignmentStatus.Forbidden);
        }

        var vehicle = context.Vehicle;
        var draft = context.Draft;
        if (vehicle is null || draft is null || draft.BranchId != actor.BranchId || draft.TechnicianId != actorId)
        {
            return new(InspectionAssignmentStatus.NotFound);
        }

        var existing = context.Existing;
        if (existing is not null)
        {
            return existing.Matches(actorId, inspectionId, request, entries)
                ? new(InspectionAssignmentStatus.Succeeded, existing) : new(InspectionAssignmentStatus.Conflict);
        }

        if (draft.AssignmentVersion != request.AssignmentVersion || context.Version != request.ExpectedVersion ||
            vehicle.Status != VehicleStatus.PendingInspection || draft.IsSigned)
        {
            return new(InspectionAssignmentStatus.Conflict);
        }

        var photoSet = await transaction.LockPhotosAsync(request.PhotoIds, cancellationToken);
        var photos = photoSet.Photos;
        var now = photoSet.RecordedAt;
        var findingCodes = entries.Where(entry => entry.Kind == InspectionEntryKind.Finding)
            .Select(entry => entry.PointCode).ToHashSet(StringComparer.Ordinal);
        if (photos.Count != request.PhotoIds.Count || photos.Any(photo => photo.InspectionId != inspectionId ||
            photo.AvailableAt is null || !findingCodes.Contains(photo.PointCode)) ||
            photos.GroupBy(photo => photo.PointCode).Any(group => group.Count() > InspectionPhotoLimits.MaximumPerFinding))
        {
            return new(InspectionAssignmentStatus.Invalid);
        }

        if (photos.Any(photo => photo.ExpiresAt <= now && !photo.Retained || photo.RemovalPending))
        {
            return new(InspectionAssignmentStatus.Conflict);
        }

        var receipt = new InspectionDraftSaveReceipt(request.OperationId, inspectionId, actorId,
            request.AssignmentVersion, request.ExpectedVersion, request.ExpectedVersion + 1,
            InspectionCatalog.Version, entries,
            new(assessment.EstimatedRepairCost, assessment.Grade, assessment.IsRejected), now)
        {
            PhotoIds = request.PhotoIds.Order().ToArray(),
            ExaminedPointCodes = examinedPointCodes
        };
        var status = await transaction.CommitAsync(receipt, photos, cancellationToken);
        return new(status, status == InspectionAssignmentStatus.Succeeded ? receipt : null);
    }
}
