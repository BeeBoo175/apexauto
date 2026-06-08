using Dealership.Application.Accounts;
using Dealership.Application.Inspections;
using Dealership.Application.WorkCalendar;
using Dealership.Domain.Sales;
using Dealership.Infrastructure.Inspections;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.WorkCalendar;

internal static class UnscheduledWorkRows
{
    public static async Task<UnscheduledWorkPage> ReadAsync(DealershipDbContext database,
        WorkCalendarReadScope scope, UnscheduledWorkQuery query, CancellationToken cancellationToken)
    {
        var task = scope.IsManager ? WorkshopTask.Active : WorkshopTask.MyWork;
        var workshop = WorkshopRows.ForTask(database, scope.Actor, scope.BranchId, task)
            .Where(row => row.TechnicianId != null);
        var sales = SaleRows.Project(database, database.SalesOrders.AsNoTracking().Where(sale =>
            (scope.BranchId == null || sale.BranchId == scope.BranchId) && sale.AgentId != null &&
            (sale.Status == SalesOrderStatus.Handling || sale.Status == SalesOrderStatus.Approved ||
                sale.Status == SalesOrderStatus.Settled || sale.Status == SalesOrderStatus.PendingApproval)));
        if (scope.Actor.Role == AccountRoles.SalesAgent)
        {
            workshop = workshop.Where(row => false);
            sales = sales.Where(row => row.AgentId == scope.Actor.Id && row.Status != SalesOrderStatus.PendingApproval);
        }
        if (scope.Employee == "unassigned")
        {
            // Unclaimed work belongs to feature queues, not the owned unscheduled list.
            workshop = workshop.Where(row => false);
            sales = sales.Where(row => false);
        }
        else if (scope.Employee.Length > 0)
        {
            var pattern = LiteralSearch.Pattern(scope.Employee);
            workshop = workshop.Where(row => row.TechnicianIdentity != null &&
                EF.Functions.ILike(row.TechnicianIdentity, pattern, "!"));
            sales = sales.Where(row => row.Agent != null && EF.Functions.ILike(row.Agent, pattern, "!"));
        }

        if (query.Search.Length > 0)
        {
            var pattern = LiteralSearch.Pattern(query.Search);
            workshop = workshop.Where(row => EF.Functions.ILike(row.RegistrationPlate, pattern, "!") ||
                EF.Functions.ILike(row.ManufactureYear + " " + row.Make + " " + row.Model + " " + row.Trim, pattern, "!") ||
                row.TechnicianIdentity != null && EF.Functions.ILike(row.TechnicianIdentity, pattern, "!"));
            sales = SaleRows.Search(sales, query.Search, scope.Actor.Role == AccountRoles.GeneralManager);
        }

        var workshopTotal = await workshop.CountAsync(cancellationToken);
        var salesTotal = await sales.CountAsync(cancellationToken);
        var counts = new UnscheduledWorkCounts(workshopTotal + salesTotal, workshopTotal, salesTotal);
        if (query.Kind == "Sale")
        {
            workshop = workshop.Where(row => false);
        }
        if (query.Kind == "Workshop")
        {
            sales = sales.Where(row => false);
        }
        var total = query.Kind switch
        {
            "Workshop" => counts.Workshop,
            "Sale" => counts.Sale,
            _ => counts.All
        };
        var keys = workshop.Select(row => new { Id = row.VehicleId, Kind = "Workshop", Registration = row.RegistrationPlate })
            .Concat(sales.Select(row => new { row.Id, Kind = "Sale", Registration = row.Plate }));
        var page = await keys.OrderBy(row => row.Registration).ThenBy(row => row.Kind).ThenBy(row => row.Id)
            .ReadPageAsync(query.Page, query.PageSize, total, cancellationToken);
        var workshopIds = page.Where(row => row.Kind == "Workshop").Select(row => row.Id).ToArray();
        var saleIds = page.Where(row => row.Kind == "Sale").Select(row => row.Id).ToArray();
        var jobs = await workshop.Where(row => workshopIds.Contains(row.VehicleId)).ToArrayAsync(cancellationToken);
        var orders = await sales.Where(row => saleIds.Contains(row.Id)).ToArrayAsync(cancellationToken);
        var items = jobs.Select(row => new UnscheduledWorkItem(row.VehicleId, "Workshop", row.BranchId,
            row.BranchName, row.RegistrationPlate, $"{row.ManufactureYear} {row.Make} {row.Model} {row.Trim}",
            WorkshopRows.Status(row).ToString(), row.TechnicianId!.Value, row.TechnicianIdentity ?? string.Empty,
            row.InspectionId))
            .Concat(orders.Select(row => new UnscheduledWorkItem(row.Id, "Sale", row.BranchId, row.Branch,
                row.Plate, $"{row.Year} {row.Make} {row.Model} {row.Trim}", row.Status.ToString(), row.AgentId!.Value,
                row.Agent ?? string.Empty, null))).ToDictionary(row => (row.Kind, row.Id));
        return new(scope.CurrentActor, page.Select(row => items[(row.Kind, row.Id)]).ToArray(), total,
            query.Page, query.PageSize, scope.BranchId, scope.Employee, counts);
    }
}
