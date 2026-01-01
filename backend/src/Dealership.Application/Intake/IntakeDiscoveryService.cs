using System.Globalization;
using Dealership.Domain.Intake;

namespace Dealership.Application.Intake;

public sealed class IntakeDiscoveryService(IPublicBranchStore branches, IIntakeBookingStore bookings,
    InspectionSchedule schedule, TimeProvider clock)
{
    public const int DefaultPageSize = 20;
    public const int MaximumPageSize = 50;

    public async Task<PublicBranchPage?> GetBranchesAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        if (page < 1 || pageSize is < 1 or > MaximumPageSize)
        {
            return null;
        }

        return await branches.GetActiveAsync(page, pageSize, cancellationToken);
    }

    public async Task<SlotDiscoveryResult> GetSlotsAsync(Guid branchId, string? date, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var window = schedule.GetDateWindow(now);

        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None,
            out var requestedDate) || requestedDate < window.FirstDate || requestedDate > window.LastDate)
        {
            return new SlotDiscoveryResult(SlotDiscoveryStatus.InvalidDate);
        }

        if (!await branches.IsActiveAsync(branchId, cancellationToken))
        {
            return new SlotDiscoveryResult(SlotDiscoveryStatus.BranchUnavailable);
        }

        var slots = schedule.GetSlots(requestedDate, now);

        if (slots.Count > 0)
        {
            var occupied = await bookings.GetOccupiedSlotsAsync(branchId, slots[0].StartsAt, slots[^1].EndsAt, cancellationToken);
            slots = slots.Where(slot => !occupied.Any(booked => booked.StartsAt < slot.EndsAt && slot.StartsAt < booked.EndsAt)).ToArray();
        }

        var offer = new InspectionSlotOffer(branchId, requestedDate, window, slots);
        return new SlotDiscoveryResult(SlotDiscoveryStatus.Succeeded, offer);
    }
}
