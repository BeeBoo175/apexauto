using Dealership.Application.Vehicles;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Vehicles;
using Microsoft.EntityFrameworkCore;
using static Dealership.IntegrationTests.ReservationTestData;

namespace Dealership.IntegrationTests;

internal static class ReservationStaffReadAssertions
{
    internal static async Task VerifyAsync(ReservationTestClock clock, Guid manager, Guid outsider,
        IReadOnlyList<ListingRecord> listings)
    {
        var holds = new List<Reservation>();
        foreach (var listing in listings)
        {
            var customer = await clock.Maintenance.Reservations.Where(item => item.VehicleId == listing.Publication.VehicleId)
                .OrderByDescending(item => item.CreatedAt).Select(item => item.CustomerId).FirstAsync();
            await using var writer = clock.CreateContext();
            var held = await new ReservationService(new ReservationStore(writer, new MailDeliverySignal(), new ReservationExpirySignal()))
                .ReserveAsync(customer, listing.Publication.VehicleId,
                    new(Guid.NewGuid(), listing.Publication.Id, null), CancellationToken.None);
            Assert.Equal(ReservationStatus.Succeeded, held.Status);
            holds.Add(held.Reservation!);
        }

        Assert.Equal(11, holds.Count);
        var branchId = holds[0].BranchId;
        var ids = holds.Select(item => item.Id).ToArray();
        var expected = await clock.Maintenance.Reservations.Where(item => ids.Contains(item.Id))
            .OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id).Select(item => item.Id).ToArrayAsync();
        var historyCount = await clock.Maintenance.Reservations.CountAsync(item => item.BranchId == branchId && !item.IsActive);
        await clock.AdvanceAsync(holds[0].ExpiresAt.AddTicks(-10));
        await using (var before = clock.CreateContext())
        {
            var reads = new ReservationReadStore(before);
            var first = (await reads.StaffAsync(manager, new(PageSize: 10, IsActive: true), CancellationToken.None)).History!;
            var second = (await reads.StaffAsync(manager, new(Page: 2, PageSize: 10, IsActive: true), CancellationToken.None)).History!;
            Assert.Equal(11, first.Total);
            Assert.Equal(11, second.Total);
            Assert.Equal(expected, first.Items.Concat(second.Items).Select(item => item.Receipt.Id));
            Assert.False(await before.ReservationReleases.AnyAsync(item => ids.Contains(item.ReservationId)));
        }

        await clock.AdvanceAsync(holds[0].ExpiresAt);
        await using (var reader = clock.CreateContext())
        {
            var reads = new ReservationReadStore(reader);
            var denied = await reads.StaffAsync(outsider, new(branchId, IsActive: false), CancellationToken.None);
            Assert.Equal(ReservationStatus.Forbidden, denied.Status);
            Assert.False(await reader.ReservationReleases.AnyAsync(item => ids.Contains(item.ReservationId)));
            var incomplete = await reads.StaffAsync(manager, new(Page: int.MaxValue, IsActive: false), CancellationToken.None);
            Assert.Equal(ReservationStatus.Unavailable, incomplete.Status);
            Assert.Null(incomplete.History);
            var ordered = await reader.Reservations.Where(item => ids.Contains(item.Id))
                .OrderBy(item => item.VehicleId).ToArrayAsync();
            Assert.All(ordered.Take(10), item => Assert.False(item.IsActive));
            Assert.True(ordered[^1].IsActive);
        }

        // Fresh contexts prove committed partial progress, concurrent catch-up and stable final membership.
        await using var activeReader = clock.CreateContext();
        await using var historyReader = clock.CreateContext();
        var completed = await Task.WhenAll(
            new ReservationReadStore(activeReader).StaffAsync(manager, new(IsActive: true), CancellationToken.None),
            new ReservationReadStore(historyReader).StaffAsync(manager, new(IsActive: false), CancellationToken.None));
        Assert.All(completed, result => Assert.Equal(ReservationStatus.Succeeded, result.Status));
        Assert.Equal(0, completed[0].History!.Total);
        Assert.Empty(completed[0].History!.Items);
        Assert.Equal(historyCount + 11, completed[1].History!.Total);
        Assert.Equal(expected, completed[1].History!.Items.Take(11).Select(item => item.Receipt.Id));
        var refunds = await historyReader.ReservationReleases.Where(item => ids.Contains(item.ReservationId)).ToArrayAsync();
        Assert.Equal(11, refunds.Length);
        Assert.All(refunds, item =>
        {
            Assert.Equal(ReservationReleaseKind.Expired, item.Kind);
            Assert.Equal(500m, item.Refund);
            Assert.Equal(holds[0].ExpiresAt, item.RecordedAt);
        });
    }
}
