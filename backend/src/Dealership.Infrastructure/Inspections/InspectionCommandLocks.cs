using Dealership.Application.Inspections;
using Dealership.Domain.Inspections;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Inspections;

internal static class InspectionCommandLocks
{
    // The caller owns the transaction; assignment also requires vehicle-before-draft ordering.
    public static async Task<LockedRows> LockAsync(DealershipDbContext database, Guid actorId, Guid inspectionId,
        CancellationToken cancellationToken)
    {
        var actor = await database.Users.FromSqlInterpolated(
            $"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {actorId} FOR SHARE")
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        var branchActive = false;

        if (actor?.BranchId is not null)
        {
            branchActive = await database.Branches.FromSqlInterpolated(
                $"SELECT * FROM \"Branches\" WHERE \"Id\" = {actor.BranchId} FOR UPDATE")
                .AnyAsync(branch => branch.IsActive, cancellationToken);
        }

        var vehicleId = await database.InspectionDrafts.Where(draft => draft.Id == inspectionId)
            .Select(draft => (Guid?)draft.VehicleId).SingleOrDefaultAsync(cancellationToken);
        var vehicle = await database.Vehicles.FromSqlInterpolated(
            $"SELECT * FROM \"Vehicles\" WHERE \"Id\" = {vehicleId} FOR UPDATE")
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        var draft = await database.InspectionDrafts.FromSqlInterpolated(
            $"SELECT * FROM \"InspectionDrafts\" WHERE \"Id\" = {inspectionId} FOR UPDATE")
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);

        var occupied = await database.TestDriveOccupations.AnyAsync(item => item.VehicleId == vehicleId &&
            item.OutcomeId == null, cancellationToken);
        return new(actor is null ? null : new InspectionStaff(actor.IsActive, actor.Role, actor.BranchId),
            branchActive, actor?.UserName, vehicle, draft, occupied);
    }

    internal sealed record LockedRows(InspectionStaff? Actor, bool BranchActive, string? TechnicianIdentity,
        Vehicle? Vehicle, InspectionDraft? Draft, bool VehicleOccupied);
}
