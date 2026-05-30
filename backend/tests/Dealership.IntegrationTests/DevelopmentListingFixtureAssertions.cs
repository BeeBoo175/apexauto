using System.Text.Json;
using Dealership.Application.Accounts;
using Dealership.Application.Vehicles;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

internal static class DevelopmentListingFixtureAssertions
{
    public static async Task VerifyAsync(IServiceProvider provider, DevelopmentFixtureTarget target)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var database = services.GetRequiredService<DealershipDbContext>();
        var expected = DevelopmentInspectionScenarios.All.Count(item => item.PublishListing);
        Assert.Equal(54, expected);
        Assert.Equal(expected, await database.ListingRevisions.CountAsync());
        Assert.Equal(expected, await database.ListingPublications.CountAsync());
        Assert.Equal(expected * 4, await database.ListingPhotos.CountAsync(item => item.AvailableAt != null));
        Assert.Equal(expected * 4, await database.ListingRevisionPhotos.CountAsync());
        Assert.All(await database.ListingRevisions.ToArrayAsync(), revision => Assert.Equal(4, revision.PhotoIds.Length));
        Assert.Equal(DevelopmentTestDriveWithdrawalFixtures.WithdrawalId,
            (await database.ListingWithdrawals.SingleAsync()).Id);
        var original = await database.ListingRevisions.AsNoTracking().Where(item => database.Vehicles.Any(vehicle =>
            vehicle.Id == item.VehicleId && vehicle.Status == VehicleStatus.Available))
            .OrderBy(item => item.Id).FirstAsync();
        var manager = DevelopmentFixtureManifest.Staff.Single(item =>
            item.BranchId == original.BranchId && item.Role == AccountRoles.BranchManager);
        var edit = new ListingDraftRequest(Guid.NewGuid(), original.Version,
            original.VehiclePrice + 100m, original.RoadTax, original.Transmission, original.Fuel,
            "Manager updated this listing; seed reruns must preserve it.");
        var saved = await services.GetRequiredService<ListingService>()
            .SaveAsync(manager.Id, original.VehicleId, edit, CancellationToken.None);
        Assert.Equal(ListingStatus.Succeeded, saved.Status);
        var before = await SnapshotAsync(database);
        var reports = await services.GetRequiredService<DevelopmentFixtureRunner>().SeedAsync(target, CancellationToken.None);
        Assert.Contains(reports, item => item.Fixture.StartsWith("Listing (", StringComparison.Ordinal) &&
            item.State == "listing drift; preserved" && item.RequiresAttention);
        Assert.Equal(before, await SnapshotAsync(database));
    }

    public static async Task VerifyCatalogueAsync(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var database = services.GetRequiredService<DealershipDbContext>();
        var store = services.GetRequiredService<IPublicListingStore>();
        var managers = services.GetRequiredService<IListingReadStore>();
        foreach (var branch in DevelopmentFixtureManifest.Branches)
        {
            const int available = 15;
            var expected = await database.ListingRevisions.Where(item => item.BranchId == branch.Id)
                .Where(item => database.Vehicles.Any(vehicle => vehicle.Id == item.VehicleId &&
                    vehicle.Status == VehicleStatus.Available))
                .Where(item => !database.Reservations.Any(hold => hold.VehicleId == item.VehicleId && hold.IsActive))
                .Where(item => !database.ListingWithdrawals.Any(withdrawal => withdrawal.VehicleId == item.VehicleId))
                .OrderBy(item => item.VehiclePrice).ThenBy(item => item.VehicleId).ToArrayAsync();
            Assert.Equal(available, expected.Length);
            Assert.Contains(expected.GroupBy(item => item.VehiclePrice), group => group.Count() > 1);
            var actual = new List<PublicListingItem>();
            var filter = new PublicListingFilter(BranchId: branch.Id, Sort: "price", Direction: "asc");
            for (var page = 1; page <= 2; page++)
            {
                var result = await store.ReadAsync(filter with { Page = page }, CancellationToken.None);
                Assert.Equal(ListingStatus.Succeeded, result.Status);
                Assert.Equal(available, result.Catalogue!.Total);
                Assert.Equal(Math.Min(8, available - (page - 1) * 8), result.Catalogue.Items.Count);
                Assert.All(result.Catalogue.Items, item => Assert.Equal(branch.Id, item.BranchId));
                actual.AddRange(result.Catalogue.Items);
            }

            Assert.Equal(expected.Select(item => item.VehicleId), actual.Select(item => item.Id));
            Assert.Equal(6, actual.Select(item => item.Make).Distinct().Count());
            Assert.Equal(actual.Take(8).Select(item => item.Id),
                (await store.ReadAsync(filter, CancellationToken.None)).Catalogue!.Items.Select(item => item.Id));
            Assert.Empty((await store.ReadAsync(filter with { Page = 3 }, CancellationToken.None)).Catalogue!.Items);
            var electric = await store.ReadAsync(filter with { Fuel = VehicleFuel.Electric }, CancellationToken.None);
            Assert.Equal(2, electric.Catalogue!.Total);
            Assert.All(electric.Catalogue.Items, item => Assert.Equal("BYD", item.Make));
            var detail = await store.DetailAsync(actual[0].Id, CancellationToken.None);
            Assert.Equal(ListingStatus.Succeeded, detail.Status);
            Assert.Equal(210, detail.Detail!.Checklist.Points.Count);
            Assert.Equal(4, detail.Detail.PhotoIds.Count);

            var manager = DevelopmentFixtureManifest.Staff.Single(item =>
                item.BranchId == branch.Id && item.Role == AccountRoles.BranchManager);
            var workspace = await managers.ReadAsync(manager.Id, new(PageSize: 50), CancellationToken.None);
            Assert.Equal(33, workspace.Listings!.Total);
            var second = await managers.ReadAsync(manager.Id, new(Page: 2, PageSize: 50), CancellationToken.None);
            Assert.Equal(available, workspace.Listings.Items.Concat(second.Listings!.Items).Count(item => item.IsPublic));
        }

        Assert.Equal(45, (await store.ReadAsync(new(), CancellationToken.None)).Catalogue!.Total);
    }

    private static async Task<string> SnapshotAsync(DealershipDbContext database)
    {
        var revisions = await database.ListingRevisions.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync();
        var publications = await database.ListingPublications.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync();
        var withdrawals = await database.ListingWithdrawals.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync();
        var photos = await database.ListingPhotos.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync();
        var attachments = await database.ListingRevisionPhotos.AsNoTracking()
            .OrderBy(item => item.RevisionId).ThenBy(item => item.Position).ToArrayAsync();
        return JsonSerializer.Serialize(new { revisions, publications, withdrawals, photos, attachments });
    }
}
