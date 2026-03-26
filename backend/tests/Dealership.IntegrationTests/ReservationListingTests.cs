using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dealership.Application.Accounts;
using Dealership.Application.Branches;
using Dealership.Application.Intake;
using Dealership.Application.Vehicles;
using Dealership.Domain.Intake;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Dealership.IntegrationTests.ReservationTestData;

namespace Dealership.IntegrationTests;

public sealed class ReservationListingTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task ManagerReadsAndActionsRetainScopedPageBoundedDeadlineRefundsAcrossReplayAndRestart()
    {
        var schedule = new InspectionSchedule(new(9, 0), new(18, 0), 15, 30, 0, Enum.GetValues<DayOfWeek>());
        using var data = await CheckInTestData.CreateAsync(fixture, schedule);
        using var scope = data.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var manager = await ProvisionAsync(services, data.BranchId, AccountRoles.BranchManager);
        var technician = await ProvisionAsync(services, data.BranchId, AccountRoles.WorkshopTechnician);
        var group = await ProvisionAsync(services, null, AccountRoles.GeneralManager);
        var outsideBranch = Guid.NewGuid();
        await services.GetRequiredService<BranchProvisioningService>().ProvisionBranchAsync(
            DevelopmentFixtureManifest.Branches[1] with { Id = outsideBranch }, CancellationToken.None);
        var outsider = await ProvisionAsync(services, outsideBranch, AccountRoles.BranchManager);
        var records = new List<ListingRecord>();
        for (var index = 0; index < 11; index++)
        {
            records.Add(await CreateListingAsync(data, services, manager, technician, index));
        }

        await using var clock = new ReservationTestClock(fixture.Database);
        var now = DateTimeOffset.UtcNow;
        await clock.InitializeAsync(now);
        clock.Maintenance.PurchaseSettings.Add(new(500m, 0.08m, manager, now));
        await clock.Maintenance.SaveChangesAsync();
        var otherCustomer = await RegisterCustomerAsync(services, clock.Maintenance);
        var first = records[0];
        var last = records[^1];
        var held = await HoldAsync(clock, data.OwnerId, first.Publication);
        var laterPage = await HoldAsync(clock, otherCustomer, last.Publication);
        await clock.AdvanceAsync(held.ExpiresAt.AddTicks(-10));
        await using (var before = clock.CreateContext())
        {
            var page = (await new ListingReadStore(before).ReadAsync(manager, new(PageSize: 10), CancellationToken.None)).Listings!;
            Assert.Equal(11, page.Total);
            Assert.False(Assert.Single(page.Items, item => item.VehicleId == first.Publication.VehicleId).IsPublic);
            Assert.False(await before.ReservationReleases.AnyAsync());
        }

        await clock.AdvanceAsync(held.ExpiresAt);
        await using (var denied = clock.CreateContext())
        {
            var reads = new ListingReadStore(denied);
            Assert.Equal(ListingStatus.Forbidden, (await reads.ReadAsync(technician, new(), CancellationToken.None)).Status);
            Assert.Equal(ListingStatus.Forbidden,
                (await reads.ReadAsync(outsider, new(data.BranchId), CancellationToken.None)).Status);
            Assert.Equal(ListingStatus.NotFound,
                (await reads.WorkspaceAsync(outsider, first.Publication.VehicleId, CancellationToken.None)).Status);
            Assert.Equal(ListingStatus.NotFound,
                (await reads.PhotosAsync(outsider, first.Publication.VehicleId, 1, 10, CancellationToken.None)).Status);
            Assert.Equal(ListingStatus.NotFound, (await new ListingService(new ListingStore(denied)).PublishAsync(outsider,
                first.Publication.VehicleId, new(Guid.NewGuid(), first.Revision.Id, first.Publication.SignedRevisionId),
                CancellationToken.None)).Status);
            Assert.Empty((await reads.ReadAsync(manager, new(Page: int.MaxValue), CancellationToken.None)).Listings!.Items);
            Assert.Empty((await reads.ReadAsync(manager, new(Search: "Unknown make"), CancellationToken.None)).Listings!.Items);
            Assert.False(await denied.ReservationReleases.AnyAsync());
        }

        await using (var firstReader = clock.CreateContext())
        await using (var competingReader = clock.CreateContext())
        {
            var pages = await Task.WhenAll(
                new ListingReadStore(firstReader).ReadAsync(manager, new(PageSize: 10), CancellationToken.None),
                new ListingReadStore(competingReader).ReadAsync(group, new(data.BranchId, PageSize: 10), CancellationToken.None));
            Assert.All(pages, result => Assert.Equal(ListingStatus.Succeeded, result.Status));
            Assert.All(pages, result => Assert.Equal(11, result.Listings!.Total));
            Assert.All(pages, result => Assert.True(result.Listings!.Items.All(item => item.IsPublic)));
            Assert.Equal(pages[0].Listings!.Items.Select(item => item.VehicleId),
                pages[1].Listings!.Items.Select(item => item.VehicleId));
            await AssertRefundAsync(firstReader, held);
            Assert.False(await firstReader.ReservationReleases.AnyAsync(item => item.ReservationId == laterPage.Id));
            var second = (await new ListingReadStore(firstReader).ReadAsync(manager,
                new(Page: 2, PageSize: 10), CancellationToken.None)).Listings!;
            Assert.Equal(last.Publication.VehicleId, Assert.Single(second.Items).VehicleId);
            Assert.True(second.Items[0].IsPublic);
            await AssertRefundAsync(firstReader, laterPage);
        }

        // Each entry initiates its own catch-up; no public/account read can refund on its behalf.
        foreach (var entry in Enum.GetValues<Entry>())
        {
            held = await HoldAsync(clock, data.OwnerId, first.Publication);
            await clock.AdvanceAsync(held.ExpiresAt);
            await using var reader = clock.CreateContext();
            await ExecuteAsync(reader, manager, first, entry);
            await AssertRefundAsync(reader, held);
            await using var restarted = clock.CreateContext();
            await ExecuteAsync(restarted, manager, first, entry);
            await AssertRefundAsync(restarted, held);
        }

        await AssertConcurrentReleaseAsync(clock, data.OwnerId, manager, first);
        await AssertRejectedWriteAsync(clock, data.OwnerId, manager, first);
        await AssertUnavailableAsync(clock, manager, first.Publication.VehicleId);
        await using var final = clock.CreateContext();
        Assert.All(await final.Vehicles.ToArrayAsync(), vehicle => Assert.Equal(VehicleStatus.Available, vehicle.Status));
        await ReservationExpiryAssertions.RunAsync(clock, services, data.OwnerId, records);
    }

    private async Task AssertUnavailableAsync(ReservationTestClock clock, Guid manager, Guid vehicleId)
    {
        var failures = new FailSnapshot();
        await using var source = clock.CreateContext();
        using var factory = fixture.CreateFactory().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<DealershipDbContext>>();
            services.RemoveAll<DealershipDbContext>();
            services.AddDbContext<DealershipDbContext>(options => options
                .UseNpgsql(source.Database.GetConnectionString()).AddInterceptors(failures));
        }));
        using var client = factory.CreateClient();
        var email = await source.Users.Where(user => user.Id == manager).Select(user => user.Email).SingleAsync();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(Guid.NewGuid(), email!, "Password123!"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = (await login.Content.ReadFromJsonAsync<LoginSession>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        foreach (var path in new[] { "/api/staff/listings", $"/api/staff/listings/{vehicleId}",
            $"/api/staff/listings/{vehicleId}/photos" })
        {
            var attempts = failures.Attempts;
            using var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
            Assert.Equal(3, failures.Attempts - attempts);
        }
    }

    private static async Task ExecuteAsync(DealershipDbContext database, Guid manager, ListingRecord listing, Entry entry)
    {
        var reads = new ListingReadStore(database);
        var vehicleId = listing.Publication.VehicleId;
        if (entry == Entry.Workspace)
        {
            var workspace = await reads.WorkspaceAsync(manager, vehicleId, CancellationToken.None);
            Assert.Equal(ListingStatus.Succeeded, workspace.Status);
            Assert.True(workspace.Workspace!.Vehicle.IsPublic);
            return;
        }

        if (entry == Entry.Photos)
        {
            Assert.Equal(ListingStatus.Succeeded, (await reads.PhotosAsync(manager, vehicleId, 1, 10, CancellationToken.None)).Status);
            return;
        }

        var service = new ListingService(new ListingStore(database));
        var revision = listing.Revision;
        var draft = new ListingDraftRequest(revision.Id, revision.Version - 1, revision.VehiclePrice, revision.RoadTax,
            revision.Transmission, revision.Fuel, revision.Description);
        var result = entry switch
        {
            Entry.DraftReplay => await service.SaveAsync(manager, vehicleId, draft, CancellationToken.None),
            Entry.DraftConflict => await service.SaveAsync(manager, vehicleId,
                draft with { OperationId = Guid.NewGuid(), ExpectedVersion = 99 }, CancellationToken.None),
            Entry.DraftInvalid => await service.SaveAsync(manager, vehicleId,
                draft with { OperationId = Guid.NewGuid(), VehiclePrice = -1m }, CancellationToken.None),
            Entry.PublicationReplay => await service.PublishAsync(manager, vehicleId,
                new(listing.Publication.Id, revision.Id, listing.Publication.SignedRevisionId), CancellationToken.None),
            Entry.PublicationConflict => await service.PublishAsync(manager, vehicleId,
                new(Guid.NewGuid(), Guid.NewGuid(), listing.Publication.SignedRevisionId), CancellationToken.None),
            _ => await service.WithdrawAsync(manager, vehicleId,
                new(Guid.NewGuid(), Guid.NewGuid(), "Review listing details"), CancellationToken.None)
        };
        var expected = entry switch
        {
            Entry.DraftReplay or Entry.PublicationReplay => ListingStatus.Succeeded,
            Entry.DraftInvalid => ListingStatus.Invalid,
            _ => ListingStatus.Conflict
        };
        Assert.Equal(expected, result.Status);
    }

    private static async Task AssertConcurrentReleaseAsync(ReservationTestClock clock, Guid customer, Guid manager,
        ListingRecord listing)
    {
        var held = await HoldAsync(clock, customer, listing.Publication);
        var race = new BeforeLock(async () =>
        {
            await using var writer = clock.CreateContext();
            Assert.Equal(ReservationStatus.Succeeded, (await new ReservationCancellationService(new ReservationCancellationStore(writer))
                .CancelAsync(customer, held.Id, new(Guid.NewGuid(), null), CancellationToken.None)).Status);
        });
        await using var source = clock.CreateContext();
        await using var reader = new DealershipDbContext(new DbContextOptionsBuilder<DealershipDbContext>()
            .UseNpgsql(source.Database.GetConnectionString()).AddInterceptors(race).Options);
        var result = await new ListingReadStore(reader).WorkspaceAsync(manager, listing.Publication.VehicleId, CancellationToken.None);
        Assert.True(race.Invoked);
        Assert.True(race.Attempts > 1);
        Assert.Equal(ListingStatus.Succeeded, result.Status);
        Assert.True(result.Workspace!.Vehicle.IsPublic);
        Assert.Equal(ReservationReleaseKind.CustomerCancelled,
            (await reader.ReservationReleases.SingleAsync(item => item.ReservationId == held.Id)).Kind);
    }

    private static async Task AssertRejectedWriteAsync(ReservationTestClock clock, Guid customer, Guid manager,
        ListingRecord listing)
    {
        var held = await HoldAsync(clock, customer, listing.Publication);
        await clock.AdvanceAsync(held.ExpiresAt);
        await using var source = clock.CreateContext();
        await using var writer = new DealershipDbContext(new DbContextOptionsBuilder<DealershipDbContext>()
            .UseNpgsql(source.Database.GetConnectionString()).AddInterceptors(new RejectListingWrite()).Options);
        var result = await new ListingService(new ListingStore(writer)).SaveAsync(manager, listing.Publication.VehicleId,
            new(Guid.NewGuid(), 1, 61000m, 90m, VehicleTransmission.Automatic, VehicleFuel.Petrol, "Updated approved vehicle"),
            CancellationToken.None);
        Assert.Equal(ListingStatus.Conflict, result.Status);
        await AssertRefundAsync(source, held);
        Assert.Equal(1, await source.ListingRevisions.CountAsync(item => item.VehicleId == listing.Publication.VehicleId));
    }

    private static async Task<Reservation> HoldAsync(ReservationTestClock clock, Guid customer, ListingPublication publication)
    {
        await using var writer = clock.CreateContext();
        var result = await new ReservationService(new ReservationStore(writer, new MailDeliverySignal(), new ReservationExpirySignal()))
            .ReserveAsync(customer, publication.VehicleId, new(Guid.NewGuid(), publication.Id, null), CancellationToken.None);
        Assert.Equal(ReservationStatus.Succeeded, result.Status);
        return result.Reservation!;
    }

    private static async Task AssertRefundAsync(DealershipDbContext database, Reservation held)
    {
        var refund = Assert.Single(await database.ReservationReleases.AsNoTracking()
            .Where(item => item.ReservationId == held.Id).ToArrayAsync());
        Assert.Equal(ReservationReleaseKind.Expired, refund.Kind);
        Assert.Equal(500m, refund.Refund);
        Assert.Equal(held.ExpiresAt, refund.RecordedAt);
        Assert.False(await database.Reservations.Where(item => item.Id == held.Id).Select(item => item.IsActive).SingleAsync());
    }

    private enum Entry
    {
        Workspace,
        Photos,
        DraftReplay,
        DraftConflict,
        DraftInvalid,
        PublicationReplay,
        PublicationConflict,
        WithdrawalConflict
    }

    private sealed class BeforeLock(Func<Task> cancel) : DbCommandInterceptor
    {
        public bool Invoked { get; private set; }

        public int Attempts { get; private set; }

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!command.CommandText.Contains("FROM \"Vehicles\"", StringComparison.Ordinal))
            {
                return result;
            }

            Attempts++;
            if (!Invoked)
            {
                Invoked = true;
                await cancel();
            }

            return result;
        }
    }

    private sealed class RejectListingWrite : DbCommandInterceptor
    {
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (!command.CommandText.Contains("INSERT INTO \"ListingRevisions\"", StringComparison.Ordinal))
            {
                return result;
            }

            await using var failure = command.Connection!.CreateCommand();
            failure.Transaction = command.Transaction;
            failure.CommandText = "DO $$ BEGIN RAISE EXCEPTION 'Competing listing operation' USING ERRCODE = '23505'; END $$";
            await failure.ExecuteNonQueryAsync(cancellationToken);
            return result;
        }
    }

    private sealed class FailSnapshot : DbCommandInterceptor
    {
        public int Attempts { get; private set; }

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (!command.CommandText.Contains("FROM \"AspNetUsers\"", StringComparison.Ordinal) ||
                command.Transaction?.IsolationLevel != System.Data.IsolationLevel.RepeatableRead)
            {
                return result;
            }

            Attempts++;
            await using var failure = command.Connection!.CreateCommand();
            failure.Transaction = command.Transaction;
            failure.CommandText = "DO $$ BEGIN RAISE EXCEPTION 'Concurrent reservation change' USING ERRCODE = '40001'; END $$";
            await failure.ExecuteNonQueryAsync(cancellationToken);
            return result;
        }
    }
}
