using Dealership.Domain.Inspections;
using Dealership.Domain.Vehicles;

namespace Dealership.Application.Vehicles;

public sealed record PublicListingFilter(int Page = 1, string? Make = null, string? Model = null,
    int? MinimumYear = null, int? MaximumYear = null, decimal? MinimumPrice = null, decimal? MaximumPrice = null,
    VehicleTransmission? Transmission = null, VehicleFuel? Fuel = null, Guid? BranchId = null,
    string Sort = "published", string Direction = "desc")
{
    public const int PageSize = 8;

    public bool IsValid => Page >= 1 && ValidText(Make) && ValidText(Model) && BranchId != Guid.Empty &&
        (MinimumYear is null or >= 1886 and <= 9999) && (MaximumYear is null or >= 1886 and <= 9999) &&
        (MinimumYear is null || MaximumYear is null || MinimumYear <= MaximumYear) &&
        ValidPrice(MinimumPrice) && ValidPrice(MaximumPrice) &&
        (MinimumPrice is null || MaximumPrice is null || MinimumPrice <= MaximumPrice) &&
        (Transmission is null || Enum.IsDefined(Transmission.Value)) &&
        (Fuel is null || Enum.IsDefined(Fuel.Value)) &&
        Sort is "published" or "price" or "year" or "mileage" && Direction is "asc" or "desc";

    private static bool ValidText(string? value) => value is null ||
        !string.IsNullOrWhiteSpace(value) && value.Length <= 100 && !value.Contains('\0');

    private static bool ValidPrice(decimal? value) => value is null ||
        value >= 0 && value <= 9999999999999999.99m && decimal.Round(value.Value, 2) == value;
}

public sealed record PublicListingItem(Guid Id, Guid BranchId, string BranchName, string Make, string Model,
    string Trim, int ManufactureYear, int MileageKilometres, decimal VehiclePrice, VehicleTransmission Transmission,
    VehicleFuel Fuel, Guid? CoverPhotoId, int PhotoCount, DateTimeOffset PublishedAt);

public sealed record PublicListingPage(IReadOnlyList<PublicListingItem> Items, int Total, int Page, int PageSize);

public sealed record PublicChecklistPoint(string Code, InspectionCategory Category, string Description,
    InspectionEntryKind? Kind, InspectionSeverity? Severity, decimal EstimatedRepairCost);

public sealed record PublicChecklist(string CatalogVersion, InspectionGrade? Grade, DateTimeOffset SignedAt,
    IReadOnlyList<PublicChecklistPoint> Points);

public sealed record PublicListingDetail(PublicListingItem Listing, decimal RoadTax, string Description,
    IReadOnlyList<Guid> PhotoIds, PublicChecklist Checklist, PurchasePrice Price, decimal SstRate,
    Guid PublicationId);

public sealed record PublicListingPageResult(ListingStatus Status, PublicListingPage? Catalogue = null);

public sealed record PublicListingDetailResult(ListingStatus Status, PublicListingDetail? Detail = null);

public interface IPublicListingStore
{
    Task<PublicListingPageResult> ReadAsync(PublicListingFilter filter, CancellationToken cancellationToken);

    Task<PublicListingDetailResult> DetailAsync(Guid vehicleId, CancellationToken cancellationToken);
}
