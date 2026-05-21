using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dealership.Application.Accounts;
using Dealership.Application.Common;
using Dealership.Application.Vehicles;
using Dealership.Domain.Vehicles;
using Dealership.Domain.Common;
using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using static Dealership.IntegrationTests.ReservationTestData;

namespace Dealership.IntegrationTests;

public sealed class ReservationReadTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task ScopedHistoryPagesRetainedReceiptsAndReconcilesDeadlineAcrossRestartAndConcurrentReads()
    {
        using var data = await CheckInTestData.CreateAsync(fixture);
        using var scope = data.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var manager = await ProvisionAsync(services, data.BranchId, AccountRoles.BranchManager);
        var technician = await ProvisionAsync(services, data.BranchId, AccountRoles.WorkshopTechnician);
        var listing = await CreateListingAsync(data, services, manager, technician, 0);
        await using var database = NewContext();
        await database.Branches.Where(item => item.Id == data.BranchId)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.Name, "ApexAuto Petaling Jaya"));
        database.PurchaseSettings.Add(new(500m, 0.08m, manager, DateTimeOffset.UtcNow));
        await database.SaveChangesAsync();
        var outsider = await RegisterCustomerAsync(services, database);
        await using var clock = new ReservationTestClock(fixture.Database);
        var now = DateTimeOffset.UtcNow;
        await clock.InitializeAsync(now);

        Reservation? active = null;
        // One production listing supplies full/full/partial history without repeating intake setup.
        for (var index = 0; index < 51; index++)
        {
            active = await HoldAsync(clock, data.OwnerId, listing.Publication);
            if (index < 50)
            {
                await CancelAsync(clock, data.OwnerId, active.Id);
            }
        }

        var reads = new ReservationReadStore(database);
        foreach (var denied in new[] { manager, data.StaffId, technician, Guid.NewGuid() })
        {
            Assert.Equal(ReservationStatus.Forbidden,
                (await reads.AccountAsync(denied, 1, CancellationToken.None)).Status);
        }
        var expected = await database.Reservations.Where(item => item.CustomerId == data.OwnerId)
            .OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id).Select(item => item.Id).ToArrayAsync();
        Assert.Single(await database.Reservations.Select(item => item.CreatedAt).Distinct().ToArrayAsync());
        await database.Users.Where(item => item.Id == data.OwnerId)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.EmailConfirmed, false));

        using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/account/reservations")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/staff/reservations")).StatusCode);
        await LoginAsync(client, data.CustomerEmail);
        var actual = new List<Guid>();
        var pageSize = ReservationHistory.PageSize;
        var pages = (51 + pageSize - 1) / pageSize;
        for (var page = 1; page <= pages; page++)
        {
            var response = await client.GetAsync($"/api/account/reservations?page={page}&pageSize=50&isActive=false&customerId={outsider}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
            Assert.DoesNotContain("registrationPlate", await response.Content.ReadAsStringAsync());
            var history = (await response.Content.ReadFromJsonAsync<ReservationHistoryPage>())!;
            Assert.Equal(51, history.Total);
            Assert.Equal(pageSize, history.PageSize);
            Assert.Equal(page == pages ? 51 - pageSize * (pages - 1) : pageSize, history.Items.Count);
            Assert.Equal(new CurrentActor(data.OwnerId, AccountRoles.Customer, null), history.Actor);
            Assert.Equal(active!.Id, history.Active!.Receipt.Id);
            Assert.True(history.Active.IsActive);
            actual.AddRange(history.Items.Select(item => item.Receipt.Id));
            Assert.All(history.Items, item => Assert.Equal(data.CustomerEmail, item.Receipt.Buyer.Email));
        }

        Assert.Equal(expected, actual);
        var filtered = (await client.GetFromJsonAsync<ReservationHistoryPage>(
            "/api/account/reservations?from=2000-01-01&to=2000-01-02&search=no-match"))!;
        Assert.Empty(filtered.Items);
        Assert.Equal(0, filtered.Total);
        Assert.Equal(active!.Id, filtered.Active!.Receipt.Id);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.GetAsync("/api/account/reservations?from=2000-01-01")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.GetAsync($"/api/account/reservations?search={new string('x', 161)}")).StatusCode);
        var beyond = (await client.GetFromJsonAsync<ReservationHistoryPage>("/api/account/reservations?page=2147483647"))!;
        Assert.Equal(51, beyond.Total);
        Assert.Empty(beyond.Items);
        Assert.Equal(active!.Id, beyond.Active!.Receipt.Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/account/reservations?page=0")).StatusCode);
        await LoginAsync(client, (await database.Users.Where(item => item.Id == outsider).Select(item => item.Email).SingleAsync())!);
        Assert.Equal(0, (await client.GetFromJsonAsync<ReservationHistoryPage>("/api/account/reservations"))!.Total);
        await LoginAsync(client, data.StaffEmail);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/account/reservations")).StatusCode);
        await AssertSnapshotAndRetryAsync(clock, data.OwnerId, active!, Array.IndexOf(expected, active!.Id) / ReservationHistory.PageSize + 1);

        await clock.AdvanceAsync(now.AddSeconds(1));
        var final = await HoldAsync(clock, data.OwnerId, listing.Publication);
        await clock.AdvanceAsync(final.ExpiresAt.AddTicks(-10));
        await using (var before = clock.CreateContext())
        {
            var history = (await new ReservationReadStore(before).AccountAsync(data.OwnerId, 1, CancellationToken.None)).History!;
            Assert.True(Assert.Single(history.Items, item => item.Receipt.Id == final.Id).IsActive);
            Assert.False(await before.ReservationReleases.AnyAsync(item => item.ReservationId == final.Id));
        }

        // Concurrent reads at the deadline reconcile the hold once, and a restarted read sees the same refund.
        await clock.AdvanceAsync(final.ExpiresAt);
        await using var firstRead = clock.CreateContext();
        await using var secondRead = clock.CreateContext();
        var concurrent = await Task.WhenAll(
            new ReservationReadStore(firstRead).AccountAsync(data.OwnerId, 1, CancellationToken.None),
            new ReservationReadStore(secondRead).AccountAsync(data.OwnerId, 1, CancellationToken.None));
        Assert.All(concurrent, result => Assert.Equal(ReservationStatus.Succeeded, result.Status));
        var expired = Assert.Single(concurrent[0].History!.Items, item => item.Receipt.Id == final.Id);
        Assert.False(expired.IsActive);
        Assert.Equal("Expired", expired.Release!.Kind);
        Assert.Equal(500m, expired.Release.Refund);
        Assert.Equal(final.ExpiresAt, expired.Release.RecordedAt);
        Assert.Equal(ReservationReceipt.From(final), expired.Receipt);
        Assert.Equal(expired, Assert.Single(concurrent[1].History!.Items, item => item.Receipt.Id == final.Id));
        Assert.Equal(1, await database.ReservationReleases.CountAsync(item => item.ReservationId == final.Id));
        Assert.Null(concurrent[0].History!.Active);
        await using var restarted = clock.CreateContext();
        var again = (await new ReservationReadStore(restarted).AccountAsync(data.OwnerId, 1, CancellationToken.None)).History!;
        Assert.Equal(expired, Assert.Single(again.Items, item => item.Receipt.Id == final.Id));
        var malaysia = TimeZoneInfo.FindSystemTimeZoneById(DealershipLocale.MalaysiaTimeZoneId);
        var finalDay = TimeZoneInfo.ConvertTime(final.CreatedAt, malaysia).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var inRange = (await new ReservationReadStore(restarted).AccountAsync(data.OwnerId, 1, CancellationToken.None,
            ListDateRange.Parse(finalDay, finalDay))).History!;
        Assert.Contains(inRange.Items, item => item.Receipt.Id == final.Id);
        Assert.All(inRange.Items, item => Assert.Equal(finalDay,
            TimeZoneInfo.ConvertTime(item.Receipt.CreatedAt, malaysia).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        var outOfRange = (await new ReservationReadStore(restarted).AccountAsync(data.OwnerId, 1, CancellationToken.None,
            ListDateRange.Parse("2000-01-01", "2000-01-02"))).History!;
        Assert.Equal(0, outOfRange.Total);
        var byNumber = (await new ReservationReadStore(restarted).AccountAsync(data.OwnerId, 1, CancellationToken.None,
            search: final.Number.ToLowerInvariant())).History!;
        Assert.Equal(final.Id, Assert.Single(byNumber.Items).Receipt.Id);
        Assert.Equal(0, (await new ReservationReadStore(restarted).AccountAsync(data.OwnerId, 1, CancellationToken.None,
            search: "no such reservation")).History!.Total);
        Assert.Empty(outOfRange.Items);
        Assert.All(await database.Vehicles.ToArrayAsync(), vehicle => Assert.Equal(VehicleStatus.Available, vehicle.Status));
    }

    private async Task AssertSnapshotAndRetryAsync(ReservationTestClock clock, Guid customerId, Reservation active, int activePage)
    {
        var race = new AfterRead("FROM \"AspNetUsers\"", async () => await CancelAsync(clock, customerId, active.Id));
        await using (var database = NewContext(race))
        {
            var result = await new ReservationReadStore(database).AccountAsync(customerId, activePage, CancellationToken.None);
            Assert.True(race.Invoked);
            Assert.Equal(ReservationStatus.Succeeded, result.Status);
            var item = Assert.Single(result.History!.Items, item => item.Receipt.Id == active.Id);
            Assert.False(item.IsActive);
            Assert.Equal("CustomerCancelled", item.Release!.Kind);
        }

        var changedActor = new AfterRead("SELECT count(*)", async () =>
        {
            await using var writer = NewContext();
            await writer.Users.Where(item => item.Id == customerId)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.IsActive, false));
        });
        await using (var database = NewContext(changedActor))
        {
            var snapshot = await new ReservationReadStore(database).AccountAsync(customerId, 1, CancellationToken.None);
            Assert.True(changedActor.Invoked);
            Assert.Equal(ReservationStatus.Succeeded, snapshot.Status);
            Assert.Equal(51, snapshot.History!.Total);
            Assert.Equal(ReservationHistory.PageSize, snapshot.History.Items.Count);
            Assert.Equal(ReservationStatus.Forbidden,
                (await new ReservationReadStore(database).AccountAsync(customerId, 1, CancellationToken.None)).Status);
        }

        await using var restore = NewContext();
        await restore.Users.Where(item => item.Id == customerId).ExecuteUpdateAsync(update =>
            update.SetProperty(item => item.IsActive, true).SetProperty(item => item.EmailConfirmed, true));
    }

    private static async Task<Reservation> HoldAsync(ReservationTestClock clock, Guid customerId, ListingPublication publication)
    {
        await using var database = clock.CreateContext();
        var result = await new ReservationService(new ReservationStore(database, new MailDeliverySignal(), new ReservationExpirySignal()))
            .ReserveAsync(customerId, publication.VehicleId, new(Guid.NewGuid(), publication.Id, null), CancellationToken.None);
        Assert.Equal(ReservationStatus.Succeeded, result.Status);
        return result.Reservation!;
    }

    private static async Task CancelAsync(ReservationTestClock clock, Guid customerId, Guid reservationId)
    {
        await using var database = clock.CreateContext();
        var result = await new ReservationCancellationService(new ReservationCancellationStore(database))
            .CancelAsync(customerId, reservationId, new(Guid.NewGuid(), null), CancellationToken.None);
        Assert.Equal(ReservationStatus.Succeeded, result.Status);
    }

    private DealershipDbContext NewContext(IInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<DealershipDbContext>().UseNpgsql(fixture.Database.RuntimeConnectionString);
        if (interceptor is not null)
        {
            options.AddInterceptors(interceptor);
        }

        return new(options.Options);
    }

    private static async Task LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(Guid.NewGuid(), email, "Password123!"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = (await response.Content.ReadFromJsonAsync<LoginSession>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
    }

    private sealed class AfterRead(string match, Func<Task> action) : DbCommandInterceptor
    {
        public bool Invoked { get; private set; }

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command,
            CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (!Invoked && command.CommandText.Contains(match, StringComparison.Ordinal))
            {
                Invoked = true;
                await action();
            }

            return result;
        }
    }
}
