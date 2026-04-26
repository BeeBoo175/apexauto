using Dealership.Domain.Inspections;
using Dealership.Domain.Vehicles;

namespace Dealership.Application.Vehicles;

public sealed class WarrantyAdministrationService(IWarrantyAdministrationStore store)
{
    public static readonly IReadOnlySet<string> Statuses =
        new HashSet<string> { AdministrationListQuery.AllStatuses, "Active", "Inactive" };

    public static readonly IReadOnlySet<string> Sorts =
        new HashSet<string> { "name", "term", "mileage", "price", "grade", "status" };

    public Task<WarrantyAdministrationResult> ListAsync(Guid actorId, int page, int pageSize, string? search,
        string? status, string? sort, string? direction, CancellationToken cancellationToken)
    {
        if (actorId == Guid.Empty)
        {
            return Rejected(WarrantyAdministrationStatus.Forbidden);
        }

        var query = AdministrationListQuery.Create(page, pageSize, search, status, sort, direction, Statuses, Sorts);
        return query is null ? Rejected(WarrantyAdministrationStatus.Invalid)
            : store.ListAsync(actorId, query, cancellationToken);
    }

    public Task<WarrantyAdministrationResult> CreateAsync(Guid actorId, WarrantyCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (actorId == Guid.Empty)
        {
            return Rejected(WarrantyAdministrationStatus.Forbidden);
        }

        if (request.Id == Guid.Empty || !TryParseGrade(request.MinimumGrade, out var grade) ||
            !WarrantyPackage.ValidName(request.Name) ||
            !WarrantyPackage.ValidTerms(request.DurationMonths, request.MileageCapKm, request.Price, grade))
        {
            return Rejected(WarrantyAdministrationStatus.Invalid);
        }

        var package = new WarrantyPackage(request.Id, request.Name, request.DurationMonths, request.MileageCapKm,
            request.Price, grade, request.IsActive);
        return store.CreateAsync(actorId, package, cancellationToken);
    }

    public Task<WarrantyAdministrationResult> UpdateAsync(Guid actorId, Guid packageId, WarrantyUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (actorId == Guid.Empty)
        {
            return Rejected(WarrantyAdministrationStatus.Forbidden);
        }

        if (packageId == Guid.Empty || request.ExpectedVersion <= 0 || !TryParseGrade(request.MinimumGrade, out var grade) ||
            !WarrantyPackage.ValidName(request.Name) ||
            !WarrantyPackage.ValidTerms(request.DurationMonths, request.MileageCapKm, request.Price, grade))
        {
            return Rejected(WarrantyAdministrationStatus.Invalid);
        }

        return store.UpdateAsync(actorId, packageId, request, grade, cancellationToken);
    }

    // Only the exact letters are accepted; numeric strings would otherwise parse as enum values.
    private static bool TryParseGrade(string? value, out InspectionGrade grade)
    {
        grade = default;
        return value is "A" or "B" or "C" && Enum.TryParse(value, out grade);
    }

    private static Task<WarrantyAdministrationResult> Rejected(WarrantyAdministrationStatus status) =>
        Task.FromResult(new WarrantyAdministrationResult(status));
}
