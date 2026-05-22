using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Development;

public sealed record DevelopmentFixtureReport(string Fixture, string State, bool RequiresAttention);

public sealed class DevelopmentFixtureRunner(DealershipDbContext database, DevelopmentAccountFixtures accounts,
    DevelopmentIntakeFixtures intake,
    DevelopmentInspectionFixtures inspections, DevelopmentListingFixtures listings, DevelopmentPurchaseFixtures purchases,
    DevelopmentReservationFixtures reservations, DevelopmentSaleFixtures sales,
    DevelopmentFixtureMail mail)
{
    public async Task<IReadOnlyList<DevelopmentFixtureReport>> SeedAsync(DevelopmentFixtureTarget target,
        CancellationToken cancellationToken, bool deliverMail = false)
    {
        target.EnsureMatches(database.Database.GetConnectionString());
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(2));
        var token = deadline.Token;
        await using var lockConnection = await DevelopmentFixtureLock.AcquireAsync(target, token);
        // One bounded session avoids reopening unpooled connections for every canonical command/read.
        var opened = database.Database.GetDbConnection().State != System.Data.ConnectionState.Open;
        if (opened)
        {
            await database.Database.OpenConnectionAsync(token);
        }
        try
        {
            await intake.EnsureLocalStorageAsync(token);
            if (!deliverMail)
            {
                await mail.CaptureAsync(token);
            }

            try
            {
                return await SeedFixturesAsync(target, token);
            }
            finally
            {
                if (!deliverMail)
                {
                    // Committed fixture units survive interruption; suppress their mail even when a later unit fails.
                    using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    await mail.CompleteAsync(cleanup.Token);
                }
            }
        }
        finally
        {
            if (opened)
            {
                await database.Database.CloseConnectionAsync();
            }
        }
    }

    private async Task<IReadOnlyList<DevelopmentFixtureReport>> SeedFixturesAsync(DevelopmentFixtureTarget target,
        CancellationToken token)
    {
        // A dedicated nonpooled connection owns the run lock; disposal releases it after failure or restart.
        var reports = new List<DevelopmentFixtureReport>(await accounts.RunAsync(true, token));

        reports.AddRange(await purchases.RunAsync(true, token));
        reports.AddRange(await intake.SeedAsync(target, token));
        reports.AddRange(await inspections.RunAsync(true, token));
        reports.AddRange(await listings.RunAsync(true, token));
        reports.AddRange(await reservations.RunAsync(true, token));
        return reports;
    }

    public async Task<IReadOnlyList<DevelopmentFixtureReport>> ReviewAsync(DevelopmentFixtureTarget target,
        CancellationToken cancellationToken)
    {
        target.EnsureMatches(database.Database.GetConnectionString());
        // One bounded session avoids reopening unpooled connections for every canonical command/read.
        var opened = database.Database.GetDbConnection().State != System.Data.ConnectionState.Open;
        if (opened)
        {
            await database.Database.OpenConnectionAsync(cancellationToken);
        }
        try
        {
            var reports = new List<DevelopmentFixtureReport>(await accounts.RunAsync(false, cancellationToken));

            reports.AddRange(await purchases.RunAsync(false, cancellationToken));
            reports.AddRange(await intake.ReviewAsync(cancellationToken));
            reports.AddRange(await inspections.RunAsync(false, cancellationToken));
            reports.AddRange(await listings.RunAsync(false, cancellationToken));
            reports.AddRange(await reservations.RunAsync(false, cancellationToken));
            reports.AddRange(await sales.RunAsync(false, cancellationToken));
            return reports;
        }
        finally
        {
            if (opened)
            {
                await database.Database.CloseConnectionAsync();
            }
        }
    }

}
