using Dealership.Domain.Sales;
using System.Linq.Expressions;
using Dealership.Application.Vehicles;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Vehicles;

internal static class ReservationReadRows
{
    public static IQueryable<Row> Filter(IQueryable<Row> rows, ReservationReadFilter filter, bool searchBranch = false)
    {
        if (filter.Search.Length > 0)
        {
            rows = Search(rows, LiteralSearch.Pattern(filter.Search), searchBranch);
        }
        if (filter.Agent.Length > 0)
        {
            var pattern = LiteralSearch.Pattern(filter.Agent);
            rows = rows.Where(row => EF.Functions.ILike(row.ClaimedBy ?? string.Empty, pattern, "!"));
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

    // What staff look a hold up by: vehicle, buyer and contact, promo and handler. Branch is only a choice for the group scope, and no identifier is matched.
    private static IQueryable<Row> Search(IQueryable<Row> rows, string pattern, bool searchBranch) =>
        rows.Where(row => EF.Functions.ILike(row.RegistrationPlate, pattern, "!") ||
            EF.Functions.ILike(row.Reservation.Number, pattern, "!") ||
            EF.Functions.ILike(row.Vin, pattern, "!") ||
            EF.Functions.ILike(row.ManufactureYear + " " + row.Make + " " + row.Model + " " + row.Trim, pattern, "!") ||
            EF.Functions.ILike(row.Reservation.Buyer.LegalName, pattern, "!") ||
            EF.Functions.ILike(row.Reservation.Buyer.Mobile, pattern, "!") ||
            EF.Functions.ILike(row.Reservation.Buyer.Email, pattern, "!") ||
            EF.Functions.ILike(row.Reservation.PromoCode ?? string.Empty, pattern, "!") ||
            EF.Functions.ILike(row.ClaimedBy ?? string.Empty, pattern, "!") ||
            searchBranch && EF.Functions.ILike(row.BranchName, pattern, "!"));

    public static IQueryable<Row> CreateRows(DealershipDbContext database, IQueryable<Reservation> reservations)
    {
        return from reservation in reservations
               join branch in database.Branches on reservation.BranchId equals branch.Id
               join vehicle in database.Vehicles on reservation.VehicleId equals vehicle.Id
               join refunded in database.ReservationReleases.AsNoTracking() on reservation.Id equals refunded.ReservationId into releases
               from release in releases.DefaultIfEmpty()
               join sold in database.SalesOrders.AsNoTracking() on reservation.Id equals sold.Id
               join agent in database.Users.AsNoTracking() on sold.AgentId equals agent.Id into agents
               from claimant in agents.DefaultIfEmpty()
               select new Row
               {
                   Reservation = reservation,
                   BranchName = branch.Name,
                   RegistrationPlate = vehicle.RegistrationPlate,
                   Vin = vehicle.Vin,
                   Make = vehicle.Make,
                   Model = vehicle.Model,
                   Trim = vehicle.Trim,
                   ManufactureYear = vehicle.ManufactureYear,
                   Release = release,
                   Sale = sold,
                   ClaimedBy = claimant.Email,
                   CoverPhotoId = (from publication in database.ListingPublications
                                   join photo in database.ListingRevisionPhotos on publication.RevisionId equals photo.RevisionId
                                   where publication.Id == reservation.PublicationId
                                   orderby photo.Position
                                   select (Guid?)photo.PhotoId).FirstOrDefault()
               };
    }

    public static IOrderedQueryable<Row> Order(IQueryable<Row> rows, ReservationReadFilter filter)
    {
        if (filter.Sort.Length == 0)
        {
            // Queue order: open holds by soonest expiry, handled holds by the longest-standing claim.
            return (filter.IsActive, filter.Claimed) switch
            {
                (true, true) => rows.OrderBy(row => row.Sale.ClaimedAt).ThenBy(row => row.Reservation.Id),
                (true, false) => rows.OrderBy(row => row.Sale.DueAt).ThenBy(row => row.Reservation.Id),
                _ => rows.OrderByDescending(row => row.Reservation.CreatedAt).ThenByDescending(row => row.Reservation.Id)
            };
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
            "agent" => OrderBy(rows, row => row.ClaimedBy, filter.Direction),
            "claimed" => OrderBy(rows, row => row.Sale.ClaimedAt, filter.Direction),
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
        row.Release is null ? null : ReservationRefundReceipt.From(row.Release), staff ? row.RegistrationPlate : null,
        row.CoverPhotoId, staff ? ClaimOf(row) : null, staff ? row.Vin : null);

    private static ReservationClaimView? ClaimOf(Row row) => row.Sale is
    {
        ClaimedAt: { } claimedAt, PausedSeconds: { } remaining
    } ? new ReservationClaimView(row.ClaimedBy ?? string.Empty, claimedAt, remaining) : null;

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
        public required string Vin { get; init; }
        public required string Make { get; init; }
        public required string Model { get; init; }
        public required string Trim { get; init; }
        public int ManufactureYear { get; init; }
        public required ReservationRelease? Release { get; init; }
        public required SalesOrder Sale { get; init; }
        public string? ClaimedBy { get; init; }
        public Guid? CoverPhotoId { get; init; }
    }
}
