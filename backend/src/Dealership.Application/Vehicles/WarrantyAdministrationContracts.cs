using Dealership.Domain.Inspections;
using Dealership.Domain.Vehicles;

namespace Dealership.Application.Vehicles;

public sealed record WarrantyCreateRequest(Guid Id, string Name, int DurationMonths, int MileageCapKm, decimal Price,
    string MinimumGrade, bool IsActive = true);

public sealed record WarrantyUpdateRequest(string Name, int DurationMonths, int MileageCapKm, decimal Price,
    string MinimumGrade, bool IsActive, long ExpectedVersion);

public enum WarrantyAdministrationStatus
{
    Succeeded,
    Invalid,
    Forbidden,
    NotFound,
    Conflict
}

public sealed record WarrantyView(Guid Id, string Name, int DurationMonths, int MileageCapKm, decimal Price,
    string MinimumGrade, bool IsActive, long Version)
{
    public static WarrantyView From(WarrantyPackage package) => new(package.Id, package.Name, package.DurationMonths,
        package.MileageCapKm, package.Price, package.MinimumGrade.ToString(), package.IsActive, package.Version);
}

public sealed record WarrantyPage(IReadOnlyList<WarrantyView> Items, int Total, int Page, int PageSize);

// Conflict carries the current package so a stale editor can reload without another read.
public sealed record WarrantyAdministrationResult(WarrantyAdministrationStatus Status, WarrantyView? Package = null,
    WarrantyPage? Page = null);

public sealed record WarrantyProvisioningRequest(Guid Id, string Name, int DurationMonths, int MileageCapKm,
    decimal Price, InspectionGrade MinimumGrade, bool IsActive = true);

public interface IWarrantyAdministrationStore
{
    Task<WarrantyAdministrationResult> ListAsync(Guid actorId, AdministrationListQuery query,
        CancellationToken cancellationToken);

    Task<WarrantyAdministrationResult> GetAsync(Guid actorId, Guid packageId, CancellationToken cancellationToken);

    Task<WarrantyAdministrationResult> CreateAsync(Guid actorId, WarrantyPackage package,
        CancellationToken cancellationToken);

    Task<WarrantyAdministrationResult> UpdateAsync(Guid actorId, Guid packageId, WarrantyUpdateRequest request,
        InspectionGrade minimumGrade, CancellationToken cancellationToken);
}
