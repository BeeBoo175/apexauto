using Dealership.Application.Sales;
using Dealership.Domain.Sales;
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
    public string Mobile { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string? Promo { get; init; }
    public Guid? AgentId { get; init; }
    public string? Agent { get; init; }
    public DateTimeOffset? DueAt { get; init; }
    public long? PausedSeconds { get; init; }
    public DateTimeOffset? ClaimedAt { get; init; }
    public decimal NegotiatedDiscount { get; init; }
    public decimal? DiscountLimitAtSubmit { get; init; }
    public decimal BalancePayable { get; init; }
    public DateTimeOffset? SubmittedAt { get; init; }
    public DateTimeOffset? DecidedAt { get; init; }
    public DateTimeOffset? ClosedAt { get; init; }
    public long Version { get; init; }
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
            Mobile = profile.Mobile,
            Email = customer.Email,
            Promo = sale.PromoCode,
            AgentId = sale.AgentId,
            Agent = agent == null ? null : agent.Email,
            DueAt = sale.DueAt,
            PausedSeconds = sale.PausedSeconds,
            ClaimedAt = sale.ClaimedAt,
            NegotiatedDiscount = sale.NegotiatedDiscount,
            DiscountLimitAtSubmit = sale.DiscountLimitAtSubmit,
            BalancePayable = sale.BalancePayable,
            SubmittedAt = sale.SubmittedAt,
            DecidedAt = sale.DecidedAt,
            ClosedAt = sale.ClosedAt,
            Version = sale.Version
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
            searchBranch && EF.Functions.ILike(row.Branch, pattern, "!"));
    }

    public static SaleSummary Summary(SaleRow row) => new(row.Id, row.ReservationNumber, row.OrderNumber,
        row.Status.ToString(), row.Plate, row.Year, row.Make, row.Model, row.Trim, row.Customer,
        row.Agent, row.Branch, row.DueAt, row.PausedSeconds, row.ClaimedAt, row.NegotiatedDiscount,
        row.DiscountLimitAtSubmit, row.BalancePayable, row.SubmittedAt, row.DecidedAt, row.ClosedAt,
        row.Status is SalesOrderStatus.Expired or SalesOrderStatus.Cancelled ? row.Status.ToString() : null,
        row.Version);

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
        // As the column shows it: the excess over the submitted limit, or nothing without a limit or an excess.
        "overLimit" => By(rows, row => row.DiscountLimitAtSubmit != null && row.NegotiatedDiscount > row.DiscountLimitAtSubmit
            ? row.NegotiatedDiscount - row.DiscountLimitAtSubmit.Value
            : 0m, down),
        "submitted" => By(rows, row => row.SubmittedAt, down),
        "due" => By(rows, row => row.DueAt, down),
        "claimed" => By(rows, row => row.ClaimedAt, down),
        "balance" => By(rows, row => row.BalancePayable, down),
        "decided" => By(rows, row => row.DecidedAt, down),
        _ => By(rows, row => row.ClosedAt, down)
    };

    private static IOrderedQueryable<SaleRow> By<TKey>(IQueryable<SaleRow> rows,
        System.Linq.Expressions.Expression<Func<SaleRow, TKey>> key, bool down) =>
        (down ? rows.OrderByDescending(key) : rows.OrderBy(key)).ThenBy(row => row.Id);
}
