using System.Diagnostics;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Development;

public sealed record DevelopmentFixtureReport(string Fixture, string State, bool RequiresAttention);

public sealed record DevelopmentFixtureTiming(string Unit, TimeSpan Elapsed);

public sealed class DevelopmentFixtureRunner(DealershipDbContext database, DevelopmentAccountFixtures accounts,
    DevelopmentIntakeFixtures intake,
    DevelopmentInspectionFixtures inspections, DevelopmentListingFixtures listings, DevelopmentPurchaseFixtures purchases,
    DevelopmentReservationFixtures reservations, DevelopmentSaleFixtures sales,
    DevelopmentFixtureMail mail, DevelopmentTestDriveFixtures testDrives, DevelopmentSaleVolumeFixtures salesVolume,
    DevelopmentAccountPictureFixtures accountPictures)
{
    private readonly List<DevelopmentFixtureTiming> timings = [];

    // Timings describe the latest seed or review run; reports stay free of operational measurements.
    public IReadOnlyList<DevelopmentFixtureTiming> Timings => timings;

    public async Task<IReadOnlyList<DevelopmentFixtureReport>> SeedAsync(DevelopmentFixtureTarget target,
        CancellationToken cancellationToken, bool deliverMail = false)
    {
        target.EnsureMatches(database.Database.GetConnectionString());
        timings.Clear();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(5));
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
        var reports = new List<DevelopmentFixtureReport>();

        reports.AddRange(await TimeAsync("Accounts", () => accounts.RunAsync(true, token)));
        reports.AddRange(await TimeAsync("Account pictures", () => accountPictures.RunAsync(true, token)));
        reports.AddRange(await TimeAsync("Purchase settings", () => purchases.RunAsync(true, token)));
        reports.AddRange(await TimeAsync("Intake", () => intake.SeedAsync(target, token)));
        reports.AddRange(await TimeAsync("Inspections", () => inspections.RunAsync(true, token)));
        reports.AddRange(await TimeAsync("Listings", () => listings.RunAsync(true, token)));
        reports.AddRange(await TimeAsync("Reservations", () => reservations.RunAsync(true, token)));
        reports.AddRange(await TimeAsync("Test drives", () => testDrives.RunAsync(true, token)));
        reports.AddRange(await TimeAsync("Sales", () => salesVolume.RunAsync(true, token)));
        return reports;
    }

    public async Task<IReadOnlyList<DevelopmentFixtureReport>> ReviewAsync(DevelopmentFixtureTarget target,
        CancellationToken cancellationToken)
    {
        target.EnsureMatches(database.Database.GetConnectionString());
        timings.Clear();
        // One bounded session avoids reopening unpooled connections for every canonical command/read.
        var opened = database.Database.GetDbConnection().State != System.Data.ConnectionState.Open;
        if (opened)
        {
            await database.Database.OpenConnectionAsync(cancellationToken);
        }
        try
        {
            var reports = new List<DevelopmentFixtureReport>();

            reports.AddRange(await TimeAsync("Accounts", () => accounts.RunAsync(false, cancellationToken)));
            reports.AddRange(await TimeAsync("Account pictures", () => accountPictures.RunAsync(false, cancellationToken)));
            reports.AddRange(await TimeAsync("Purchase settings", () => purchases.RunAsync(false, cancellationToken)));
            reports.AddRange(await TimeAsync("Intake", () => intake.ReviewAsync(cancellationToken)));
            reports.AddRange(await TimeAsync("Inspections", () => inspections.RunAsync(false, cancellationToken)));
            reports.AddRange(await TimeAsync("Listings", () => listings.RunAsync(false, cancellationToken)));
            reports.AddRange(await TimeAsync("Reservations", () => reservations.RunAsync(false, cancellationToken)));
            reports.AddRange(await TimeAsync("Sales", () => sales.RunAsync(false, cancellationToken)));
            reports.AddRange(await TimeAsync("Test drives", () => testDrives.RunAsync(false, cancellationToken)));
            reports.AddRange(await TimeAsync("Sales volume", () => salesVolume.RunAsync(false, cancellationToken)));
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

    private async Task<IReadOnlyList<DevelopmentFixtureReport>> TimeAsync(string unit,
        Func<Task<IReadOnlyList<DevelopmentFixtureReport>>> run)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            return await run();
        }
        finally
        {
            timings.Add(new(unit, stopwatch.Elapsed));
        }
    }
}
