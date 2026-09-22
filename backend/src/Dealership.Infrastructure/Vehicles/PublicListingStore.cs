using System.Linq.Expressions;
using Dealership.Application.Vehicles;
using Dealership.Domain.Inspections;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Vehicles;

public sealed class PublicListingStore(DealershipDbContext database) : IPublicListingStore
{
    // How the signed report stores each grade: the assessment object holds the InspectionGrade number.
    private const string GradeAJson = "{\"assessment\":{\"grade\":0}}";
    private const string GradeBJson = "{\"assessment\":{\"grade\":1}}";
    private const string GradeCJson = "{\"assessment\":{\"grade\":2}}";

    private static readonly Expression<Func<PublicListingRow, PublicListingRead>> Item = row => new(new PublicListingItem(
        row.Vehicle.Id, row.Vehicle.BranchId, row.BranchName, row.Vehicle.Make, row.Vehicle.Model,
        row.Vehicle.Trim, row.Vehicle.ManufactureYear, row.Vehicle.MileageKilometres,
        row.Revision.VehiclePrice, row.Revision.Transmission, row.Revision.Fuel,
        row.Revision.PhotoIds.Length == 0 ? null : row.Revision.PhotoIds[0], row.Revision.PhotoIds.Length,
        row.Publication.RecordedAt,
        EF.Functions.JsonContains(row.ReportJson, GradeAJson) ? (InspectionGrade?)InspectionGrade.A :
        EF.Functions.JsonContains(row.ReportJson, GradeBJson) ? (InspectionGrade?)InspectionGrade.B :
        EF.Functions.JsonContains(row.ReportJson, GradeCJson) ? (InspectionGrade?)InspectionGrade.C : null),
        row.Revision.RoadTax);

    // The card item plus the road tax its on-the-road total needs; the total is priced after materialisation.
    private sealed record PublicListingRead(PublicListingItem Item, decimal RoadTax);

    public async Task<PublicListingPageResult> ReadAsync(PublicListingFilter filter, CancellationToken cancellationToken)
    {
        if (!filter.IsValid)
        {
            return new(ListingStatus.Invalid);
        }

        return await ReservationReadSnapshot.ReadAsync(database,
            () => ReadSnapshotAsync(filter, cancellationToken),
            new PublicListingPageResult(ListingStatus.Unavailable), cancellationToken);
    }

    private async Task<PublicListingPageResult> ReadSnapshotAsync(PublicListingFilter filter, CancellationToken cancellationToken)
    {
        var vehicleIds = Filter(PublicListingQuery.Published(database), filter).Select(row => row.Vehicle.Id);
        var caughtUp = await ReservationReadSnapshot.ReconcileDueAsync(database, vehicleIds,
            filter.RequestedPageSize, cancellationToken);
        if (!caughtUp)
        {
            return new(ListingStatus.Unavailable);
        }

        // The count and bounded rows include this snapshot's committed refund catch-up.
        var now = await database.DatabaseNowAsync(cancellationToken);
        var rows = Filter(PublicListingQuery.Read(database, now), filter);
        var total = await rows.CountAsync(cancellationToken);
        var settings = await database.PurchaseSettings.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (settings is null)
        {
            return new(ListingStatus.Unavailable);
        }

        var reads = await Order(rows, filter).Select(Item)
            .ReadPageAsync(filter.Page, filter.RequestedPageSize, total, cancellationToken);
        var items = reads.Select(read => read.Item with { OnTheRoadTotal = OnTheRoadTotal(read, settings) }).ToArray();
        return new(ListingStatus.Succeeded, new(Array.AsReadOnly(items), total, filter.Page, filter.RequestedPageSize));
    }

    public async Task<PublicListingDetailResult> DetailAsync(Guid vehicleId, CancellationToken cancellationToken)
    {
        if (vehicleId == Guid.Empty)
        {
            return new(ListingStatus.Invalid);
        }

        return await ReservationReadSnapshot.ReadAsync(database,
            () => DetailSnapshotAsync(vehicleId, cancellationToken),
            new PublicListingDetailResult(ListingStatus.Unavailable), cancellationToken);
    }

    private async Task<PublicListingDetailResult> DetailSnapshotAsync(Guid vehicleId, CancellationToken cancellationToken)
    {
        await ReservationReadSnapshot.ReconcileAsync(database, vehicleId, cancellationToken);
        var now = await database.DatabaseNowAsync(cancellationToken);
        var listing = PublicListingQuery.Read(database, now).Where(row => row.Vehicle.Id == vehicleId);
        var read = await listing.Select(Item).SingleOrDefaultAsync(cancellationToken);
        if (read is null)
        {
            return new(ListingStatus.NotFound);
        }

        var settings = await database.PurchaseSettings.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (settings is null)
        {
            return new(ListingStatus.Unavailable);
        }

        var item = read.Item;
        var details = await listing.Select(row => new
        {
            row.Revision.RoadTax,
            row.Revision.Description,
            row.Revision.PhotoIds,
            PublicationId = row.Publication.Id,
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
        var price = PurchasePricing.CalculatePrice(item.VehiclePrice, settings.ProcessingFee,
            details.RoadTax, settings.SstRate);
        item = item with { OnTheRoadTotal = price.Total };
        return new(ListingStatus.Succeeded, new(item, details.RoadTax, details.Description,
            Array.AsReadOnly(details.PhotoIds), checklist, price, settings.SstRate, details.PublicationId));
    }

    private static decimal OnTheRoadTotal(PublicListingRead read, PurchaseSettings settings) =>
        PurchasePricing.CalculatePrice(read.Item.VehiclePrice, settings.ProcessingFee, read.RoadTax, settings.SstRate).Total;

    private static IQueryable<PublicListingRow> Filter(IQueryable<PublicListingRow> rows, PublicListingFilter filter)
    {
        var anyGrade = filter.Grades is null;
        var wantsA = filter.Grades?.Contains(InspectionGrade.A) == true;
        var wantsB = filter.Grades?.Contains(InspectionGrade.B) == true;
        var wantsC = filter.Grades?.Contains(InspectionGrade.C) == true;
        var make = filter.Make?.Trim();
        var model = filter.Model?.Trim();
        rows = rows.Where(row =>
            (make == null || row.Vehicle.Make == make) &&
            (model == null || row.Vehicle.Model == model) &&
            (filter.BranchId == null || row.Vehicle.BranchId == filter.BranchId) &&
            (filter.MinimumYear == null || row.Vehicle.ManufactureYear >= filter.MinimumYear) &&
            (filter.MaximumYear == null || row.Vehicle.ManufactureYear <= filter.MaximumYear) &&
            (filter.MinimumPrice == null || row.Revision.VehiclePrice >= filter.MinimumPrice) &&
            (filter.MaximumPrice == null || row.Revision.VehiclePrice <= filter.MaximumPrice) &&
            (filter.Transmission == null || row.Revision.Transmission == filter.Transmission) &&
            (filter.Fuel == null || row.Revision.Fuel == filter.Fuel) &&
            (anyGrade ||
                (wantsA && EF.Functions.JsonContains(row.ReportJson, GradeAJson)) ||
                (wantsB && EF.Functions.JsonContains(row.ReportJson, GradeBJson)) ||
                (wantsC && EF.Functions.JsonContains(row.ReportJson, GradeCJson))));
        return Search(rows, filter.Search);
    }

    // Every word must match the make, model or trim; a four digit word also matches the year.
    private static IQueryable<PublicListingRow> Search(IQueryable<PublicListingRow> rows, string? search)
    {
        var terms = (search ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(6);
        foreach (var term in terms)
        {
            var pattern = LiteralSearch.Pattern(term);
            var year = int.TryParse(term, out var parsed) ? parsed : (int?)null;
            rows = rows.Where(row => EF.Functions.ILike(row.Vehicle.Make, pattern, "!") ||
                EF.Functions.ILike(row.Vehicle.Model, pattern, "!") ||
                EF.Functions.ILike(row.Vehicle.Trim, pattern, "!") || row.Vehicle.ManufactureYear == year);
        }

        return rows;
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
