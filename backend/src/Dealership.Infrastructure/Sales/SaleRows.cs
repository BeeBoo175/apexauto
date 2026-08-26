using System.Linq.Expressions;
using Dealership.Application.Sales;
using Dealership.Domain.Sales;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Sales;

// Member-initialised so filters, search and orderings translate to SQL over the sale's current columns.
internal sealed class SaleRow
{
    public Guid Id { get; init; }
    public string ReservationNumber { get; init; } = string.Empty;
    public string? OrderNumber { get; init; }
    public SalesOrderStatus Status { get; init; }
    public Guid BranchId { get; init; }
    public string Branch { get; init; } = string.Empty;
    public string Plate { get; init; } = string.Empty;
    public string Vin { get; init; } = string.Empty;
    public int Year { get; init; }
    public string Make { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public string Trim { get; init; } = string.Empty;
    public string Customer { get; init; } = string.Empty;
    public Guid CustomerId { get; init; }
    public string Mobile { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string? Promo { get; init; }
    public Guid? AgentId { get; init; }
    public string? Agent { get; init; }
    public string? AgentName { get; init; }
    public Guid? AgentPictureId { get; init; }
    public bool AgentSuspended { get; init; }
    public bool CustomerSuspended { get; init; }
    public DateTimeOffset? DueAt { get; init; }
    public long? PausedSeconds { get; init; }
    public long TotalHoldSeconds { get; init; }
    public DateTimeOffset? ClaimedAt { get; init; }
    public decimal NegotiatedDiscount { get; init; }
    public decimal? DiscountLimitAtSubmit { get; init; }
    public decimal BalancePayable { get; init; }
    public DateTimeOffset? SubmittedAt { get; init; }
    public DateTimeOffset? DecidedAt { get; init; }
    public DateTimeOffset? SettledAt { get; init; }
    public DateTimeOffset? ClosedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public long Version { get; init; }
    public Guid? VehicleId { get; init; }
    // A deposit request shown in the sale row shape; it is not a sale and has no order status.
    public bool IsDepositRequest { get; init; }
}

internal static class SaleRows
{
    public static IQueryable<SaleRow> Project(DealershipDbContext database, IQueryable<SalesOrder> sales) =>
        from sale in sales
        join reservation in database.Reservations.AsNoTracking() on sale.Id equals reservation.Id
        join vehicle in database.Vehicles.AsNoTracking() on sale.VehicleId equals vehicle.Id
        join profile in database.CustomerProfiles.AsNoTracking() on sale.CustomerId equals profile.UserId
        join customer in database.Users.AsNoTracking() on sale.CustomerId equals customer.Id
        join branch in database.Branches.AsNoTracking() on sale.BranchId equals branch.Id
        join handler in database.Users.AsNoTracking() on sale.AgentId equals handler.Id into handlers
        from agent in handlers.DefaultIfEmpty()
        select new SaleRow
        {
            Id = sale.Id,
            ReservationNumber = reservation.Number,
            OrderNumber = sale.OrderNumber,
            Status = sale.Status,
            BranchId = sale.BranchId,
            Branch = branch.Name,
            Plate = vehicle.RegistrationPlate,
            Vin = vehicle.Vin,
            Year = vehicle.ManufactureYear,
            Make = vehicle.Make,
            Model = vehicle.Model,
            Trim = vehicle.Trim,
            Customer = profile.LegalName,
            CustomerId = sale.CustomerId,
            Mobile = profile.Mobile,
            Email = customer.Email,
            Promo = sale.PromoCode,
            AgentId = sale.AgentId,
            // The SQL left join returns a null email when no agent is assigned.
            Agent = agent.Email,
            AgentName = database.StaffProfiles.Where(profile => profile.UserId == sale.AgentId)
                .Select(profile => profile.Name).FirstOrDefault(),
            AgentPictureId = database.AccountPictures
                .Where(picture => picture.UserId == sale.AgentId && picture.RemovedAt == null)
                .Select(picture => (Guid?)picture.Id).FirstOrDefault(),
            AgentSuspended = agent.SuspendedAt != null,
            CustomerSuspended = customer.SuspendedAt != null,
            DueAt = sale.DueAt,
            PausedSeconds = sale.PausedSeconds,
            TotalHoldSeconds = (long)(reservation.ExpiresAt - reservation.CreatedAt).TotalSeconds,
            ClaimedAt = sale.ClaimedAt,
            NegotiatedDiscount = sale.NegotiatedDiscount,
            DiscountLimitAtSubmit = sale.DiscountLimitAtSubmit,
            BalancePayable = sale.BalancePayable,
            SubmittedAt = sale.SubmittedAt,
            DecidedAt = sale.DecidedAt,
            SettledAt = sale.SettledAt,
            ClosedAt = sale.ClosedAt,
            UpdatedAt = sale.UpdatedAt,
            Version = sale.Version
        };

    // Deposit requests awaiting their customer, in the sale row shape so the table's search, column filters and sorting
    // apply unchanged: the booking staff member stands in as the agent and the request's lapse time as the due time.
    public static IQueryable<SaleRow> ProjectRequests(DealershipDbContext database,
        IQueryable<ReservationDepositRequest> requests) =>
        from request in requests
        join vehicle in database.Vehicles.AsNoTracking() on request.VehicleId equals vehicle.Id
        join branch in database.Branches.AsNoTracking() on request.BranchId equals branch.Id
        join profile in database.CustomerProfiles.AsNoTracking() on request.CustomerId equals profile.UserId
        join customer in database.Users.AsNoTracking() on request.CustomerId equals customer.Id
        join staff in database.Users.AsNoTracking() on request.BookedByStaffId equals (Guid?)staff.Id
        select new SaleRow
        {
            Id = request.Id,
            ReservationNumber = "Deposit request",
            Status = SalesOrderStatus.Open,
            BranchId = request.BranchId,
            Branch = branch.Name,
            Plate = vehicle.RegistrationPlate,
            Vin = vehicle.Vin,
            Year = vehicle.ManufactureYear,
            Make = vehicle.Make,
            Model = vehicle.Model,
            Trim = vehicle.Trim,
            Customer = profile.LegalName,
            CustomerId = request.CustomerId,
            Mobile = profile.Mobile,
            Email = customer.Email,
            AgentId = request.BookedByStaffId,
            Agent = staff.Email,
            AgentName = database.StaffProfiles.Where(item => item.UserId == request.BookedByStaffId)
                .Select(item => item.Name).FirstOrDefault(),
            AgentPictureId = database.AccountPictures
                .Where(picture => picture.UserId == request.BookedByStaffId && picture.RemovedAt == null)
                .Select(picture => (Guid?)picture.Id).FirstOrDefault(),
            AgentSuspended = staff.SuspendedAt != null,
            CustomerSuspended = customer.SuspendedAt != null,
            Promo = (string?)null,
            OrderNumber = (string?)null,
            DueAt = request.ExpiresAt,
            TotalHoldSeconds = (long)(request.ExpiresAt - request.CreatedAt).TotalSeconds,
            PausedSeconds = (long?)null,
            ClaimedAt = (DateTimeOffset?)null,
            NegotiatedDiscount = 0m,
            DiscountLimitAtSubmit = (decimal?)null,
            BalancePayable = 0m,
            SubmittedAt = (DateTimeOffset?)null,
            DecidedAt = (DateTimeOffset?)null,
            SettledAt = (DateTimeOffset?)null,
            ClosedAt = (DateTimeOffset?)null,
            UpdatedAt = request.CreatedAt,
            Version = 1,
            VehicleId = vehicle.Id,
            IsDepositRequest = true
        };

    // One box for the common lookups: numbers, car, customer and contact, promo and agent; branch for the group scope.
    public static IQueryable<SaleRow> Search(IQueryable<SaleRow> rows, string search, bool searchBranch)
    {
        var pattern = LiteralSearch.Pattern(search);
        return rows.Where(row =>
            EF.Functions.ILike(row.ReservationNumber, pattern, "!") ||
            row.OrderNumber != null && EF.Functions.ILike(row.OrderNumber, pattern, "!") ||
            EF.Functions.ILike(row.Plate, pattern, "!") ||
            EF.Functions.ILike(row.Vin, pattern, "!") ||
            EF.Functions.ILike(row.Year + " " + row.Make + " " + row.Model + " " + row.Trim, pattern, "!") ||
            EF.Functions.ILike(row.Customer, pattern, "!") ||
            EF.Functions.ILike(row.Mobile, pattern, "!") ||
            row.Email != null && EF.Functions.ILike(row.Email, pattern, "!") ||
            row.Promo != null && EF.Functions.ILike(row.Promo, pattern, "!") ||
            row.Agent != null && EF.Functions.ILike(row.Agent, pattern, "!") ||
            row.AgentName != null && EF.Functions.ILike(row.AgentName, pattern, "!") ||
            searchBranch && EF.Functions.ILike(row.Branch, pattern, "!"));
    }

    public static SaleSummary Summary(SaleRow row) => new(row.Id, row.ReservationNumber, row.OrderNumber,
        row.IsDepositRequest ? SaleViews.AwaitingDeposit : row.Status.ToString(), row.Plate, row.Year, row.Make, row.Model, row.Trim, row.Customer,
        row.Agent, row.Branch, row.DueAt, row.PausedSeconds, row.ClaimedAt, row.NegotiatedDiscount,
        row.DiscountLimitAtSubmit, row.BalancePayable, row.SubmittedAt, row.DecidedAt, row.ClosedAt,
        !row.IsDepositRequest && SalesOrder.IsClosedStatus(row.Status) ? row.Status.ToString() : null,
        row.Version, row.UpdatedAt, row.TotalHoldSeconds, row.AgentName, row.AgentPictureId, row.AgentId,
        row.AgentSuspended, row.CustomerSuspended, row.CustomerId, row.VehicleId);

    // Compare remaining time before counting and paging, using one instant for both bounds.
    public static IQueryable<SaleRow> TimeLeftBetween(IQueryable<SaleRow> rows, decimal? minimum,
        decimal? maximum, DateTimeOffset now)
    {
        if (minimum is not null || maximum is not null)
        {
            rows = rows.Where(row => row.ClosedAt == null);
        }

        if (minimum is { } least)
        {
            var due = now.AddHours((double)least);
            var seconds = least * 3600;
            rows = rows.Where(row => row.PausedSeconds != null
                ? row.PausedSeconds >= seconds
                : row.DueAt >= due);
        }

        if (maximum is { } most)
        {
            var due = now.AddHours((double)most);
            var seconds = most * 3600;
            rows = rows.Where(row => row.PausedSeconds != null
                ? row.PausedSeconds <= seconds
                : row.DueAt <= due);
        }

        return rows;
    }

    // Every ordering ends in the sale identity, so paging never repeats or skips a sale.
    public static IOrderedQueryable<SaleRow> Order(IQueryable<SaleRow> rows, string sort, bool down) => sort switch
    {
        "reference" => By(rows, row => row.OrderNumber ?? row.ReservationNumber, down),
        "registration" => By(rows, row => row.Plate, down),
        "vehicle" => By(rows, row => row.Year + " " + row.Make + " " + row.Model + " " + row.Trim, down),
        "customer" => By(rows, row => row.Customer, down),
        "agent" => By(rows, row => row.Agent, down),
        "branch" => By(rows, row => row.Branch, down),
        // Status sorts in lifecycle order, which is the order SalesOrderStatus declares.
        "status" => By(rows, row => row.Status, down),
        "discount" => By(rows, row => row.NegotiatedDiscount, down),
        "submitted" => By(rows, row => row.SubmittedAt, down),
        "due" => By(rows, row => row.DueAt, down),
        "claimed" => By(rows, row => row.ClaimedAt, down),
        "paused" => By(rows, row => row.PausedSeconds, down),
        "balance" => By(rows, row => row.BalancePayable, down),
        "decided" => By(rows, row => row.DecidedAt, down),
        "settled" => By(rows, row => row.SettledAt, down),
        "updated" => By(rows, row => row.UpdatedAt, down),
        _ => By(rows, row => row.ClosedAt, down)
    };

    private static IOrderedQueryable<SaleRow> By<TKey>(IQueryable<SaleRow> rows,
        Expression<Func<SaleRow, TKey>> key, bool down) =>
        (down ? rows.OrderByDescending(key) : rows.OrderBy(key)).ThenBy(row => row.Id);
}
