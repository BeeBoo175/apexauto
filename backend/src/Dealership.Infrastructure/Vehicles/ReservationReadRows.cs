using System.Linq.Expressions;
using Dealership.Application.Vehicles;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Vehicles;

internal static class ReservationReadRows
{
    public static IQueryable<Row> Filter(IQueryable<Row> rows, ReservationReadFilter filter)
    {
        if (filter.Search.Length > 0)
        {
            var pattern = LiteralSearch.Pattern(filter.Search);
            rows = rows.Where(row => EF.Functions.ILike(row.RegistrationPlate, pattern, "!") ||
                EF.Functions.ILike(row.ManufactureYear + " " + row.Make + " " + row.Model + " " + row.Trim, pattern, "!") ||
                EF.Functions.ILike(row.Reservation.Buyer.LegalName, pattern, "!") ||
                EF.Functions.ILike(row.BranchName, pattern, "!") ||
                EF.Functions.ILike(row.Reservation.Id.ToString(), pattern, "!"));
        }
        if (filter.Registration.Length > 0)
        {
            var pattern = LiteralSearch.Pattern(filter.Registration);
            rows = rows.Where(row => EF.Functions.ILike(row.RegistrationPlate, pattern, "!"));
        }
        if (filter.Vehicle.Length > 0)
        {
            var pattern = LiteralSearch.Pattern(filter.Vehicle);
            rows = rows.Where(row => EF.Functions.ILike(
                row.ManufactureYear + " " + row.Make + " " + row.Model + " " + row.Trim, pattern, "!"));
        }
        if (filter.Buyer.Length > 0)
        {
            var pattern = LiteralSearch.Pattern(filter.Buyer);
            rows = rows.Where(row => EF.Functions.ILike(row.Reservation.Buyer.LegalName, pattern, "!"));
        }
        if (filter.Status == "Active")
        {
            rows = rows.Where(row => row.Reservation.IsActive);
        }
        else if (filter.Status.Length > 0)
        {
            var kind = Enum.Parse<ReservationReleaseKind>(filter.Status);
            rows = rows.Where(row => row.Release != null && row.Release.Kind == kind);
        }
        if (filter.MinimumTotal is not null)
        {
            rows = rows.Where(row => row.Reservation.Price.Total >= filter.MinimumTotal);
        }
        if (filter.MaximumTotal is not null)
        {
            rows = rows.Where(row => row.Reservation.Price.Total <= filter.MaximumTotal);
        }
        if (filter.CreatedFrom is not null)
        {
            rows = rows.Where(row => row.Reservation.CreatedAt >= filter.CreatedFrom && row.Reservation.CreatedAt < filter.CreatedUntil);
        }
        if (filter.ExpiresFrom is not null)
        {
            rows = rows.Where(row => row.Reservation.ExpiresAt >= filter.ExpiresFrom && row.Reservation.ExpiresAt < filter.ExpiresUntil);
        }
        if (filter.ReleasedFrom is not null)
        {
            rows = rows.Where(row => row.Release != null && row.Release.RecordedAt >= filter.ReleasedFrom &&
                row.Release.RecordedAt < filter.ReleasedUntil);
        }
        return rows;
    }

    public static IQueryable<Row> CreateRows(DealershipDbContext database, IQueryable<Reservation> reservations)
    {
        return from reservation in reservations
               join branch in database.Branches on reservation.BranchId equals branch.Id
               join vehicle in database.Vehicles on reservation.VehicleId equals vehicle.Id
               join refunded in database.ReservationReleases.AsNoTracking() on reservation.Id equals refunded.ReservationId into releases
               from release in releases.DefaultIfEmpty()
               select new Row
               {
                   Reservation = reservation,
                   BranchName = branch.Name,
                   RegistrationPlate = vehicle.RegistrationPlate,
                   Make = vehicle.Make,
                   Model = vehicle.Model,
                   Trim = vehicle.Trim,
                   ManufactureYear = vehicle.ManufactureYear,
                   Release = release
               };
    }

    public static IOrderedQueryable<Row> Order(IQueryable<Row> rows, ReservationReadFilter filter)
    {
        if (filter.Sort.Length == 0)
        {
            return rows.OrderByDescending(row => row.Reservation.CreatedAt).ThenByDescending(row => row.Reservation.Id);
        }

        var ordered = filter.Sort switch
        {
            "registration" => OrderBy(rows, row => row.RegistrationPlate, filter.Direction),
            "released" => OrderReleased(rows, filter.Direction),
            "reference" => OrderBy(rows, row => row.Reservation.Id, filter.Direction),
            "vehicle" => OrderBy(rows, row => row.ManufactureYear + " " + row.Make + " " + row.Model + " " + row.Trim, filter.Direction),
            "buyer" => OrderBy(rows, row => row.Reservation.Buyer.LegalName, filter.Direction),
            "branch" => OrderBy(rows, row => row.BranchName, filter.Direction),
            "created" => OrderBy(rows, row => row.Reservation.CreatedAt, filter.Direction),
            "expires" => OrderBy(rows, row => row.Reservation.ExpiresAt, filter.Direction),
            "total" => OrderBy(rows, row => row.Reservation.Price.Total, filter.Direction),
            _ => OrderStatus(rows, filter.Direction)
        };
        return ordered.ThenBy(row => row.Reservation.Id);
    }

    private static IOrderedQueryable<Row> OrderBy<TKey>(IQueryable<Row> rows,
        Expression<Func<Row, TKey>> key, string direction)
    {
        return direction == "desc" ? rows.OrderByDescending(key) : rows.OrderBy(key);
    }

    private static IOrderedQueryable<Row> OrderStatus(IQueryable<Row> rows, string direction)
    {
        // Active precedes the alphabetically persisted release kinds when ascending.
        return direction == "desc"
            ? rows.OrderByDescending(row => row.Release != null).ThenByDescending(row => row.Release!.Kind)
            : rows.OrderBy(row => row.Release != null).ThenBy(row => row.Release!.Kind);
    }

    public static ReservationHistoryItem ToItem(Row row, bool staff) => new(ReservationReceipt.From(row.Reservation), row.BranchName,
        row.Make, row.Model, row.Trim, row.ManufactureYear, row.Reservation.IsActive,
        row.Release is null ? null : ReservationRefundReceipt.From(row.Release), staff ? row.RegistrationPlate : null);

    private static IOrderedQueryable<Row> OrderReleased(IQueryable<Row> rows, string direction)
    {
        var ordered = rows.OrderBy(row => row.Release == null);
        return direction == "desc"
            ? ordered.ThenByDescending(row => row.Release!.RecordedAt)
            : ordered.ThenBy(row => row.Release!.RecordedAt);
    }

    public sealed class Row
    {
        public required Reservation Reservation { get; init; }
        public required string BranchName { get; init; }
        public required string RegistrationPlate { get; init; }
        public required string Make { get; init; }
        public required string Model { get; init; }
        public required string Trim { get; init; }
        public int ManufactureYear { get; init; }
        public required ReservationRelease? Release { get; init; }
    }
}
