using Dealership.Application.Accounts;
using Dealership.Application.Inspections;
using Dealership.Domain.Inspections;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dealership.Infrastructure.Inspections;

public sealed class InspectionRepairCompletionStore(DealershipDbContext database) : IInspectionRepairCompletionStore
{
    public async Task<InspectionRepairCompletionResult> CompleteAsync(Guid actorId, Guid inspectionId,
        InspectionRepairCompletionRequest request, CancellationToken cancellationToken)
    {
        if (actorId == Guid.Empty || inspectionId == Guid.Empty || request.OperationId == Guid.Empty ||
            request.RequestId == Guid.Empty || request.AssignmentVersion < 1 || request.Revision < 1 || request.RepairVersion < 1)
        {
            return new(InspectionAssignmentStatus.Invalid);
        }

        try
        {
            request = request with { Notes = InspectionAssignment.NormalizeReason(request.Notes) };
        }
        catch (ArgumentException)
        {
            return new(InspectionAssignmentStatus.Invalid);
        }
        if (request.Notes is null)
        {
            return new(InspectionAssignmentStatus.Invalid);
        }

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({"inspection-repair-completion:" + request.OperationId}, 0))", cancellationToken);
            var actor = await database.Users.FromSqlInterpolated(
                $"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {actorId} FOR SHARE")
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            if (actor is not { IsActive: true, Role: AccountRoles.WorkshopTechnician, BranchId: not null } ||
                !await database.Branches.FromSqlInterpolated($"SELECT * FROM \"Branches\" WHERE \"Id\" = {actor.BranchId} FOR SHARE")
                    .AnyAsync(branch => branch.IsActive, cancellationToken))
            {
                return new(InspectionAssignmentStatus.Forbidden);
            }

            var vehicleId = await database.InspectionDrafts.Where(draft => draft.Id == inspectionId)
                .Select(draft => (Guid?)draft.VehicleId).SingleOrDefaultAsync(cancellationToken);
            var vehicle = await database.Vehicles.FromSqlInterpolated(
                $"SELECT * FROM \"Vehicles\" WHERE \"Id\" = {vehicleId} FOR UPDATE")
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            var draft = await database.InspectionDrafts.FromSqlInterpolated(
                $"SELECT * FROM \"InspectionDrafts\" WHERE \"Id\" = {inspectionId} FOR UPDATE")
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            if (vehicle is null || draft is null || draft.BranchId != actor.BranchId || draft.TechnicianId != actorId)
            {
                return new(InspectionAssignmentStatus.NotFound);
            }

            var existing = await database.InspectionRepairCompletions.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == request.OperationId, cancellationToken);
            if (existing is not null)
            {
                return existing.Matches(actorId, inspectionId, request)
                    ? new(InspectionAssignmentStatus.Succeeded, existing.Receipt()) : new(InspectionAssignmentStatus.Conflict);
            }

            var repair = await database.InspectionRepairRequests.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == request.RequestId, cancellationToken);
            if (!draft.IsSigned || draft.Revision != request.Revision || draft.AssignmentVersion != request.AssignmentVersion ||
                draft.RepairVersion != request.RepairVersion || vehicle.Status != VehicleStatus.PendingInspection ||
                repair is null || repair.InspectionId != inspectionId || repair.Revision != request.Revision ||
                repair.Version != request.RepairVersion || repair.AssignmentVersion != request.AssignmentVersion ||
                await database.InspectionRepairCompletions.AnyAsync(item => item.RequestId == request.RequestId, cancellationToken))
            {
                return new(InspectionAssignmentStatus.Conflict);
            }

            // Approval may cover a later request only while its revision/work match and its budget does not increase.
            var approval = repair.Budget == 0 ? null : await (
                from granted in database.InspectionRepairApprovals.AsNoTracking()
                join scope in database.InspectionRepairRequests.AsNoTracking() on granted.RequestId equals scope.Id
                where scope.InspectionId == inspectionId && scope.Revision == repair.Revision &&
                    scope.ItemsJson == repair.ItemsJson && scope.Budget >= repair.Budget
                orderby granted.ApprovedAt descending, granted.Id descending
                select granted).FirstOrDefaultAsync(cancellationToken);
            if (repair.Budget > 0 && approval is null)
            {
                return new(InspectionAssignmentStatus.Conflict);
            }

            var now = await database.Database.SqlQuery<DateTimeOffset>($"SELECT clock_timestamp() AS \"Value\"")
                .SingleAsync(cancellationToken);
            var receipt = new InspectionRepairCompletionReceipt(request.OperationId, inspectionId, draft.BranchId,
                actorId, repair.Id, approval?.Id, draft.AssignmentVersion, draft.Revision, draft.RepairVersion, request.Notes, now);
            database.InspectionRepairCompletions.Add(new(receipt));
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(InspectionAssignmentStatus.Succeeded, receipt);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return new(InspectionAssignmentStatus.Conflict);
        }
        finally
        {
            database.ChangeTracker.Clear();
        }
    }
}
