using System.Globalization;
using Dealership.Application.Performance;
using Dealership.Application.Sales;
using Dealership.Domain.Common;
using Dealership.Domain.Sales;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

public sealed class PerformanceReadsTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task DeliveredSaleMovesTheHeadlineAndPipelineOfItsBranchManager()
    {
        var world = await SaleWorld.SharedAsync(fixture);
        var reads = world.Services.GetRequiredService<IPerformanceReads>();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, DealershipLocale.Malaysia).DateTime);
        var query = PerformanceQuery.Parse(today.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            today.AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), null)!;
        var before = (await reads.HeadlineAsync(world.Manager, query, default)).Value!;
        var pipelineBefore = (await reads.PipelineAsync(world.Manager, query, default)).Value!;

        var (held, request, total) = await SettledAsync(world);
        var settledGroup = (await reads.PipelineAsync(world.Manager, query, default)).Value!.Groups
            .Single(group => group.Group == PerformancePipelineGroups.SettledUndelivered);
        var settledBefore = pipelineBefore.Groups.Single(group => group.Group == PerformancePipelineGroups.SettledUndelivered);
        Assert.Equal(settledBefore.Count + 1, settledGroup.Count);
        Assert.Equal(settledBefore.AgreedTotal + total, settledGroup.AgreedTotal);

        Assert.Equal(SaleResultStatus.Succeeded, (await world.Services.GetRequiredService<SaleHandoverService>()
            .DeliverAsync(world.Agent, held.Id, request, default)).Status);
        var after = (await reads.HeadlineAsync(world.Manager, query, default)).Value!;
        Assert.Equal(before.Current.VehiclesDelivered + 1, after.Current.VehiclesDelivered);
        Assert.Equal(before.Current.DeliveredValue + total, after.Current.DeliveredValue);
        Assert.Equal(before.Current.ListedCount + 1, after.Current.ListedCount);
        Assert.NotNull(after.Current.CheckInToListedDays);
        Assert.Equal(0, after.Previous.VehiclesDelivered);
        var settledAfter = (await reads.PipelineAsync(world.Manager, query, default)).Value!.Groups
            .Single(group => group.Group == PerformancePipelineGroups.SettledUndelivered);
        Assert.Equal(settledBefore.Count, settledAfter.Count);

        // Agents and customers may not read performance at all.
        Assert.Equal(PerformanceReadStatus.Forbidden, (await reads.HeadlineAsync(world.Agent, query, default)).Status);
        Assert.Equal(PerformanceReadStatus.Forbidden, (await reads.PipelineAsync(held.CustomerId, query, default)).Status);
    }

    [Fact]
    public void PeriodsOutsideTheMalaysianCalendarLimitAreRejected()
    {
        Assert.Null(PerformanceQuery.Parse(null, null, null));
        Assert.Null(PerformanceQuery.Parse("2026-11-02", "2026-11-01", null));
        Assert.Null(PerformanceQuery.Parse("2025-01-01", "2026-01-02", null));
        Assert.Null(PerformanceQuery.Parse("2026-11-01", "2026-11-02", Guid.Empty));
        var period = PerformanceQuery.Parse("2026-11-01", "2026-11-07", null)!;
        Assert.Equal(TimeSpan.FromDays(7), period.Length);
        Assert.Equal(new DateTimeOffset(2026, 10, 31, 16, 0, 0, TimeSpan.Zero), period.From);
    }

    private async Task<(Dealership.Domain.Vehicles.Reservation Hold, SaleHandoverRequest Request, decimal Total)> SettledAsync(
        SaleWorld world)
    {
        var held = await world.HoldAsync();
        Assert.Equal(SaleResultStatus.Succeeded, (await world.Handling.ClaimAsync(world.Agent, held.Id, new(Guid.NewGuid(), 1), default)).Status);
        Assert.Equal(SaleResultStatus.Succeeded, (await world.Terms.SubmitAsync(world.Agent, held.Id, new(Guid.NewGuid(), 2), default)).Status);
        await using var database = new DealershipDbContext(new DbContextOptionsBuilder<DealershipDbContext>()
            .UseNpgsql(fixture.Database.RuntimeConnectionString).Options);
        var sale = await database.SalesOrders.AsNoTracking().SingleAsync(item => item.Id == held.Id);
        var profile = await database.CustomerProfiles.SingleAsync(item => item.UserId == sale.CustomerId);
        var vehicle = await database.Vehicles.SingleAsync(item => item.Id == sale.VehicleId);
        var review = new SaleSettlementReview(profile.LegalName, profile.Nric, profile.ResidentialAddress, SaleReviewedVehicle.Of(vehicle));
        var payment = new SalePaymentEvidence(SalePaymentMethod.Cash, sale.BalancePayable, "CASH 43880", new DateOnly(2026, 11, 2),
            null, 0m, null, false, false);
        Assert.Equal(SaleResultStatus.Succeeded, (await world.Settlement.SettleAsync(world.Agent, held.Id,
            new(Guid.NewGuid(), 3, payment, review), default)).Status);
        var total = sale.BalancePayable + sale.DepositCredit + sale.TradeInAllowance;
        return (held, new(Guid.NewGuid(), 4, new(vehicle.MileageKilometres + 100, 2, true, true, true)), total);
    }
}
