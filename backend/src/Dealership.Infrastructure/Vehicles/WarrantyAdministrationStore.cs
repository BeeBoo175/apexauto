using Dealership.Application.Vehicles;
using Dealership.Domain.Inspections;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Vehicles;

public sealed class WarrantyAdministrationStore(DealershipDbContext database) : IWarrantyAdministrationStore
{
    public async Task<WarrantyAdministrationResult> ListAsync(Guid actorId, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        if (!await AdministratorScope.IsAdministratorAsync(database, actorId, cancellationToken))
        {
            return new(WarrantyAdministrationStatus.Forbidden);
        }

        var packages = database.WarrantyPackages.AsNoTracking();
        var total = await packages.CountAsync(cancellationToken);
        // Name is unique, so the ordering is total and paging never repeats or skips a package.
        var items = await packages.OrderBy(item => item.Price).ThenBy(item => item.Name)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var result = new WarrantyPage(items.Select(WarrantyView.From).ToList(), total, page, pageSize);
        return new(WarrantyAdministrationStatus.Succeeded, Page: result);
    }

    public async Task<WarrantyAdministrationResult> CreateAsync(Guid actorId, WarrantyPackage package,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (!await AdministratorScope.LockAdministratorAsync(database, actorId, cancellationToken))
        {
            return new(WarrantyAdministrationStatus.Forbidden);
        }

        await LockNamesAsync(cancellationToken);
        var existing = await database.WarrantyPackages.AsNoTracking()
            .Where(item => item.Id == package.Id || item.Name == package.Name).ToListAsync(cancellationToken);
        if (existing.Count > 0)
        {
            // An identical replay reconciles a lost receipt; any other overlap is a conflict.
            var replay = existing.Count == 1 && Matches(existing[0], package);
            return replay ? new(WarrantyAdministrationStatus.Succeeded, WarrantyView.From(existing[0]))
                : new(WarrantyAdministrationStatus.Conflict);
        }

        database.WarrantyPackages.Add(package);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(WarrantyAdministrationStatus.Succeeded, WarrantyView.From(package));
    }

    public async Task<WarrantyAdministrationResult> UpdateAsync(Guid actorId, Guid packageId,
        WarrantyUpdateRequest request, InspectionGrade minimumGrade, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (!await AdministratorScope.LockAdministratorAsync(database, actorId, cancellationToken))
        {
            return new(WarrantyAdministrationStatus.Forbidden);
        }

        await LockNamesAsync(cancellationToken);
        var package = await database.WarrantyPackages.FromSqlInterpolated(
            $"SELECT * FROM \"WarrantyPackages\" WHERE \"Id\" = {packageId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
        if (package is null)
        {
            return new(WarrantyAdministrationStatus.NotFound);
        }

        if (package.Version != request.ExpectedVersion)
        {
            return new(WarrantyAdministrationStatus.Conflict, WarrantyView.From(package));
        }

        var name = request.Name.Trim();
        if (await database.WarrantyPackages.AnyAsync(item => item.Name == name && item.Id != packageId, cancellationToken))
        {
            return new(WarrantyAdministrationStatus.Conflict, WarrantyView.From(package));
        }

        package.Update(name, request.DurationMonths, request.MileageCapKm, request.Price, minimumGrade, request.IsActive);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(WarrantyAdministrationStatus.Succeeded, WarrantyView.From(package));
    }

    internal static bool Matches(WarrantyPackage existing, WarrantyPackage requested) =>
        existing.Id == requested.Id && existing.Name == requested.Name &&
        existing.DurationMonths == requested.DurationMonths && existing.MileageCapKm == requested.MileageCapKm &&
        existing.Price == requested.Price && existing.MinimumGrade == requested.MinimumGrade &&
        existing.IsActive == requested.IsActive;

    private async Task LockNamesAsync(CancellationToken cancellationToken)
    {
        // Serialize identity and unique-name checks so replays and competing writers see one outcome.
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({"warranty-configuration"}, 0))", cancellationToken);
    }
}
