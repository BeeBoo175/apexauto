using Dealership.Domain.Common;
using Dealership.Domain.Inspections;
using Dealership.Domain.Vehicles;

namespace Dealership.Application.Vehicles;

public sealed record PublicListingFilter(int Page = 1, string? Make = null, string? Model = null,
    int? MinimumYear = null, int? MaximumYear = null, decimal? MinimumPrice = null, decimal? MaximumPrice = null,
    VehicleTransmission? Transmission = null, VehicleFuel? Fuel = null, Guid? BranchId = null,
    string Sort = "published", string Direction = "desc", int RequestedPageSize = PublicListingFilter.PageSize,
    string? Search = null, IReadOnlyList<InspectionGrade>? Grades = null)
{
    public const int PageSize = 8;
    public const int StorefrontPageSize = 12;
    public const int CataloguePageSize = 18;

    public bool IsValid => Page >= 1 && RequestedPageSize is PageSize or StorefrontPageSize or CataloguePageSize &&
        ValidText(Make) && ValidText(Model) && ValidSearch(Search) && BranchId != Guid.Empty &&
        (MinimumYear is null or >= 1886 and <= 9999) && (MaximumYear is null or >= 1886 and <= 9999) &&
        (MinimumYear is null || MaximumYear is null || MinimumYear <= MaximumYear) &&
        ValidPrice(MinimumPrice) && ValidPrice(MaximumPrice) &&
        (MinimumPrice is null || MaximumPrice is null || MinimumPrice <= MaximumPrice) &&
        (Transmission is null || Enum.IsDefined(Transmission.Value)) &&
        (Fuel is null || Enum.IsDefined(Fuel.Value)) && ValidGrades(Grades) &&
        Sort is "published" or "price" or "year" or "mileage" && Direction is "asc" or "desc";

    // A comma list such as "A,B": the grades a visitor wants, each named once. Null means every grade; an empty or
    // unrecognised list is invalid, so a mistyped link never quietly shows the wrong cars.
    public static IReadOnlyList<InspectionGrade>? ParseGrades(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var grades = new List<InspectionGrade>();
        foreach (var token in value.Split(',', StringSplitOptions.TrimEntries))
        {
            if (token.Length != 1 || !Enum.TryParse<InspectionGrade>(token, ignoreCase: true, out var grade) ||
                !Enum.IsDefined(grade) || grades.Contains(grade))
            {
                return Array.Empty<InspectionGrade>();
            }

            grades.Add(grade);
        }

        return grades;
    }

    private static bool ValidGrades(IReadOnlyList<InspectionGrade>? grades) => grades is null || grades.Count > 0;

    private static bool ValidText(string? value) => value is null ||
        !string.IsNullOrWhiteSpace(value) && value.Length <= 100 && !value.Contains('\0');

    // Free text over make, model, trim and year; the box allows 160 characters.
    private static bool ValidSearch(string? value) => value is null ||
        !string.IsNullOrWhiteSpace(value) && value.Length <= 160 && !value.Contains('\0');

    private static bool ValidPrice(decimal? value) => value is null ||
        value >= 0 && value <= MoneyStorage.MaximumAmount && decimal.Round(value.Value, 2) == value;
}

public sealed record PublicListingItem(Guid Id, Guid BranchId, string BranchName, string Make, string Model,
    string Trim, int ManufactureYear, int MileageKilometres, decimal VehiclePrice, VehicleTransmission Transmission,
    VehicleFuel Fuel, Guid? CoverPhotoId, int PhotoCount, DateTimeOffset PublishedAt, InspectionGrade? Grade)
{
    // The no-promo on-the-road total from PurchasePricing; the store fills it so cards and detail quote one figure.
    public decimal OnTheRoadTotal { get; init; }
}

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
