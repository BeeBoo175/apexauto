using Dealership.Application.Common;
using Dealership.Domain.Inspections;
using Dealership.Domain.Vehicles;

namespace Dealership.Application.Vehicles;

public sealed class WarrantyAdministrationService(IWarrantyAdministrationStore store)
{
    public Task<WarrantyAdministrationResult> ListAsync(Guid actorId, int page, int pageSize,
        CancellationToken cancellationToken) =>
        actorId == Guid.Empty ? Rejected(WarrantyAdministrationStatus.Forbidden)
            : !Pagination.IsValid(page, pageSize) ? Rejected(WarrantyAdministrationStatus.Invalid)
            : store.ListAsync(actorId, page, pageSize, cancellationToken);

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
