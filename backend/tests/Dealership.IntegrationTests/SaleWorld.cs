using Dealership.Application.Accounts;
using Dealership.Application.Branches;
using Dealership.Application.Sales;
using Dealership.Application.Vehicles;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Dealership.IntegrationTests.ReservationTestData;

namespace Dealership.IntegrationTests;

// One provisioned branch per database: the check-in fixture can be created only once, so tests share it and
// every hold uses a fresh car.
internal sealed class SaleWorld
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<IntakePhotoFixture, Task<SaleWorld>>
        Worlds = [];

    private readonly CheckInTestData data;
    private readonly IServiceScope scope;
    private readonly Guid technician;
    private string connection = string.Empty;
    private int nextCar;

    private SaleWorld(CheckInTestData data, IServiceScope scope, Guid technician)
    {
        this.data = data;
        this.scope = scope;
        this.technician = technician;
    }

    public IServiceProvider Services => scope.ServiceProvider;

    public SaleHandlingService Handling => Services.GetRequiredService<SaleHandlingService>();

    public SaleTermsService Terms => Services.GetRequiredService<SaleTermsService>();

    public SaleSettlementService Settlement => Services.GetRequiredService<SaleSettlementService>();

    public ISaleReadStore Reads => Services.GetRequiredService<ISaleReadStore>();

    public ISaleListStore Lists => Services.GetRequiredService<ISaleListStore>();

    public Guid Manager { get; private init; }

    public Guid Agent { get; private init; }

    public Guid Colleague { get; private init; }

    public Guid OtherBranchAgent { get; private init; }

    public static Task<SaleWorld> SharedAsync(IntakePhotoFixture fixture) =>
        Worlds.GetValue(fixture, CreateAsync);

    private static async Task<SaleWorld> CreateAsync(IntakePhotoFixture fixture)
    {
        var data = await CheckInTestData.CreateAsync(fixture,
            new Dealership.Domain.Intake.InspectionSchedule(new TimeOnly(9, 0), new TimeOnly(18, 0),
                15, 30, 0, Enum.GetValues<DayOfWeek>()));
        var scope = data.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var manager = await ProvisionAsync(services, data.BranchId, AccountRoles.BranchManager);
        var technician = await ProvisionAsync(services, data.BranchId, AccountRoles.WorkshopTechnician);
        await using var database = new DealershipDbContext(new DbContextOptionsBuilder<DealershipDbContext>()
            .UseNpgsql(fixture.Database.RuntimeConnectionString).Options);
        if (!await database.PurchaseSettings.AnyAsync())
        {
            database.PurchaseSettings.Add(new(500m, 0.08m, manager, DateTimeOffset.UtcNow));
            await database.SaveChangesAsync();
        }

        var otherBranch = Guid.NewGuid();
        await services.GetRequiredService<BranchProvisioningService>().ProvisionBranchAsync(
            DevelopmentFixtureManifest.Branches[1] with { Id = otherBranch }, CancellationToken.None);
        return new SaleWorld(data, scope, technician)
        {
            connection = fixture.Database.RuntimeConnectionString,
            Manager = manager,
            Agent = await ProvisionAsync(services, data.BranchId, AccountRoles.SalesAgent),
            Colleague = await ProvisionAsync(services, data.BranchId, AccountRoles.SalesAgent),
            OtherBranchAgent = await ProvisionAsync(services, otherBranch, AccountRoles.SalesAgent)
        };
    }

    // A fresh verified customer reserves a freshly published car of the test branch.
    public async Task<Guid> TradeInAsync()
    {
        var index = Interlocked.Increment(ref nextCar);
        var listing = await CreateListingAsync(data, Services, Manager, technician, index);
        return listing.Publication.VehicleId;
    }

    public async Task<Reservation> HoldAsync(bool withPhoto = false, bool intakeOwner = false,
        bool withInspectionEvidence = false)
    {
        var index = Interlocked.Increment(ref nextCar);
        await using var database = new DealershipDbContext(new DbContextOptionsBuilder<DealershipDbContext>()
            .UseNpgsql(connection).Options);
        var customer = intakeOwner ? data.OwnerId : await RegisterCustomerAsync(Services, database);
        var listing = await CreateListingAsync(data, Services, Manager, technician, index, withPhoto, withInspectionEvidence);
        var result = await Services.GetRequiredService<ReservationService>().ReserveAsync(customer,
            listing.Publication.VehicleId, new(Guid.NewGuid(), listing.Publication.Id, null), default);
        Assert.Equal(ReservationStatus.Succeeded, result.Status);
        return result.Reservation!;
    }
}
