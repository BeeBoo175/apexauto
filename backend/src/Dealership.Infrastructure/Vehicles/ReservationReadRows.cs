using Dealership.Application.Vehicles;
using Dealership.Domain.Sales;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Vehicles;

internal static class ReservationReadRows
{
    public static IQueryable<Row> CreateRows(DealershipDbContext database, IQueryable<Reservation> reservations)
    {
        return from reservation in reservations
               join branch in database.Branches on reservation.BranchId equals branch.Id
               join vehicle in database.Vehicles on reservation.VehicleId equals vehicle.Id
               join refunded in database.ReservationReleases.AsNoTracking() on reservation.Id equals refunded.ReservationId into releases
               from release in releases.DefaultIfEmpty()
               join sold in database.SalesOrders.AsNoTracking() on reservation.Id equals sold.Id
               select new Row
               {
                   Reservation = reservation,
                   BranchName = branch.Name,
                   Make = vehicle.Make,
                   Model = vehicle.Model,
                   Trim = vehicle.Trim,
                   ManufactureYear = vehicle.ManufactureYear,
                   Release = release,
                   Sale = sold,
                   CoverPhotoId = (from publication in database.ListingPublications
                                   join photo in database.ListingRevisionPhotos on publication.RevisionId equals photo.RevisionId
                                   where publication.Id == reservation.PublicationId
                                   orderby photo.Position
                                   select (Guid?)photo.PhotoId).FirstOrDefault()
               };
    }

    // Newest first, with the identifier breaking ties between holds made at the same instant.
    public static IOrderedQueryable<Row> Order(IQueryable<Row> rows) =>
        rows.OrderByDescending(row => row.Reservation.CreatedAt).ThenByDescending(row => row.Reservation.Id);

    // Latest handover first, for the collection history.
    public static IOrderedQueryable<Row> OrderByCollection(IQueryable<Row> rows) =>
        rows.OrderByDescending(row => row.Sale.DeliveredAt).ThenByDescending(row => row.Reservation.Id);

    public static ReservationHistoryItem ToItem(Row row) => new(ReservationReceipt.From(row.Reservation), row.BranchName,
        row.Make, row.Model, row.Trim, row.ManufactureYear, row.Reservation.IsActive,
        row.Release is null ? null : ReservationRefundReceipt.From(row.Release), row.CoverPhotoId)
    {
        IsHandled = row.Reservation.IsActive && row.Sale.IsHandled,
        DueAt = row.Reservation.IsActive ? row.Sale.DueAt : null,
        SettledAt = row.Sale.SettledAt,
        DeliveredAt = row.Sale.DeliveredAt
    };

    public sealed class Row
    {
        public required Reservation Reservation { get; init; }
        public required string BranchName { get; init; }
        public required string Make { get; init; }
        public required string Model { get; init; }
        public required string Trim { get; init; }
        public int ManufactureYear { get; init; }
        public required ReservationRelease? Release { get; init; }
        public required SalesOrder Sale { get; init; }
        public Guid? CoverPhotoId { get; init; }
    }
}
