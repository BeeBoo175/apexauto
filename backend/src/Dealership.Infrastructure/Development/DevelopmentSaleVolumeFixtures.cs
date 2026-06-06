using Dealership.Infrastructure.Persistence;

namespace Dealership.Infrastructure.Development;

public sealed class DevelopmentSaleVolumeFixtures(DealershipDbContext database,
    DevelopmentReservationFixtures reservations, DevelopmentSaleFixtures sales)
{
    public async Task<IReadOnlyList<DevelopmentFixtureReport>> RunAsync(bool seed, CancellationToken cancellationToken)
    {
        var now = await database.DatabaseNowAsync(cancellationToken);
        var reports = new List<DevelopmentFixtureReport>();
        foreach (var scenario in DevelopmentSaleVolumeManifest.All)
        {
            var hold = await reservations.RunOneAsync(seed, scenario.Hold, now, cancellationToken);
            reports.Add(hold);
            if (hold.RequiresAttention)
            {
                continue;
            }

            reports.Add(await sales.RunOneAsync(seed, scenario, cancellationToken));
        }

        return reports;
    }
}
