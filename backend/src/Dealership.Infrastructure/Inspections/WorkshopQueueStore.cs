using System.Data;
using System.Linq.Expressions;
using Dealership.Application.Accounts;
using Dealership.Application.Inspections;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Persistence;
using static Dealership.Infrastructure.Inspections.WorkshopRows;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Inspections;

public sealed class WorkshopQueueStore(DealershipDbContext database) : IWorkshopQueueStore
{
    public async Task<WorkshopQueueResult> ReadAsync(Guid actorId, InspectionQueueFilter filter,
        WorkshopTask task, CancellationToken cancellationToken)
    {
        if (!filter.IsValid || !Enum.IsDefined(task))
        {
            return new(InspectionAssignmentStatus.Invalid);
        }

        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var actor = await InspectionQueueScope.ReadAsync(database, actorId, filter.BranchId, cancellationToken);
        if (actor is null)
        {
            return new(InspectionAssignmentStatus.Forbidden);
        }
        if (task is WorkshopTask.MyWork or WorkshopTask.Waiting or WorkshopTask.Claimable &&
            actor.Role != AccountRoles.WorkshopTechnician)
        {
            return new(InspectionAssignmentStatus.Forbidden);
        }

        var work = FilterColumns(Filter(actor, filter, task), filter);
        var total = await work.CountAsync(cancellationToken);
        var ordered = Order(work, filter);
        ordered = filter.Direction == "desc"
            ? ordered.ThenByDescending(row => row.VehicleId)
            : ordered.ThenBy(row => row.VehicleId);
        var page = await ordered
            .ReadPageAsync(filter.Page, filter.PageSize, total, cancellationToken);
        var items = Items(page);
        var counts = task is WorkshopTask.MyWork or WorkshopTask.Waiting or WorkshopTask.Claimable
            ? null
            : new WorkshopQueueCounts(
                await WorkshopRows.ForTask(database, actor, filter.BranchId, WorkshopTask.Active).CountAsync(cancellationToken),
                await WorkshopRows.ForTask(database, actor, filter.BranchId, WorkshopTask.Inspection).CountAsync(cancellationToken),
                await WorkshopRows.ForTask(database, actor, filter.BranchId, WorkshopTask.Approval).CountAsync(cancellationToken),
                await WorkshopRows.ForTask(database, actor, filter.BranchId, WorkshopTask.Repairs).CountAsync(cancellationToken),
                await WorkshopRows.ForTask(database, actor, filter.BranchId, WorkshopTask.All).CountAsync(cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        return new(InspectionAssignmentStatus.Succeeded,
            new(Array.AsReadOnly(items), total, filter.Page, filter.PageSize, counts));
    }

    public async Task<WorkshopJobsResult> ReadJobsAsync(Guid actorId, InspectionQueueFilter filter,
        WorkshopTask task, CancellationToken cancellationToken)
    {
        if (!filter.IsValid || filter.BranchId is not null || filter.From is not null ||
            filter.Sort != "since" || filter.Direction != "desc" ||
            task is not (WorkshopTask.MyWork or WorkshopTask.Waiting or WorkshopTask.Claimable))
        {
            return new(InspectionAssignmentStatus.Invalid);
        }

        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var actor = await InspectionQueueScope.ReadAsync(database, actorId, null, cancellationToken);
        if (actor?.Role != AccountRoles.WorkshopTechnician)
        {
            return new(InspectionAssignmentStatus.Forbidden);
        }

        var work = Filter(actor, filter, task);
        var total = await work.CountAsync(cancellationToken);
        var page = await work.OrderByDescending(row => row.Since).ThenByDescending(row => row.VehicleId)
            .ReadPageAsync(filter.Page, filter.PageSize, total, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return new(InspectionAssignmentStatus.Succeeded,
            new(Array.AsReadOnly(Items(page)), total, filter.Page, filter.PageSize));
    }

    private IQueryable<WorkshopRow> Filter(AccountUser actor, InspectionQueueFilter filter, WorkshopTask task)
    {
        var work = WorkshopRows.ForTask(database, actor, filter.BranchId, task);
        if (filter.From is not null)
        {
            work = task is WorkshopTask.Approval or WorkshopTask.Repairs
                ? work.Where(row => row.RequestedAt >= filter.From && row.RequestedAt < filter.Until)
                : work.Where(row => row.CheckedInAt >= filter.From && row.CheckedInAt < filter.Until);
        }
        if (filter.Search.Length > 0)
        {
            var pattern = LiteralSearch.Pattern(filter.Search);
            // Vehicle as the table shows it (year included), VIN and technician; branch names only for the group scope.
            var searchBranch = actor.Role == AccountRoles.GeneralManager;
            work = work.Where(row => EF.Functions.ILike(row.RegistrationPlate, pattern, "!") ||
                EF.Functions.ILike(row.Vin, pattern, "!") ||
                EF.Functions.ILike(row.ManufactureYear + " " + row.Make + " " + row.Model + " " + row.Trim, pattern, "!") ||
                row.TechnicianIdentity != null && EF.Functions.ILike(row.TechnicianIdentity, pattern, "!") ||
                row.TechnicianEmail != null && EF.Functions.ILike(row.TechnicianEmail, pattern, "!") ||
                searchBranch && EF.Functions.ILike(row.BranchName, pattern, "!"));
        }

        return work;
    }

    private static WorkshopQueueItem[] Items(IEnumerable<WorkshopRow> page)
    {
        return [.. page.Select(row => new WorkshopQueueItem(row.VehicleId, row.BranchId, row.BranchName,
            row.RegistrationPlate, row.Make, row.Model, row.VehicleStatus.ToString(), row.InspectionId,
            row.TechnicianId, row.TechnicianIdentity, row.Revision, row.RequestId, row.Budget,
            row.Since, row.RequestId is null ? "CheckedIn" : "Requested", WorkshopRows.Status(row),
            row.ManufactureYear, row.Trim, row.CheckedInAt, row.RequestedAt, row.TechnicianPictureId))];
    }

    private static IQueryable<WorkshopRow> FilterColumns(IQueryable<WorkshopRow> rows, InspectionQueueFilter filter)
    {
        if (filter.Registration.Length > 0)
        {
            var pattern = LiteralSearch.Pattern(filter.Registration);
            rows = rows.Where(row => EF.Functions.ILike(row.RegistrationPlate, pattern, "!"));
        }
        if (filter.Vehicle.Length > 0)
        {
            var pattern = LiteralSearch.Pattern(filter.Vehicle);
            rows = rows.Where(row => EF.Functions.ILike(row.ManufactureYear + " " + row.Make + " " + row.Model + " " + row.Trim, pattern, "!"));
        }
        rows = filter.WorkStatus switch
        {
            "ReadyForSale" => rows.Where(row => row.VehicleStatus == VehicleStatus.Available),
            "Rejected" => rows.Where(row => row.VehicleStatus == VehicleStatus.Rejected),
            "AwaitingInspection" => rows.Where(row => row.VehicleStatus == VehicleStatus.PendingInspection && row.InspectionId == null),
            "InspectionUnderway" => rows.Where(row => row.VehicleStatus == VehicleStatus.PendingInspection && row.InspectionId != null && !row.IsSigned),
            "RepairRequestRequired" => rows.Where(row => row.VehicleStatus == VehicleStatus.PendingInspection && row.IsSigned && (row.RequestId == null || row.Completed)),
            "ApprovalRequired" => rows.Where(row => row.VehicleStatus == VehicleStatus.PendingInspection && row.IsSigned && row.RequestId != null && !row.Completed && row.Budget > 0 && !row.Approved),
            "RepairsToComplete" => rows.Where(row => row.VehicleStatus == VehicleStatus.PendingInspection && row.IsSigned && row.RequestId != null && !row.Completed && (row.Budget == 0 || row.Approved)),
            _ => rows
        };
        if (filter.Technician == "unassigned")
        {
            rows = rows.Where(row => row.TechnicianId == null);
        }
        else if (filter.Technician.Length > 0)
        {
            var pattern = LiteralSearch.Pattern(filter.Technician);
            rows = rows.Where(row => row.TechnicianIdentity != null && EF.Functions.ILike(row.TechnicianIdentity, pattern, "!") ||
                row.TechnicianEmail != null && EF.Functions.ILike(row.TechnicianEmail, pattern, "!"));
        }
        if (filter.MinimumBudget is not null)
        {
            rows = rows.Where(row => row.Budget >= filter.MinimumBudget);
        }
        if (filter.MaximumBudget is not null)
        {
            rows = rows.Where(row => row.Budget <= filter.MaximumBudget);
        }
        return rows;
    }

    private static IOrderedQueryable<WorkshopRow> Order(IQueryable<WorkshopRow> rows, InspectionQueueFilter filter)
    {
        return filter.Sort switch
        {
            "registrationPlate" => OrderBy(rows, row => row.RegistrationPlate, filter.Direction),
            "vehicle" => OrderBy(rows, row => row.ManufactureYear + " " + row.Make + " " + row.Model + " " + row.Trim, filter.Direction),
            "technicianIdentity" => OrderBy(rows, row => row.TechnicianIdentity ?? string.Empty, filter.Direction),
            "branchName" => OrderBy(rows, row => row.BranchName, filter.Direction),
            "budget" => OrderBudget(rows, filter.Direction),
            "checkedInAt" => OrderBy(rows, row => row.CheckedInAt, filter.Direction),
            "requestedAt" => OrderRequestDate(rows, filter.Direction),
            "status" => OrderStatus(rows, filter.Direction),
            _ => OrderBy(rows, row => row.Since, filter.Direction)
        };
    }

    private static IOrderedQueryable<WorkshopRow> OrderBy<TKey>(IQueryable<WorkshopRow> rows,
        Expression<Func<WorkshopRow, TKey>> key, string direction)
    {
        return direction == "desc" ? rows.OrderByDescending(key) : rows.OrderBy(key);
    }

    private static IOrderedQueryable<WorkshopRow> OrderBudget(IQueryable<WorkshopRow> rows, string direction)
    {
        var ordered = rows.OrderBy(row => row.Budget == null);
        return direction == "desc"
            ? ordered.ThenByDescending(row => row.Budget)
            : ordered.ThenBy(row => row.Budget);
    }

    private static IOrderedQueryable<WorkshopRow> OrderRequestDate(IQueryable<WorkshopRow> rows, string direction)
    {
        var ordered = rows.OrderBy(row => row.RequestedAt == null);
        return direction == "desc"
            ? ordered.ThenByDescending(row => row.RequestedAt)
            : ordered.ThenBy(row => row.RequestedAt);
    }

    private static IOrderedQueryable<WorkshopRow> OrderStatus(IQueryable<WorkshopRow> rows, string direction)
    {
        // These stages follow WorkshopWorkStatus; terminal vehicle outcomes take precedence.
        if (direction == "desc")
        {
            return rows.OrderByDescending(row => row.VehicleStatus == VehicleStatus.Rejected)
                .ThenByDescending(row => row.VehicleStatus == VehicleStatus.Available)
                .ThenByDescending(row => row.InspectionId != null)
                .ThenByDescending(row => row.IsSigned)
                .ThenByDescending(row => row.RequestId != null && !row.Completed)
                .ThenByDescending(row => row.Budget == 0 || row.Approved);
        }

        return rows.OrderBy(row => row.VehicleStatus == VehicleStatus.Rejected)
            .ThenBy(row => row.VehicleStatus == VehicleStatus.Available)
            .ThenBy(row => row.InspectionId != null)
            .ThenBy(row => row.IsSigned)
            .ThenBy(row => row.RequestId != null && !row.Completed)
            .ThenBy(row => row.Budget == 0 || row.Approved);
    }

}
