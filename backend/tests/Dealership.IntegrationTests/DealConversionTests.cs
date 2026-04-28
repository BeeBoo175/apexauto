using Dealership.Application.Accounts;
using Dealership.Application.Sales;
using Dealership.Application.Vehicles;
using Dealership.Domain.Inspections;
using Dealership.Domain.Sales;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Dealership.IntegrationTests.ReservationTestData;

namespace Dealership.IntegrationTests;

public sealed class DealConversionTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{

    [Fact]
    public async Task ReservationsWarrantiesAndTradeInsAreBoundToTheirOwnersAndTheVehicleGrade()
    {
        using var data = await CheckInTestData.CreateAsync(fixture);
        using var scope = data.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var manager = await ProvisionAsync(services, data.BranchId, AccountRoles.BranchManager);
        var technician = await ProvisionAsync(services, data.BranchId, AccountRoles.WorkshopTechnician);
        var agent = await ProvisionAsync(services, data.BranchId, AccountRoles.SalesAgent);
        var listing = await CreateListingAsync(data, services, manager, technician, 0);
        await using var database = NewContext();
        database.PurchaseSettings.Add(new(500m, 0.08m, manager, DateTimeOffset.UtcNow));
        await database.SaveChangesAsync();
        var vehicleId = listing.Publication.VehicleId;
        var deals = services.GetRequiredService<DealService>();
        var held = await HoldAsync(data, listing.Publication);

        // Only the vehicle's active hold can start a deal, and the holder's terms are carried in.
        Assert.Equal(DealStatus.NotFound, (await deals.ClaimAsync(agent, Guid.NewGuid(), new(Guid.NewGuid()), default)).Status);
        var created = await deals.ClaimAsync(agent, held.Id, new(Guid.NewGuid()), default);
        Assert.Equal(DealStatus.Succeeded, created.Status);
        var deal = created.Deal!;
        Assert.Equal((data.OwnerId, held.Id, 500m), (deal.CustomerId, deal.ReservationId, deal.DepositCredit));
        Assert.Equal(deal.BasePrice - deal.PromoDiscount + deal.ProcessingFee + deal.RoadTax + deal.ServiceTax - 500m,
            deal.BalancePayable);

        var silver = new WarrantyPackage(Guid.NewGuid(), "Silver test", 12, 25000, 1200m, InspectionGrade.C, true);
        var retired = new WarrantyPackage(Guid.NewGuid(), "Retired test", 12, 25000, 900m, InspectionGrade.C, false);
        database.WarrantyPackages.AddRange(silver, retired);
        await database.SaveChangesAsync();
        var revise = new DealReviseRequest(1, silver.Id, 0m, null, 0m);
        Assert.Equal(DealStatus.Invalid, (await deals.ReviseAsync(agent, deal.Id, revise with { WarrantyPackageId = retired.Id }, default)).Status);
        Assert.Equal(DealStatus.Invalid, (await deals.ReviseAsync(agent, deal.Id, revise with { WarrantyPackageId = Guid.NewGuid() }, default)).Status);
        var withWarranty = await deals.ReviseAsync(agent, deal.Id, revise, default);
        Assert.Equal(DealStatus.Succeeded, withWarranty.Status);
        Assert.Equal(("Silver test", 1200m), (withWarranty.Deal!.WarrantyName, withWarranty.Deal.WarrantyPrice));

        var appointment = await database.IntakeAppointments.AsNoTracking().FirstAsync(item => item.OwnerId == data.OwnerId);
        var maximum = appointment.Valuation.Maximum;
        var tradeIn = revise with { ExpectedVersion = 2, TradeInAppointmentId = appointment.Id, TradeInAllowance = maximum };
        Assert.Equal(DealStatus.Invalid, (await deals.ReviseAsync(agent, deal.Id, tradeIn with { TradeInAllowance = maximum + 0.01m }, default)).Status);
        Assert.Equal(DealStatus.Invalid, (await deals.ReviseAsync(agent, deal.Id, tradeIn with { TradeInAppointmentId = Guid.NewGuid() }, default)).Status);
        var withTradeIn = await deals.ReviseAsync(agent, deal.Id, tradeIn with { TradeInAllowance = Math.Min(maximum, 1000m) }, default);
        Assert.Equal((DealStatus.Succeeded, appointment.Id), (withTradeIn.Status, withTradeIn.Deal!.TradeInAppointmentId));
        Assert.True(withTradeIn.Deal.BalancePayable >= 0m);

        var options = (await deals.OptionsAsync(agent, deal.Id, default)).Options!;
        Assert.Contains(silver.Id, options.Warranties.Select(item => item.Id));
        Assert.DoesNotContain(retired.Id, options.Warranties.Select(item => item.Id));
        Assert.Equal(maximum, options.TradeIns.Single(item => item.AppointmentId == appointment.Id).Maximum);

        var submitted = await deals.TransitionAsync(agent, deal.Id, DealAction.Submit, new(3, null), default);
        Assert.Equal("Approved", submitted.Deal!.Status);
        // The captured price still satisfies the database identity after the same terms are stored.
        var stored = await database.SalesOrders.AsNoTracking().SingleAsync(item => item.Id == deal.Id);
        Assert.Equal(deal.BasePrice + stored.ProcessingFee + stored.RoadTax + stored.ServiceTax + 1200m - stored.TradeInAllowance - 500m,
            stored.BalancePayable);
    }

    private static async Task<Reservation> HoldAsync(CheckInTestData data, ListingPublication publication)
    {
        using var scope = data.Factory.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<ReservationService>().ReserveAsync(data.OwnerId,
            publication.VehicleId, new(Guid.NewGuid(), publication.Id, null), CancellationToken.None);
        Assert.Equal(ReservationStatus.Succeeded, result.Status);
        return result.Reservation!;
    }

    private DealershipDbContext NewContext() => new(new DbContextOptionsBuilder<DealershipDbContext>()
        .UseNpgsql(fixture.Database.RuntimeConnectionString).Options);
}
