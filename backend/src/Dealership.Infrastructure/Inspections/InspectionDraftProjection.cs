using Dealership.Application.Inspections;
using Dealership.Domain.Inspections;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Inspections;

internal static class InspectionDraftProjection
{
    public static async Task<InspectionDraftSnapshot?> ReadAsync(DealershipDbContext database,
        IQueryable<InspectionDraft> drafts, Guid inspectionId, CancellationToken cancellationToken)
    {
        var draft = await drafts.Select(item => new InspectionDraftSnapshot(item.Id, item.VehicleId, item.BranchId,
            item.TechnicianId, item.AssignmentVersion, item.CreatedAt, null)
        {
            Revision = item.Revision,
            IsSigned = item.IsSigned,
            ReopenedAfterSaveVersion = item.ReopenedAfterSaveVersion,
            RepairVersion = item.RepairVersion
        }).SingleOrDefaultAsync(cancellationToken);
        if (draft is not null)
        {
            var vehicle = await (from item in database.Vehicles.AsNoTracking()
                                 join branch in database.Branches.AsNoTracking() on item.BranchId equals branch.Id
                                 where item.Id == draft.VehicleId
                                 select new InspectionDraftVehicle(item.RegistrationPlate, item.Make, item.Model,
                                     item.Vin, item.MileageKilometres, item.Status.ToString(), branch.Name))
                .SingleAsync(cancellationToken);
            var latest = await database.InspectionDraftSaves.AsNoTracking().Where(save => save.InspectionId == inspectionId)
                .OrderByDescending(save => save.Version).FirstOrDefaultAsync(cancellationToken);
            var saveId = latest?.Id;
            // Older saves retain evidence, but only the latest save supplies the editable photo set.
            var photos = await database.InspectionDraftPhotos.AsNoTracking()
                .Where(photo => photo.SaveId == saveId && photo.InspectionId == inspectionId)
                .OrderBy(photo => photo.PhotoId)
                .Select(photo => new InspectionPhotoReference(photo.PhotoId, photo.PointCode))
                .ToArrayAsync(cancellationToken);
            var technician = await database.Users.AsNoTracking().Where(user => user.Id == draft.TechnicianId)
                .Select(user => user.UserName).FirstOrDefaultAsync(cancellationToken);
            draft = draft with
            {
                LatestSave = latest?.Receipt(),
                Vehicle = vehicle,
                PhotoReferences = photos,
                TechnicianIdentity = technician ?? string.Empty
            };
        }
        return draft;
    }
}
