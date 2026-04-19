using System.Data;
using System.Linq.Expressions;
using Dealership.Application.Accounts;
using Dealership.Application.Inspections;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Persistence;
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
        await transaction.CommitAsync(cancellationToken);
        return new(InspectionAssignmentStatus.Succeeded, new(Array.AsReadOnly(items), total, filter.Page, filter.PageSize));
    }

    public async Task<WorkshopJobsResult> ReadJobsAsync(Guid actorId, InspectionQueueFilter filter,
        WorkshopTask task, string? after, CancellationToken cancellationToken)
    {
        if (!filter.IsValid || filter.Page != 1 || filter.BranchId is not null || filter.From is not null ||
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

        var cursor = after is null ? null : WorkshopQueueCursor.Parse(after);
        if (after is not null && (cursor is null || !cursor.Matches(actorId, actor.BranchId, filter, task)))
        {
            return new(InspectionAssignmentStatus.Invalid);
        }

        var work = Filter(actor, filter, task);
        var total = await work.CountAsync(cancellationToken);
        if (cursor is not null)
        {
            // Use PostgreSQL's timestamp/uuid order for both the boundary and the ordered rows.
            work = work.Where(row => EF.Functions.LessThan(
                ValueTuple.Create(row.Since, row.VehicleId), ValueTuple.Create(cursor.Since, cursor.VehicleId)));
        }

        var rows = await work.OrderByDescending(row => row.Since).ThenByDescending(row => row.VehicleId)
            .Take(filter.PageSize + 1).ToArrayAsync(cancellationToken);
        var page = rows.Take(filter.PageSize).ToArray();
        string? nextCursor = null;
        if (rows.Length > filter.PageSize)
        {
            var last = page[^1];
            nextCursor = new WorkshopQueueCursor(1, actorId, actor.BranchId, task,
                filter.Search, filter.PageSize, last.Since, last.VehicleId).Encode();
        }

        await transaction.CommitAsync(cancellationToken);
        return new(InspectionAssignmentStatus.Succeeded,
            new(Array.AsReadOnly(Items(page)), total, filter.PageSize, after, nextCursor));
    }

    private IQueryable<WorkshopRow> Filter(AccountUser actor, InspectionQueueFilter filter, WorkshopTask task)
    {
        var work = Rows(actor, filter.BranchId);
        work = task switch
        {
            WorkshopTask.Active => work.Where(row => row.VehicleStatus == VehicleStatus.PendingInspection),
            WorkshopTask.Inspection => work.Where(row => row.VehicleStatus == VehicleStatus.PendingInspection && !row.IsSigned),
            WorkshopTask.Approval => work.Where(row => row.VehicleStatus == VehicleStatus.PendingInspection &&
                row.IsSigned && row.RequestId != null && !row.Completed && row.Budget > 0 && !row.Approved),
            WorkshopTask.Repairs => work.Where(row => row.VehicleStatus == VehicleStatus.PendingInspection &&
                row.IsSigned && (row.RequestId == null || row.Completed || row.Budget == 0 || row.Approved)),
            WorkshopTask.MyWork => work.Where(row => row.TechnicianId == actor.Id &&
                row.VehicleStatus == VehicleStatus.PendingInspection &&
                (!row.IsSigned || row.RequestId == null || row.Completed || row.Budget == 0 || row.Approved)),
            WorkshopTask.Waiting => work.Where(row => row.TechnicianId == actor.Id &&
                row.VehicleStatus == VehicleStatus.PendingInspection && row.IsSigned &&
                row.RequestId != null && !row.Completed && row.Budget > 0 && !row.Approved),
            WorkshopTask.Claimable => work.Where(row => row.InspectionId == null &&
                row.VehicleStatus == VehicleStatus.PendingInspection),
            _ => work
        };
        if (filter.From is not null)
        {
            work = task is WorkshopTask.Approval or WorkshopTask.Repairs
                ? work.Where(row => row.RequestedAt >= filter.From && row.RequestedAt < filter.Until)
                : work.Where(row => row.CheckedInAt >= filter.From && row.CheckedInAt < filter.Until);
        }
        if (filter.Search.Length > 0)
        {
            var pattern = LiteralSearch.Pattern(filter.Search);
            work = work.Where(row => EF.Functions.ILike(row.RegistrationPlate, pattern, "!") ||
                EF.Functions.ILike(row.Make, pattern, "!") || EF.Functions.ILike(row.Model, pattern, "!") ||
                EF.Functions.ILike(row.Trim, pattern, "!") ||
                EF.Functions.ILike(row.BranchName, pattern, "!") ||
                row.TechnicianIdentity != null && EF.Functions.ILike(row.TechnicianIdentity, pattern, "!"));
        }

        return work;
    }

    private static WorkshopQueueItem[] Items(IEnumerable<WorkshopRow> page)
    {
        return page.Select(row => new WorkshopQueueItem(row.VehicleId, row.BranchId, row.BranchName,
            row.RegistrationPlate, row.Make, row.Model, row.VehicleStatus.ToString(), row.InspectionId,
            row.TechnicianId, row.TechnicianIdentity, row.Revision, row.RequestId, row.Budget,
            row.Since, row.RequestId is null ? "CheckedIn" : "Requested", Status(row),
            row.ManufactureYear, row.Trim, row.CheckedInAt, row.RequestedAt)).ToArray();
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
            rows = rows.Where(row => row.TechnicianIdentity != null && EF.Functions.ILike(row.TechnicianIdentity, pattern, "!"));
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

    private IQueryable<WorkshopRow> Rows(AccountUser actor, Guid? selectedBranch)
    {
        var branchId = actor.BranchId ?? selectedBranch;
        var covered = InspectionRepairAuthority.CoveredRequests(database);
        return from vehicle in database.Vehicles.AsNoTracking()
               join branch in database.Branches.AsNoTracking() on vehicle.BranchId equals branch.Id
               join inspection in database.InspectionDrafts.AsNoTracking() on vehicle.Id equals inspection.VehicleId into inspections
               from draft in inspections.DefaultIfEmpty()
               join assigned in database.Users.AsNoTracking() on draft.TechnicianId equals assigned.Id into technicians
               from technician in technicians.DefaultIfEmpty()
               from request in database.InspectionRepairRequests.AsNoTracking().Where(item =>
                   draft != null && draft.IsSigned && item.InspectionId == draft.Id && item.Revision == draft.Revision &&
                   item.AssignmentVersion == draft.AssignmentVersion && item.Version == draft.RepairVersion).DefaultIfEmpty()
               where (branchId == null || vehicle.BranchId == branchId) &&
                   (actor.Role != AccountRoles.WorkshopTechnician || draft.TechnicianId == actor.Id ||
                       draft == null && vehicle.Status == VehicleStatus.PendingInspection)
               select new WorkshopRow
               {
                   VehicleId = vehicle.Id,
                   BranchId = vehicle.BranchId,
                   BranchName = branch.Name,
                   RegistrationPlate = vehicle.RegistrationPlate,
                   Make = vehicle.Make,
                   Model = vehicle.Model,
                   ManufactureYear = vehicle.ManufactureYear,
                   Trim = vehicle.Trim,
                   CheckedInAt = vehicle.CreatedAt,
                   RequestedAt = request == null ? null : request.RequestedAt,
                   VehicleStatus = vehicle.Status,
                   InspectionId = draft == null ? null : draft.Id,
                   TechnicianId = draft == null ? null : draft.TechnicianId,
                   TechnicianIdentity = technician == null ? null : technician.UserName,
                   Revision = draft == null ? null : draft.Revision,
                   IsSigned = draft != null && draft.IsSigned,
                   RequestId = request == null ? null : request.Id,
                   Budget = request == null ? null : request.Budget,
                   Since = request == null ? vehicle.CreatedAt : request.RequestedAt,
                   Completed = request != null && database.InspectionRepairCompletions.Any(item => item.RequestId == request.Id),
                   Approved = request != null && covered.Any(item => item.Request.Id == request.Id)
               };
    }

    private static WorkshopWorkStatus Status(WorkshopRow row)
    {
        // Availability is decided by the canonical completion/sign-off services, including partial scope.
        if (row.VehicleStatus == VehicleStatus.Available)
        {
            return WorkshopWorkStatus.ReadyForSale;
        }
        if (row.VehicleStatus == VehicleStatus.Rejected)
        {
            return WorkshopWorkStatus.Rejected;
        }
        if (row.InspectionId is null)
        {
            return WorkshopWorkStatus.AwaitingInspection;
        }
        if (!row.IsSigned)
        {
            return WorkshopWorkStatus.InspectionUnderway;
        }
        if (row.RequestId is null || row.Completed)
        {
            return WorkshopWorkStatus.RepairRequestRequired;
        }
        if (row.Budget > 0 && !row.Approved)
        {
            return WorkshopWorkStatus.ApprovalRequired;
        }

        return WorkshopWorkStatus.RepairsToComplete;
    }

    private sealed class WorkshopRow
    {
        public Guid VehicleId { get; init; }
        public Guid BranchId { get; init; }
        public string BranchName { get; init; } = string.Empty;
        public string RegistrationPlate { get; init; } = string.Empty;
        public string Make { get; init; } = string.Empty;
        public string Model { get; init; } = string.Empty;
        public int ManufactureYear { get; init; }
        public string Trim { get; init; } = string.Empty;
        public DateTimeOffset CheckedInAt { get; init; }
        public DateTimeOffset? RequestedAt { get; init; }
        public VehicleStatus VehicleStatus { get; init; }
        public Guid? InspectionId { get; init; }
        public Guid? TechnicianId { get; init; }
        public string? TechnicianIdentity { get; init; }
        public long? Revision { get; init; }
        public bool IsSigned { get; init; }
        public Guid? RequestId { get; init; }
        public decimal? Budget { get; init; }
        public DateTimeOffset Since { get; init; }
        public bool Completed { get; init; }
        public bool Approved { get; init; }
    }
}
