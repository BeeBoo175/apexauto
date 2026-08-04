using Dealership.Application.Vehicles;
using Dealership.Domain.Inspections;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Vehicles;

public sealed class WarrantyAdministrationStore(DealershipDbContext database) : IWarrantyAdministrationStore
{
    public async Task<WarrantyAdministrationResult> ListAsync(Guid actorId, AdministrationListQuery query,
        CancellationToken cancellationToken)
    {
        if (!await AdministratorScope.IsAdministratorAsync(database, actorId, cancellationToken))
        {
            return new(WarrantyAdministrationStatus.Forbidden);
        }

        var packages = Filter(database.WarrantyPackages.AsNoTracking(), query);
        var total = await packages.CountAsync(cancellationToken);
        // Name is unique, so every ordering ends in a total tie-breaker and paging never repeats or skips a package.
        var items = await Order(packages, query).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .ToListAsync(cancellationToken);
        var result = new WarrantyPage([.. items.Select(WarrantyView.From)], total, query.Page, query.PageSize);
        return new(WarrantyAdministrationStatus.Succeeded, Page: result);
    }

    public async Task<WarrantyAdministrationResult> GetAsync(Guid actorId, Guid packageId,
        CancellationToken cancellationToken)
    {
        if (!await AdministratorScope.IsAdministratorAsync(database, actorId, cancellationToken))
        {
            return new(WarrantyAdministrationStatus.Forbidden);
        }

        var package = await database.WarrantyPackages.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == packageId, cancellationToken);
        return package is null ? new(WarrantyAdministrationStatus.NotFound)
            : new(WarrantyAdministrationStatus.Succeeded, WarrantyView.From(package));
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

    private static IQueryable<WarrantyPackage> Filter(IQueryable<WarrantyPackage> packages, AdministrationListQuery query)
    {
        if (query.Search is not null)
        {
            var pattern = LiteralSearch.Pattern(query.Search);
            packages = packages.Where(item => EF.Functions.ILike(item.Name, pattern, "!"));
        }

        if (query.Text is not null)
        {
            var pattern = LiteralSearch.Pattern(query.Text);
            packages = packages.Where(item => EF.Functions.ILike(item.Name, pattern, "!"));
        }

        if (query.MinimumAmount is { } minimum)
        {
            packages = packages.Where(item => item.Price >= minimum);
        }

        if (query.MaximumAmount is { } maximum)
        {
            packages = packages.Where(item => item.Price <= maximum);
        }

        if (query.MinimumTerm is { } minimumTerm)
        {
            packages = packages.Where(item => item.DurationMonths >= minimumTerm);
        }

        if (query.MaximumTerm is { } maximumTerm)
        {
            packages = packages.Where(item => item.DurationMonths <= maximumTerm);
        }

        if (query.MinimumMileage is { } minimumMileage)
        {
            packages = packages.Where(item => item.MileageCapKm >= minimumMileage);
        }

        if (query.MaximumMileage is { } maximumMileage)
        {
            packages = packages.Where(item => item.MileageCapKm <= maximumMileage);
        }

        if (query.Grade is not null)
        {
            var grade = Enum.Parse<InspectionGrade>(query.Grade);
            packages = packages.Where(item => item.MinimumGrade == grade);
        }

        return query.Status switch
        {
            "active" => packages.Where(item => item.IsActive),
            "inactive" => packages.Where(item => !item.IsActive),
            _ => packages
        };
    }

    private static IOrderedQueryable<WarrantyPackage> Order(IQueryable<WarrantyPackage> packages,
        AdministrationListQuery query)
    {
        var descending = query.Descending;
        return query.Sort switch
        {
            "name" => descending ? packages.OrderByDescending(item => item.Name) : packages.OrderBy(item => item.Name),
            "term" => (descending ? packages.OrderByDescending(item => item.DurationMonths) : packages.OrderBy(item => item.DurationMonths))
                .ThenBy(item => item.Name),
            "mileage" => (descending ? packages.OrderByDescending(item => item.MileageCapKm) : packages.OrderBy(item => item.MileageCapKm))
                .ThenBy(item => item.Name),
            "price" => (descending ? packages.OrderByDescending(item => item.Price) : packages.OrderBy(item => item.Price))
                .ThenBy(item => item.Name),
            "grade" => (descending ? packages.OrderByDescending(item => item.MinimumGrade) : packages.OrderBy(item => item.MinimumGrade))
                .ThenBy(item => item.Name),
            "status" => (descending ? packages.OrderByDescending(item => item.IsActive) : packages.OrderBy(item => item.IsActive))
                .ThenBy(item => item.Name),
            _ => packages.OrderBy(item => item.Price).ThenBy(item => item.Name)
        };
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
