using System.Data;
using Dealership.Application.Accounts;
using Dealership.Application.Inspections;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Inspections;

public sealed class InspectionQueueStore(DealershipDbContext database) : IInspectionQueueStore
{
    public async Task<InspectionQueueResult> InspectionsAsync(Guid actorId, InspectionQueueFilter filter,
        InspectionQueueStatus status, CancellationToken cancellationToken)
    {
        if (!filter.IsValid || !Enum.IsDefined(status))
        {
            return new(InspectionAssignmentStatus.Invalid);
        }

        // Current authorization, scoped totals and page rows must describe the same snapshot.
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var actor = await ScopeAsync(actorId, filter.BranchId, cancellationToken);
        if (actor is null)
        {
            return new(InspectionAssignmentStatus.Forbidden);
        }

        var branchId = actor.BranchId ?? filter.BranchId;
        var vehicles = database.Vehicles.AsNoTracking().Where(item => branchId == null || item.BranchId == branchId);
        if (filter.From is not null)
        {
            vehicles = vehicles.Where(item => item.CreatedAt >= filter.From && item.CreatedAt < filter.Until);
        }

        var rows = from vehicle in vehicles
                   join branch in database.Branches.AsNoTracking() on vehicle.BranchId equals branch.Id
                   join assigned in database.InspectionDrafts.AsNoTracking() on vehicle.Id equals assigned.VehicleId into drafts
                   from draft in drafts.DefaultIfEmpty()
                   where actor.Role != AccountRoles.WorkshopTechnician || draft.TechnicianId == actorId ||
                       draft == null && vehicle.Status == VehicleStatus.PendingInspection
                   select new
                   {
                       Vehicle = vehicle,
                       BranchName = branch.Name,
                       Draft = draft,
                       State = draft == null ? InspectionQueueStatus.Unassigned : InspectionQueueStatus.Draft
                   };
        // Inspection progress remains separate from the vehicle's sale status.
        var work = rows.Select(row => new
        {
            row.Vehicle,
            row.BranchName,
            row.Draft,
            State = row.Draft != null && row.Draft.IsSigned ? InspectionQueueStatus.Sealed : row.State
        });
        if (status != InspectionQueueStatus.All)
        {
            work = work.Where(row => row.State == status);
        }
        if (filter.Search.Length > 0)
        {
            var pattern = Pattern(filter.Search);
            work = work.Where(row => EF.Functions.ILike(row.Vehicle.RegistrationPlate, pattern, "!") ||
                EF.Functions.ILike(row.Vehicle.Make, pattern, "!") || EF.Functions.ILike(row.Vehicle.Model, pattern, "!") ||
                EF.Functions.ILike(row.BranchName, pattern, "!") || EF.Functions.ILike(row.Vehicle.Id.ToString(), pattern, "!"));
        }

        var total = await work.CountAsync(cancellationToken);
        var offset = (long)(filter.Page - 1) * filter.PageSize;
        InspectionQueueItem[] items = [];
        if (offset < total)
        {
            var page = await work.OrderByDescending(row => row.Vehicle.CreatedAt).ThenByDescending(row => row.Vehicle.Id)
                .Skip((int)offset).Take(filter.PageSize).ToArrayAsync(cancellationToken);
            items = [.. page.Select(row => new InspectionQueueItem(row.Vehicle.Id, row.Vehicle.BranchId, row.BranchName,
                row.Vehicle.RegistrationPlate, row.Vehicle.Make, row.Vehicle.Model, row.Vehicle.CreatedAt,
                row.Vehicle.Status.ToString(), row.Draft?.Id, row.Draft?.TechnicianId, row.Draft?.AssignmentVersion,
                row.Draft?.Revision, row.State))];
        }

        await transaction.CommitAsync(cancellationToken);
        return new(InspectionAssignmentStatus.Succeeded, new(Array.AsReadOnly(items), total, filter.Page, filter.PageSize));
    }

    public async Task<InspectionRepairQueueResult> RepairsAsync(Guid actorId, InspectionQueueFilter filter,
        InspectionRepairQueueStatus status, CancellationToken cancellationToken)
    {
        if (!filter.IsValid || !Enum.IsDefined(status))
        {
            return new(InspectionAssignmentStatus.Invalid);
        }

        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var actor = await ScopeAsync(actorId, filter.BranchId, cancellationToken);
        if (actor is null)
        {
            return new(InspectionAssignmentStatus.Forbidden);
        }

        var branchId = actor.BranchId ?? filter.BranchId;
        var requests = database.InspectionRepairRequests.AsNoTracking().Where(item => branchId == null || item.BranchId == branchId);
        if (filter.From is not null)
        {
            requests = requests.Where(item => item.RequestedAt >= filter.From && item.RequestedAt < filter.Until);
        }

        var rows = from request in requests
                   join draft in database.InspectionDrafts.AsNoTracking() on request.InspectionId equals draft.Id
                   join vehicle in database.Vehicles.AsNoTracking() on draft.VehicleId equals vehicle.Id
                   join branch in database.Branches.AsNoTracking() on request.BranchId equals branch.Id
                   join approved in database.InspectionRepairApprovals.AsNoTracking() on request.Id equals approved.RequestId into approvals
                   from approval in approvals.DefaultIfEmpty()
                   from covering in (from granted in database.InspectionRepairApprovals.AsNoTracking()
                                     join scope in database.InspectionRepairRequests.AsNoTracking() on granted.RequestId equals scope.Id
                                     where scope.InspectionId == request.InspectionId && scope.Revision == request.Revision &&
                                         scope.ItemsJson == request.ItemsJson && scope.Budget >= request.Budget
                                     orderby granted.ApprovedAt descending, granted.Id descending
                                     select granted).Take(1).DefaultIfEmpty()
                   where actor.Role != AccountRoles.WorkshopTechnician || draft.TechnicianId == actorId
                   select new
                   {
                       Request = request,
                       Draft = draft,
                       Vehicle = vehicle,
                       BranchName = branch.Name,
                       ApprovalId = (Guid?)approval.Id,
                       CoveringApprovalId = (Guid?)covering.Id,
                       ApprovalState = covering == null ? InspectionRepairQueueStatus.PendingApproval : InspectionRepairQueueStatus.Approved
                   };
        var priced = rows.Select(row => new
        {
            row.Request,
            row.Draft,
            row.Vehicle,
            row.BranchName,
            row.ApprovalId,
            row.CoveringApprovalId,
            State = row.Request.Budget == 0 ? InspectionRepairQueueStatus.ApprovalNotRequired : row.ApprovalState
        });
        // Retained approval evidence cannot grant actionability to an old request or reopened revision.
        var work = priced.Select(row => new
        {
            row.Request,
            row.Vehicle,
            row.BranchName,
            row.ApprovalId,
            row.CoveringApprovalId,
            row.Draft.TechnicianId,
            State = !row.Draft.IsSigned || row.Draft.Revision != row.Request.Revision ||
                row.Draft.AssignmentVersion != row.Request.AssignmentVersion || row.Draft.RepairVersion != row.Request.Version ||
                row.Vehicle.Status != VehicleStatus.PendingInspection ? InspectionRepairQueueStatus.Superseded : row.State
        });
        if (status != InspectionRepairQueueStatus.All)
        {
            work = work.Where(row => row.State == status);
        }
        if (filter.Search.Length > 0)
        {
            var pattern = Pattern(filter.Search);
            work = work.Where(row => EF.Functions.ILike(row.Vehicle.RegistrationPlate, pattern, "!") ||
                EF.Functions.ILike(row.BranchName, pattern, "!") || EF.Functions.ILike(row.Request.Id.ToString(), pattern, "!") ||
                EF.Functions.ILike(row.Request.InspectionId.ToString(), pattern, "!"));
        }

        var total = await work.CountAsync(cancellationToken);
        var offset = (long)(filter.Page - 1) * filter.PageSize;
        InspectionRepairQueueItem[] items = [];
        if (offset < total)
        {
            var page = await work.OrderByDescending(row => row.Request.RequestedAt).ThenByDescending(row => row.Request.Id)
                .Skip((int)offset).Take(filter.PageSize).ToArrayAsync(cancellationToken);
            items = [.. page.Select(row => new InspectionRepairQueueItem(row.Request.Id, row.Request.InspectionId,
                row.Vehicle.Id, row.Request.BranchId, row.BranchName, row.Vehicle.RegistrationPlate, row.TechnicianId,
                row.Request.AssignmentVersion, row.Request.Revision, row.Request.Version, row.Request.Budget,
                row.Request.RequestedAt, row.ApprovalId, row.CoveringApprovalId, row.State))];
        }

        await transaction.CommitAsync(cancellationToken);
        return new(InspectionAssignmentStatus.Succeeded, new(Array.AsReadOnly(items), total, filter.Page, filter.PageSize));
    }

    private async Task<AccountUser?> ScopeAsync(Guid actorId, Guid? branchId, CancellationToken cancellationToken)
    {
        var actor = await database.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == actorId, cancellationToken);
        if (actor is not { IsActive: true } ||
            actor.Role is not (AccountRoles.WorkshopTechnician or AccountRoles.BranchManager or AccountRoles.GeneralManager))
        {
            return null;
        }
        if (actor.Role == AccountRoles.GeneralManager)
        {
            return actor;
        }
        if (actor.BranchId is null || branchId is not null && actor.BranchId != branchId ||
            !await database.Branches.AnyAsync(item => item.Id == actor.BranchId && item.IsActive, cancellationToken))
        {
            return null;
        }

        return actor;
    }

    private static string Pattern(string search) => "%" + search.Replace("!", "!!", StringComparison.Ordinal)
        .Replace("%", "!%", StringComparison.Ordinal).Replace("_", "!_", StringComparison.Ordinal) + "%";
}
