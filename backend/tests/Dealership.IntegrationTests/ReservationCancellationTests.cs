using Dealership.Application.Accounts;
using Dealership.Application.Branches;
using Dealership.Application.Vehicles;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Dealership.IntegrationTests.ReservationTestData;

namespace Dealership.IntegrationTests;

public sealed class ReservationCancellationTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task ProductionCancellationRetainsScopedExactRefundsAndGivesExpiryPrecedence()
    {
        using var data = await CheckInTestData.CreateAsync(fixture);
        using var scope = data.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var manager = await ProvisionAsync(services, data.BranchId, AccountRoles.BranchManager);
        var technician = await ProvisionAsync(services, data.BranchId, AccountRoles.WorkshopTechnician);
        var generalManager = await ProvisionAsync(services, null, AccountRoles.GeneralManager);
        var administrator = await ProvisionAsync(services, null, AccountRoles.Administrator);
        var branchId = Guid.NewGuid();
        await services.GetRequiredService<BranchProvisioningService>().ProvisionBranchAsync(
            DevelopmentFixtureManifest.Branches[1] with { Id = branchId }, CancellationToken.None);
        var outside = await ProvisionAsync(services, branchId, AccountRoles.BranchManager);
        var listing = await CreateListingAsync(data, services, manager, technician, 0);
        await using var database = NewContext();
        database.PurchaseSettings.Add(new(500m, 0.08m, manager, DateTimeOffset.UtcNow));
        await database.SaveChangesAsync();
        var other = await RegisterCustomerAsync(services, database);
        var held = await HoldAsync(data, listing.Publication);
        var own = new ReservationCancellationRequest(Guid.NewGuid(), null);
        var staff = new ReservationCancellationRequest(Guid.NewGuid(), "Customer requested cancellation");
        Assert.Equal(ReservationStatus.Invalid, (await CancelAsync(data, data.OwnerId, held.Id, own with { OperationId = Guid.Empty })).Status);
        Assert.Equal(ReservationStatus.NotFound, (await CancelAsync(data, other, held.Id, own)).Status);
        Assert.Equal(ReservationStatus.Forbidden, (await CancelAsync(data, data.StaffId, held.Id, staff)).Status);
        Assert.Equal(ReservationStatus.Forbidden, (await CancelAsync(data, administrator, held.Id, staff)).Status);
        Assert.Equal(ReservationStatus.NotFound, (await CancelAsync(data, outside, held.Id, staff)).Status);
        Assert.Equal(ReservationStatus.Invalid, (await CancelAsync(data, manager, held.Id, own)).Status);
        Assert.Equal(ReservationStatus.Invalid, (await CancelAsync(data, manager, held.Id, staff with { Reason = " reason " })).Status);
        Assert.Equal(ReservationStatus.Invalid, (await CancelAsync(data, data.OwnerId, held.Id, staff)).Status);
        Assert.False(await database.ReservationReleases.AnyAsync());
        await database.Users.Where(item => item.Id == data.OwnerId)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.EmailConfirmed, false));
        var retries = await Task.WhenAll(CancelAsync(data, data.OwnerId, held.Id, own), CancelAsync(data, data.OwnerId, held.Id, own));
        Assert.All(retries, result => Assert.Equal(ReservationStatus.Succeeded, result.Status));
        Assert.Equal(retries[0].Release!.Id, retries[1].Release!.Id);
        Assert.Equal(ReservationReleaseKind.CustomerCancelled, retries[0].Release!.Kind);
        Assert.Equal(held.Deposit, retries[0].Release!.Refund);
        Assert.Equal(1, await database.ReservationReleases.CountAsync());
        await database.Users.Where(item => item.Id == data.OwnerId)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.EmailConfirmed, true));

        var next = await HoldAsync(data, listing.Publication);
        Assert.Equal(ReservationStatus.Conflict, (await CancelAsync(data, data.OwnerId, next.Id, own)).Status);
        Assert.Equal(ReservationStatus.Conflict, (await CancelAsync(data, data.OwnerId, next.Id,
            own with { OperationId = next.Id })).Status);
        await database.Users.Where(item => item.Id == manager)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.IsActive, false));
        Assert.Equal(ReservationStatus.Forbidden, (await CancelAsync(data, manager, next.Id, staff)).Status);
        await database.Users.Where(item => item.Id == manager)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.IsActive, true));
        var cancelled = await CancelAsync(data, manager, next.Id, staff);
        Assert.Equal(ReservationStatus.Succeeded, cancelled.Status);
        var replay = await CancelAsync(data, manager, next.Id, staff);
        Assert.Equal(cancelled.Release!.RecordedAt, replay.Release!.RecordedAt);
        Assert.Equal(cancelled.Release.Id, replay.Release.Id);
        Assert.Equal(ReservationStatus.Conflict, (await CancelAsync(data, manager, next.Id,
            staff with { Reason = "Different reason" })).Status);
        Assert.Equal(ReservationStatus.Conflict, (await CancelAsync(data, generalManager, next.Id, staff)).Status);

        var third = await HoldAsync(data, listing.Publication);
        await database.Branches.Where(item => item.Id == data.BranchId)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.IsActive, false));
        Assert.Equal(ReservationStatus.Forbidden, (await CancelAsync(data, manager, third.Id, staff)).Status);
        var groupCancelled = await CancelAsync(data, generalManager, third.Id, staff with { OperationId = Guid.NewGuid() });
        Assert.Equal(ReservationStatus.Succeeded, groupCancelled.Status);
        Assert.Equal(ReservationReleaseKind.StaffCancelled, groupCancelled.Release!.Kind);
        await database.Branches.Where(item => item.Id == data.BranchId)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.IsActive, true));
        var due = await HoldAsync(data, listing.Publication);
        await using var clock = new ReservationTestClock(fixture.Database);
        await clock.InitializeAsync(due.ExpiresAt.AddSeconds(-1));
        await using var restarted = clock.CreateContext();
        var cancellation = new ReservationCancellationService(new ReservationCancellationStore(restarted));
        var expiredRequest = new ReservationCancellationRequest(Guid.NewGuid(), null);
        await using (var transaction = await new ReservationCancellationStore(restarted)
            .LockAsync(data.OwnerId, due.Id, expiredRequest.OperationId, CancellationToken.None))
        {
            var attempted = new ReservationRelease(expiredRequest.OperationId, due, ReservationReleaseKind.CustomerCancelled,
                data.OwnerId, null, transaction.Context.Now);
            await clock.AdvanceAsync(due.ExpiresAt);
            Assert.Equal(ReservationStatus.Conflict, await transaction.CommitAsync(attempted, CancellationToken.None));
        }

        var expired = await restarted.ReservationReleases.SingleAsync(item => item.ReservationId == due.Id);
        Assert.Equal(ReservationReleaseKind.Expired, expired.Kind);
        Assert.Equal(due.ExpiresAt, expired.RecordedAt);
        Assert.Equal(500m, expired.Refund);
        Assert.NotEqual(expiredRequest.OperationId, expired.Id);
        Assert.Equal(4, await restarted.ReservationReleases.CountAsync());
        Assert.False(await restarted.Reservations.AnyAsync(item => item.IsActive));
        Assert.Equal(ReservationStatus.Conflict, (await cancellation.CancelAsync(data.OwnerId, due.Id,
            expiredRequest, CancellationToken.None)).Status);
        Assert.Equal(ReservationStatus.Conflict, (await cancellation.CancelAsync(data.OwnerId, due.Id,
            expiredRequest, CancellationToken.None)).Status);
        Assert.Equal(4, await restarted.ReservationReleases.CountAsync());
        Assert.All(await restarted.Vehicles.ToListAsync(), item => Assert.Equal(VehicleStatus.Available, item.Status));
    }

    private static async Task<Reservation> HoldAsync(CheckInTestData data, ListingPublication publication)
    {
        using var scope = data.Factory.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<ReservationService>().ReserveAsync(data.OwnerId,
            publication.VehicleId, new(Guid.NewGuid(), publication.Id, null), CancellationToken.None);
        Assert.Equal(ReservationStatus.Succeeded, result.Status);
        return result.Reservation!;
    }

    private static async Task<ReservationCancellationResult> CancelAsync(CheckInTestData data, Guid actorId,
        Guid reservationId, ReservationCancellationRequest request)
    {
        using var scope = data.Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ReservationCancellationService>()
            .CancelAsync(actorId, reservationId, request, CancellationToken.None);
    }

    private DealershipDbContext NewContext() => new(new DbContextOptionsBuilder<DealershipDbContext>()
        .UseNpgsql(fixture.Database.RuntimeConnectionString).Options);
}
