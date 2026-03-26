using System.Data;
using System.Linq.Expressions;
using Dealership.Application.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Vehicles;

public sealed class PublicListingStore(DealershipDbContext database) : IPublicListingStore
{
    private static readonly Expression<Func<PublicListingRow, PublicListingItem>> Item = row => new(
        row.Vehicle.Id, row.Vehicle.BranchId, row.BranchName, row.Vehicle.Make, row.Vehicle.Model,
        row.Vehicle.Trim, row.Vehicle.ManufactureYear, row.Vehicle.MileageKilometres,
        row.Revision.VehiclePrice, row.Revision.Transmission, row.Revision.Fuel,
        row.Revision.PhotoIds.Length == 0 ? null : row.Revision.PhotoIds[0], row.Revision.PhotoIds.Length,
        row.Publication.RecordedAt);

    public async Task<PublicListingPageResult> ReadAsync(PublicListingFilter filter, CancellationToken cancellationToken)
    {
        if (!filter.IsValid)
        {
            return new(ListingStatus.Invalid);
        }

        // The count and bounded rows must describe the same eligibility snapshot.
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var rows = Filter(PublicListingQuery.Read(database), filter);
        var total = await rows.CountAsync(cancellationToken);
        var items = await Order(rows, filter).Select(Item)
            .ReadPageAsync(filter.Page, PublicListingFilter.PageSize, total, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(ListingStatus.Succeeded, new(Array.AsReadOnly(items), total, filter.Page, PublicListingFilter.PageSize));
    }

    public async Task<PublicListingDetailResult> DetailAsync(Guid vehicleId, CancellationToken cancellationToken)
    {
        if (vehicleId == Guid.Empty)
        {
            return new(ListingStatus.Invalid);
        }

        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var listing = PublicListingQuery.Read(database).Where(row => row.Vehicle.Id == vehicleId);
        var item = await listing.Select(Item).SingleOrDefaultAsync(cancellationToken);
        if (item is null)
        {
            return new(ListingStatus.NotFound);
        }

        var details = await listing.Select(row => new
        {
            row.Revision.RoadTax,
            row.Revision.Description,
            row.Revision.PhotoIds,
            row.Publication.SignedRevisionId
        }).SingleAsync(cancellationToken);
        var signed = await database.InspectionSignedRevisions.AsNoTracking()
            .SingleAsync(row => row.Id == details.SignedRevisionId, cancellationToken);
        var report = signed.Report();
        // Free-text notes and private report identities/evidence have no public projection.
        var points = report.Checklist.Select(point => new PublicChecklistPoint(point.Code, point.Category,
            point.Description, point.Kind, point.Severity, point.EstimatedRepairCost)).ToArray();
        var checklist = new PublicChecklist(report.CatalogVersion, report.Assessment.Grade,
            report.SignedAt, Array.AsReadOnly(points));
        await transaction.CommitAsync(cancellationToken);
        return new(ListingStatus.Succeeded, new(item, details.RoadTax, details.Description,
            Array.AsReadOnly(details.PhotoIds), checklist));
    }

    private static IQueryable<PublicListingRow> Filter(IQueryable<PublicListingRow> rows, PublicListingFilter filter)
    {
        var make = filter.Make?.Trim();
        var model = filter.Model?.Trim();
        return rows.Where(row =>
            (make == null || row.Vehicle.Make == make) &&
            (model == null || row.Vehicle.Model == model) &&
            (filter.BranchId == null || row.Vehicle.BranchId == filter.BranchId) &&
            (filter.MinimumYear == null || row.Vehicle.ManufactureYear >= filter.MinimumYear) &&
            (filter.MaximumYear == null || row.Vehicle.ManufactureYear <= filter.MaximumYear) &&
            (filter.MinimumPrice == null || row.Revision.VehiclePrice >= filter.MinimumPrice) &&
            (filter.MaximumPrice == null || row.Revision.VehiclePrice <= filter.MaximumPrice) &&
            (filter.Transmission == null || row.Revision.Transmission == filter.Transmission) &&
            (filter.Fuel == null || row.Revision.Fuel == filter.Fuel));
    }

    private static IOrderedQueryable<PublicListingRow> Order(IQueryable<PublicListingRow> rows, PublicListingFilter filter)
    {
        var ascending = filter.Direction == "asc";
        var ordered = filter.Sort switch
        {
            "price" => ascending ? rows.OrderBy(row => row.Revision.VehiclePrice) : rows.OrderByDescending(row => row.Revision.VehiclePrice),
            "year" => ascending ? rows.OrderBy(row => row.Vehicle.ManufactureYear) : rows.OrderByDescending(row => row.Vehicle.ManufactureYear),
            "mileage" => ascending ? rows.OrderBy(row => row.Vehicle.MileageKilometres) : rows.OrderByDescending(row => row.Vehicle.MileageKilometres),
            _ => ascending ? rows.OrderBy(row => row.Publication.RecordedAt) : rows.OrderByDescending(row => row.Publication.RecordedAt)
        };
        return ordered.ThenBy(row => row.Vehicle.Id);
    }
}
