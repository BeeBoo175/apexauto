using Dealership.Domain.Common;
using Dealership.Domain.TestDrives;

namespace Dealership.Application.TestDrives;

public sealed class TestDriveBookingService(ITestDriveBookingStore bookings, AppointmentSchedule schedule)
{
    public async Task<TestDriveBookingResult> BookAsync(Guid customerId, Guid vehicleId,
        TestDriveBookingRequest request, CancellationToken cancellationToken)
    {
        if (customerId == Guid.Empty || vehicleId == Guid.Empty || request.OperationId == Guid.Empty ||
            request.PublicationId == Guid.Empty || request.StartsAt.UtcDateTime >= DateTime.MaxValue.AddMinutes(-30))
        {
            return new(TestDriveBookingStatus.Invalid);
        }

        var normalized = request with { StartsAt = request.StartsAt.ToUniversalTime() };
        await using var transaction = await bookings.LockAsync(customerId, vehicleId, normalized, cancellationToken);
        var context = transaction.Context;
        if (context.Status == TestDriveBookingStatus.Forbidden)
        {
            return new(context.Status);
        }

        if (context.Existing is { } existing)
        {
            var matches = existing.CustomerId == customerId && existing.VehicleId == vehicleId &&
                existing.PublicationId == normalized.PublicationId && existing.StartsAt == normalized.StartsAt;
            return matches ? new(TestDriveBookingStatus.Succeeded, existing) : new(TestDriveBookingStatus.Conflict);
        }

        if (context.Status != TestDriveBookingStatus.Ready)
        {
            return new(context.Status);
        }

        if (context.CustomerHasBooking)
        {
            return new(TestDriveBookingStatus.CustomerHasBooking);
        }

        var slot = schedule.FindSlot(normalized.StartsAt, context.Now, TestDriveBooking.Duration);
        if (slot is null || context.SlotOccupied)
        {
            return new(TestDriveBookingStatus.SlotUnavailable);
        }

        var booking = new TestDriveBooking(normalized.OperationId, customerId, vehicleId,
            context.BranchId, normalized.PublicationId, slot, context.Now);
        return await transaction.CommitAsync(booking, cancellationToken);
    }
}
