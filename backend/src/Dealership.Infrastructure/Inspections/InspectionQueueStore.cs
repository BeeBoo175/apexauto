using System.Data;
using Dealership.Application.Accounts;
using Dealership.Application.Inspections;
using Dealership.Domain.Vehicles;
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
        var actor = await InspectionQueueScope.ReadAsync(database, actorId, filter.BranchId, cancellationToken);
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
            var pattern = LiteralSearch.Pattern(filter.Search);
            work = work.Where(row => EF.Functions.ILike(row.Vehicle.RegistrationPlate, pattern, "!") ||
                EF.Functions.ILike(row.Vehicle.Make, pattern, "!") || EF.Functions.ILike(row.Vehicle.Model, pattern, "!") ||
                EF.Functions.ILike(row.BranchName, pattern, "!") || EF.Functions.ILike(row.Vehicle.Id.ToString(), pattern, "!"));
        }

        var total = await work.CountAsync(cancellationToken);
        var page = await work.OrderByDescending(row => row.Vehicle.CreatedAt).ThenByDescending(row => row.Vehicle.Id)
            .ReadPageAsync(filter.Page, filter.PageSize, total, cancellationToken);
        var items = page.Select(row => new InspectionQueueItem(row.Vehicle.Id, row.Vehicle.BranchId, row.BranchName,
            row.Vehicle.RegistrationPlate, row.Vehicle.Make, row.Vehicle.Model, row.Vehicle.CreatedAt,
            row.Vehicle.Status.ToString(), row.Draft?.Id, row.Draft?.TechnicianId, row.Draft?.AssignmentVersion,
            row.Draft?.Revision, row.State)).ToArray();

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
        var actor = await InspectionQueueScope.ReadAsync(database, actorId, filter.BranchId, cancellationToken);
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

        var covered = InspectionRepairAuthority.CoveredRequests(database);
        var rows = from request in requests
                   join draft in database.InspectionDrafts.AsNoTracking() on request.InspectionId equals draft.Id
                   join vehicle in database.Vehicles.AsNoTracking() on draft.VehicleId equals vehicle.Id
                   join branch in database.Branches.AsNoTracking() on request.BranchId equals branch.Id
                   join approved in database.InspectionRepairApprovals.AsNoTracking() on request.Id equals approved.RequestId into approvals
                   from approval in approvals.DefaultIfEmpty()
                   join completed in database.InspectionRepairCompletions.AsNoTracking() on request.Id equals completed.RequestId into completions
                   from completion in completions.DefaultIfEmpty()
                   from covering in covered.Where(item => item.Request.Id == request.Id)
                       .OrderByDescending(item => item.Approval.ApprovedAt).ThenByDescending(item => item.Approval.Id)
                       .Select(item => item.Approval).Take(1).DefaultIfEmpty()
                   where actor.Role != AccountRoles.WorkshopTechnician || draft.TechnicianId == actorId
                   select new
                   {
                       Request = request,
                       Draft = draft,
                       Vehicle = vehicle,
                       BranchName = branch.Name,
                       ApprovalId = (Guid?)approval.Id,
                       CoveringApprovalId = (Guid?)covering.Id,
                       CompletionId = (Guid?)completion.Id,
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
            row.CompletionId,
            State = row.Request.Budget == 0 ? InspectionRepairQueueStatus.ApprovalNotRequired : row.ApprovalState
        });
        var completedWork = priced.Select(row => new
        {
            row.Request,
            row.Draft,
            row.Vehicle,
            row.BranchName,
            row.ApprovalId,
            row.CoveringApprovalId,
            row.CompletionId,
            State = row.CompletionId != null ? InspectionRepairQueueStatus.Completed : row.State
        });
        // Retained approval evidence cannot grant actionability to an old request or reopened revision.
        var work = completedWork.Select(row => new
        {
            row.Request,
            row.Vehicle,
            row.BranchName,
            row.ApprovalId,
            row.CoveringApprovalId,
            row.CompletionId,
            row.Draft.TechnicianId,
            State = !row.Draft.IsSigned || row.Draft.Revision != row.Request.Revision ||
                row.Draft.AssignmentVersion != row.Request.AssignmentVersion || row.Draft.RepairVersion != row.Request.Version ||
                row.Vehicle.Status == VehicleStatus.Rejected ? InspectionRepairQueueStatus.Superseded : row.State
        });
        if (status != InspectionRepairQueueStatus.All)
        {
            work = work.Where(row => row.State == status);
        }
        if (filter.Search.Length > 0)
        {
            var pattern = LiteralSearch.Pattern(filter.Search);
            work = work.Where(row => EF.Functions.ILike(row.Vehicle.RegistrationPlate, pattern, "!") ||
                EF.Functions.ILike(row.BranchName, pattern, "!") || EF.Functions.ILike(row.Request.Id.ToString(), pattern, "!") ||
                EF.Functions.ILike(row.Request.InspectionId.ToString(), pattern, "!"));
        }

        var total = await work.CountAsync(cancellationToken);
        var page = await work.OrderByDescending(row => row.Request.RequestedAt).ThenByDescending(row => row.Request.Id)
            .ReadPageAsync(filter.Page, filter.PageSize, total, cancellationToken);
        var items = page.Select(row => new InspectionRepairQueueItem(row.Request.Id, row.Request.InspectionId,
            row.Vehicle.Id, row.Request.BranchId, row.BranchName, row.Vehicle.RegistrationPlate, row.TechnicianId,
            row.Request.AssignmentVersion, row.Request.Revision, row.Request.Version, row.Request.Budget,
            row.Request.RequestedAt, row.ApprovalId, row.CoveringApprovalId, row.State, row.CompletionId)).ToArray();

        await transaction.CommitAsync(cancellationToken);
        return new(InspectionAssignmentStatus.Succeeded, new(Array.AsReadOnly(items), total, filter.Page, filter.PageSize));
    }

}
