using Dealership.Application.Accounts;
using Dealership.Application.Vehicles;
using Dealership.Domain.Inspections;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Development;

public sealed class DevelopmentPurchaseFixtures(DealershipDbContext database, PurchaseConfigurationService configuration)
{
    public static PurchaseSettingsRequest Settings { get; } = new(500m, 0.08m,
        DevelopmentFixtureManifest.Staff.Single(item => item.Role == AccountRoles.Administrator).Id);

    public static IReadOnlyList<PromoProvisioningRequest> Promos { get; } = Array.AsReadOnly<PromoProvisioningRequest>(
    [
        new(Guid.Parse("cc73032f-15fc-43e6-806a-58f3196db00e"), "APEX500", 500m,
            new DateTimeOffset(2030, 12, 31, 16, 0, 0, TimeSpan.Zero)),
        new(Guid.Parse("85f6b329-3b7d-4d7b-a433-fb80e22f6ddf"), "RAYA1000", 1000m,
            new DateTimeOffset(2030, 12, 31, 16, 0, 0, TimeSpan.Zero))
    ]);

    public static IReadOnlyList<WarrantyProvisioningRequest> Warranties { get; } = Array.AsReadOnly<WarrantyProvisioningRequest>(
    [
        new(Guid.Parse("5f46746f-1870-4e40-ac75-42be90328bb3"), "ApexShield Silver (Powertrain)", 12, 25000, 1200m,
            InspectionGrade.C),
        new(Guid.Parse("fb599022-6cb0-4f35-aaa2-ddf5ab729341"), "ApexShield Gold (Comprehensive)", 24, 50000, 2400m,
            InspectionGrade.B),
        new(Guid.Parse("c0479b58-bc39-4998-8b92-3d3ab0d46e49"), "ApexShield Platinum (Zero Deductible)", 36, 75000, 3600m,
            InspectionGrade.A)
    ]);

    public async Task<IReadOnlyList<DevelopmentFixtureReport>> RunAsync(bool seed, CancellationToken cancellationToken)
    {
        var reports = new List<DevelopmentFixtureReport>();
        if (seed)
        {
            var settings = await configuration.ProvisionSettingsAsync(Settings, cancellationToken);
            reports.Add(Report("Purchase settings", settings.Created, settings.MatchesRequestedState));
            foreach (var promo in Promos)
            {
                var result = await configuration.ProvisionPromoAsync(promo, cancellationToken);
                reports.Add(Report($"Promo ({promo.Code})", result.Created, result.MatchesRequestedState));
            }

            foreach (var warranty in Warranties)
            {
                var result = await configuration.ProvisionWarrantyAsync(warranty, cancellationToken);
                reports.Add(Report($"Warranty ({warranty.Name})", result.Created, result.MatchesRequestedState));
            }

            return reports;
        }

        var existing = await database.PurchaseSettings.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        reports.Add(Report("Purchase settings", false, existing is not null && PurchaseConfigurationStore.Matches(existing, Settings)));
        foreach (var requested in Promos)
        {
            var promo = await database.PromoCodes.AsNoTracking().SingleOrDefaultAsync(item => item.Id == requested.Id, cancellationToken);
            reports.Add(Report($"Promo ({requested.Code})", false, promo is not null && PurchaseConfigurationStore.Matches(promo, requested)));
        }

        foreach (var requested in Warranties)
        {
            var package = await database.WarrantyPackages.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == requested.Id, cancellationToken);
            var matches = package is not null && WarrantyAdministrationStore.Matches(package,
                new(requested.Id, requested.Name, requested.DurationMonths, requested.MileageCapKm, requested.Price,
                    requested.MinimumGrade, requested.IsActive));
            reports.Add(Report($"Warranty ({requested.Name})", false, matches));
        }

        return reports;
    }

    private static DevelopmentFixtureReport Report(string label, bool created, bool matches)
    {
        var state = created ? "created" : "matches";
        return new(label, matches ? state : "missing/drift; preserved", !matches);
    }
}
