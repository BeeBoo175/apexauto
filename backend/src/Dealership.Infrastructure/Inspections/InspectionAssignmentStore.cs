using Dealership.Domain.Accounts;
using System.Data;
using Dealership.Application.Accounts;
using Dealership.Application.Common;
using Dealership.Application.Inspections;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Inspections;

public sealed class InspectionAssignmentStore(DealershipDbContext database) : IInspectionAssignmentStore
{
    public async Task<InspectionTechnicianResult> TechniciansAsync(Guid actorId, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        if (!Pagination.IsValid(page, pageSize))
        {
            return new(InspectionAssignmentStatus.Invalid);
        }

        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var actor = await database.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == actorId, cancellationToken);
        if (actor is not { IsActive: true, Role: AccountRoles.BranchManager, BranchId: not null } ||
            !await database.Branches.AnyAsync(item => item.Id == actor.BranchId && item.IsActive, cancellationToken))
        {
            return new(InspectionAssignmentStatus.Forbidden);
        }

        var technicians = database.Users.AsNoTracking().Where(item => item.IsActive &&
            item.BranchId == actor.BranchId && item.Role == AccountRoles.WorkshopTechnician);
        var total = await technicians.CountAsync(cancellationToken);
        var items = await technicians.OrderBy(item => item.UserName).ThenBy(item => item.Id)
            .Select(item => new InspectionTechnician(item.Id,
                database.StaffProfiles.Where(profile => profile.UserId == item.Id)
                    .Select(profile => profile.Name ?? PersonLabels.FormerEmployee).FirstOrDefault() ?? item.UserName!,
                database.AccountPictures.Where(AccountPicture.Current).Where(picture => picture.UserId == item.Id)
                    .Select(picture => (Guid?)picture.Id).FirstOrDefault()))
            .ReadPageAsync(page, pageSize, total, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(InspectionAssignmentStatus.Succeeded, new(Array.AsReadOnly(items), total, page, pageSize));
    }

    public async Task<IInspectionAssignmentTransaction> LockAsync(Guid actorId, Guid vehicleId,
        InspectionAssignmentRequest request, CancellationToken cancellationToken)
    {
        var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({"inspection-operation:" + request.OperationId}, 0))", cancellationToken);
            var users = await database.Users.FromSqlInterpolated($"""
                SELECT * FROM "AspNetUsers" WHERE "Id" IN ({actorId}, {request.TechnicianId}) ORDER BY "Id" FOR SHARE
                """).AsNoTracking().ToArrayAsync(cancellationToken);
            var actor = users.SingleOrDefault(item => item.Id == actorId);
            var technician = users.SingleOrDefault(item => item.Id == request.TechnicianId);
            var branchActive = false;

            if (actor?.BranchId is not null)
            {
                branchActive = await database.Branches.FromSqlInterpolated(
                    $"SELECT * FROM \"Branches\" WHERE \"Id\" = {actor.BranchId} FOR UPDATE")
                    .AnyAsync(item => item.IsActive, cancellationToken);
            }

            var vehicle = await database.Vehicles.FromSqlInterpolated(
                $"SELECT * FROM \"Vehicles\" WHERE \"Id\" = {vehicleId} FOR UPDATE")
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            var existing = await database.InspectionAssignments.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == request.OperationId, cancellationToken);
            var existingVehicleId = existing is null ? null : await database.InspectionDrafts
                .Where(item => item.Id == existing.InspectionId).Select(item => (Guid?)item.VehicleId)
                .SingleOrDefaultAsync(cancellationToken);
            var draft = await database.InspectionDrafts.SingleOrDefaultAsync(item => item.VehicleId == vehicleId, cancellationToken);
            var context = new InspectionAssignmentContext(
                actor is null ? null : new InspectionStaff(actor.IsActive, actor.Role, actor.BranchId), branchActive, vehicle,
                technician is null ? null : new InspectionStaff(technician.IsActive, technician.Role, technician.BranchId),
                existing, existingVehicleId, draft);
            return new InspectionAssignmentTransaction(database, transaction, context);
        }
        catch
        {
            try
            {
                await transaction.DisposeAsync();
            }
            finally
            {
                database.ChangeTracker.Clear();
            }

            throw;
        }
    }

    public async Task<InspectionDraftResult> ReadAsync(Guid actorId, Guid inspectionId, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var actor = await InspectionReadScope.LockActorAsync(database, actorId, cancellationToken);
        if (actor is null)
        {
            return new(InspectionAssignmentStatus.Forbidden);
        }

        var query = database.InspectionDrafts.FromSqlInterpolated(
            $"SELECT * FROM \"InspectionDrafts\" WHERE \"Id\" = {inspectionId} FOR SHARE").AsNoTracking();
        if (actor.Role != AccountRoles.GeneralManager)
        {
            query = query.Where(item => item.BranchId == actor.BranchId);
        }
        if (actor.Role == AccountRoles.WorkshopTechnician)
        {
            query = query.Where(item => item.TechnicianId == actorId);
        }

        var draft = await InspectionDraftProjection.ReadAsync(database, query, inspectionId, cancellationToken);
        return draft is null ? new(InspectionAssignmentStatus.NotFound) : new(InspectionAssignmentStatus.Succeeded, draft);
    }
}
