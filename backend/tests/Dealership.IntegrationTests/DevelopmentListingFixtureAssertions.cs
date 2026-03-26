using System.Text.Json;
using Dealership.Application.Accounts;
using Dealership.Application.Vehicles;
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
        Assert.Equal(12, expected);
        Assert.Equal(expected, await database.ListingRevisions.CountAsync());
        Assert.Equal(expected, await database.ListingPublications.CountAsync());
        Assert.Equal(expected * 4, await database.ListingPhotos.CountAsync(item => item.AvailableAt != null));
        Assert.Equal(expected * 4, await database.ListingRevisionPhotos.CountAsync());
        Assert.All(await database.ListingRevisions.ToArrayAsync(), revision => Assert.Equal(4, revision.PhotoIds.Length));
        Assert.Empty(await database.ListingWithdrawals.ToListAsync());
        var original = await database.ListingRevisions.AsNoTracking().OrderBy(item => item.Id).FirstAsync();
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
