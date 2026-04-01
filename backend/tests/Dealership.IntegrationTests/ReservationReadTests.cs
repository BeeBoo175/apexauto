using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dealership.Application.Accounts;
using Dealership.Application.Vehicles;
using Dealership.Domain.Vehicles;
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
        using var otherData = await CheckInTestData.CreateAsync(fixture);
        var otherManager = await ProvisionAsync(services, otherData.BranchId, AccountRoles.BranchManager);
        var otherTechnician = await ProvisionAsync(services, otherData.BranchId, AccountRoles.WorkshopTechnician);
        var otherListing = await CreateListingAsync(otherData, services, otherManager, otherTechnician, 1);
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

        var outside = await HoldAsync(clock, otherData.OwnerId, otherListing.Publication);
        var expected = await database.Reservations.Where(item => item.CustomerId == data.OwnerId)
            .OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id).Select(item => item.Id).ToArrayAsync();
        Assert.Single(await database.Reservations.Select(item => item.CreatedAt).Distinct().ToArrayAsync());
        await database.Users.Where(item => item.Id == data.OwnerId)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.EmailConfirmed, false));

        using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/account/reservations")).StatusCode);
        await LoginAsync(client, data.CustomerEmail);
        var actual = new List<Guid>();
        for (var page = 1; page <= 3; page++)
        {
            var response = await client.GetAsync($"/api/account/reservations?page={page}&pageSize=50&isActive=false&customerId={otherData.OwnerId}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
            var history = (await response.Content.ReadFromJsonAsync<ReservationHistoryPage>())!;
            Assert.Equal(51, history.Total);
            Assert.Equal(25, history.PageSize);
            Assert.Equal(page == 3 ? 1 : 25, history.Items.Count);
            Assert.Equal(new CurrentActor(data.OwnerId, AccountRoles.Customer, null), history.Actor);
            Assert.Equal(active!.Id, history.Active!.Receipt.Id);
            Assert.True(history.Active.IsActive);
            actual.AddRange(history.Items.Select(item => item.Receipt.Id));
            Assert.All(history.Items, item => Assert.Equal(data.CustomerEmail, item.Receipt.Buyer.Email));
        }

        Assert.Equal(expected, actual);
        var beyond = (await client.GetFromJsonAsync<ReservationHistoryPage>("/api/account/reservations?page=2147483647"))!;
        Assert.Equal(51, beyond.Total);
        Assert.Empty(beyond.Items);
        Assert.Equal(active!.Id, beyond.Active!.Receipt.Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/account/reservations?page=0")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/staff/reservations")).StatusCode);
        await LoginAsync(client, (await database.Users.Where(item => item.Id == outsider).Select(item => item.Email).SingleAsync())!);
        Assert.Equal(0, (await client.GetFromJsonAsync<ReservationHistoryPage>("/api/account/reservations"))!.Total);
        await LoginAsync(client, data.StaffEmail);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/account/reservations")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/staff/reservations")).StatusCode);
        await LoginAsync(client, (await database.Users.Where(item => item.Id == manager).Select(item => item.Email).SingleAsync())!);
        await AssertFilteredPagesAsync(client, expected, active.Id);
        await ReservationSortingAssertions.VerifyHttpAsync(client, expected);
        await AssertManagerScopeAsync(client, database, manager, data.BranchId, otherData.BranchId);
        await AssertSnapshotAndRetryAsync(clock, data.OwnerId, active!, Array.IndexOf(expected, active!.Id) / 25 + 1);

        await clock.AdvanceAsync(now.AddSeconds(1));
        var final = await HoldAsync(clock, data.OwnerId, listing.Publication);
        await clock.AdvanceAsync(final.ExpiresAt.AddTicks(-10));
        await using (var before = clock.CreateContext())
        {
            var history = (await new ReservationReadStore(before).AccountAsync(data.OwnerId, 1, CancellationToken.None)).History!;
            Assert.True(Assert.Single(history.Items, item => item.Receipt.Id == final.Id).IsActive);
            Assert.False(await before.ReservationReleases.AnyAsync(item => item.ReservationId == final.Id));
        }

        await clock.AdvanceAsync(final.ExpiresAt);
        await using var firstRead = clock.CreateContext();
        await using var secondRead = clock.CreateContext();
        var concurrent = await Task.WhenAll(
            new ReservationReadStore(firstRead).AccountAsync(data.OwnerId, 1, CancellationToken.None),
            new ReservationReadStore(secondRead).StaffAsync(manager, new(), CancellationToken.None));
        Assert.All(concurrent, result => Assert.Equal(ReservationStatus.Succeeded, result.Status));
        var expired = Assert.Single(concurrent[0].History!.Items, item => item.Receipt.Id == final.Id);
        Assert.False(expired.IsActive);
        Assert.Equal("Expired", expired.Release!.Kind);
        Assert.Equal(500m, expired.Release.Refund);
        Assert.Equal(final.ExpiresAt, expired.Release.RecordedAt);
        Assert.Equal(ReservationReceipt.From(final), expired.Receipt);
        Assert.Equal(1, await database.ReservationReleases.CountAsync(item => item.ReservationId == final.Id));
        await using var outsideRead = clock.CreateContext();
        var outsideHistory = (await new ReservationReadStore(outsideRead).StaffAsync(manager,
            new(otherData.BranchId), CancellationToken.None)).History!;
        Assert.False(Assert.Single(outsideHistory.Items).IsActive);
        Assert.Equal(1, await database.ReservationReleases.CountAsync(item => item.ReservationId == outside.Id));
        Assert.Null(concurrent[0].History!.Active);
        await using var restarted = clock.CreateContext();
        var again = (await new ReservationReadStore(restarted).AccountAsync(data.OwnerId, 1, CancellationToken.None)).History!;
        Assert.Equal(expired, Assert.Single(again.Items, item => item.Receipt.Id == final.Id));
        Assert.All(await database.Vehicles.ToArrayAsync(), vehicle => Assert.Equal(VehicleStatus.Available, vehicle.Status));

        var filteredHold = await HoldAsync(clock, data.OwnerId, listing.Publication);
        await clock.AdvanceAsync(filteredHold.ExpiresAt);
        await using (var filtered = clock.CreateContext())
        {
            var otherScope = await new ReservationReadStore(filtered).StaffAsync(manager,
                new(otherData.BranchId, IsActive: false), CancellationToken.None);
            Assert.Equal(ReservationStatus.Succeeded, otherScope.Status);
            Assert.Single(otherScope.History!.Items);
            Assert.False(await filtered.ReservationReleases.AnyAsync(item => item.ReservationId == filteredHold.Id));
            // Even an empty requested page must reconcile the complete selected scope first.
            var history = await new ReservationReadStore(filtered).StaffAsync(manager,
                new(data.BranchId, Page: int.MaxValue, IsActive: false), CancellationToken.None);
            Assert.Equal(53, history.History!.Total);
            Assert.Empty(history.History.Items);
            var holds = await new ReservationReadStore(filtered).StaffAsync(manager,
                new(data.BranchId, IsActive: true), CancellationToken.None);
            Assert.Equal(0, holds.History!.Total);
            Assert.Empty(holds.History.Items);
        }
    }

    private static async Task AssertFilteredPagesAsync(HttpClient client, Guid[] expected, Guid activeId)
    {
        var active = (await client.GetFromJsonAsync<ReservationHistoryPage>("/api/staff/reservations?isActive=true"))!;
        Assert.Equal(1, active.Total);
        Assert.Equal(activeId, Assert.Single(active.Items).Receipt.Id);
        Assert.Null(active.Active);
        var actual = new List<Guid>();
        for (var page = 1; page <= 2; page++)
        {
            var response = await client.GetAsync($"/api/staff/reservations?isActive=false&page={page}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
            var history = (await response.Content.ReadFromJsonAsync<ReservationHistoryPage>())!;
            Assert.Equal(50, history.Total);
            Assert.Equal(25, history.Items.Count);
            Assert.All(history.Items, item =>
            {
                Assert.False(item.IsActive);
                Assert.Equal(500m, item.Release!.Refund);
            });
            actual.AddRange(history.Items.Select(item => item.Receipt.Id));
        }

        Assert.Equal(expected.Where(id => id != activeId), actual);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/staff/reservations?isActive=unknown")).StatusCode);
    }

    private static async Task AssertManagerScopeAsync(HttpClient client, DealershipDbContext database, Guid manager,
        Guid branchId, Guid outsideBranchId)
    {
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/account/reservations")).StatusCode);
        int[] sizes = [10, 25, 50];
        foreach (var size in sizes)
        {
            var history = (await client.GetFromJsonAsync<ReservationHistoryPage>($"/api/staff/reservations?pageSize={size}"))!;
            Assert.Equal(51, history.Total);
            Assert.Equal(size, history.Items.Count);
            Assert.All(history.Items, item => Assert.Equal(branchId, item.Receipt.BranchId));
        }

        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/staff/reservations?pageSize=51")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/staff/reservations?branchId={outsideBranchId}")).StatusCode);
        await database.Users.Where(item => item.Id == manager)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.BranchId, outsideBranchId));
        var reassigned = (await client.GetFromJsonAsync<ReservationHistoryPage>("/api/staff/reservations"))!;
        Assert.Equal(1, reassigned.Total);
        Assert.Equal(outsideBranchId, reassigned.Actor.BranchId);
        await database.Users.Where(item => item.Id == manager)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.BranchId, branchId));
        await database.Branches.Where(item => item.Id == branchId)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.IsActive, false));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/staff/reservations")).StatusCode);
        Assert.Equal(ReservationStatus.Forbidden,
            (await new ReservationReadStore(database).StaffAsync(manager, new(), CancellationToken.None)).Status);
        await database.Users.Where(item => item.Id == manager).ExecuteUpdateAsync(update =>
            update.SetProperty(item => item.Role, AccountRoles.GeneralManager).SetProperty(item => item.BranchId, (Guid?)null));
        var group = (await client.GetFromJsonAsync<ReservationHistoryPage>("/api/staff/reservations"))!;
        Assert.Equal(52, group.Total);
        Assert.Equal(AccountRoles.GeneralManager, group.Actor.Role);
        Assert.Equal(1, (await client.GetFromJsonAsync<ReservationHistoryPage>(
            $"/api/staff/reservations?branchId={outsideBranchId}"))!.Total);
        await database.Users.Where(item => item.Id == manager)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.Role, AccountRoles.Administrator));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/staff/reservations")).StatusCode);
        await database.Users.Where(item => item.Id == manager)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.Role, AccountRoles.GeneralManager).SetProperty(item => item.IsActive, false));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/staff/reservations")).StatusCode);
        await database.Users.Where(item => item.Id == manager).ExecuteUpdateAsync(update => update.SetProperty(item => item.IsActive, true));
        await database.Branches.Where(item => item.Id == branchId).ExecuteUpdateAsync(update => update.SetProperty(item => item.IsActive, true));
        var noLocks = new RejectReconciliation();
        await using var emptyPage = new DealershipDbContext(new DbContextOptionsBuilder<DealershipDbContext>()
            .UseNpgsql(database.Database.GetConnectionString()).AddInterceptors(noLocks).Options);
        var beyond = (await new ReservationReadStore(emptyPage).StaffAsync(manager,
            new(Page: int.MaxValue), CancellationToken.None)).History!;
        Assert.Equal(52, beyond.Total);
        Assert.Empty(beyond.Items);
        Assert.Null(beyond.Active);
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
            Assert.Equal(25, snapshot.History.Items.Count);
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

    private sealed class RejectReconciliation : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Assert.DoesNotContain("FOR UPDATE", command.CommandText, StringComparison.Ordinal);
            Assert.DoesNotContain("release_unavailable_reservation", command.CommandText, StringComparison.Ordinal);
            return ValueTask.FromResult(result);
        }
    }
}
