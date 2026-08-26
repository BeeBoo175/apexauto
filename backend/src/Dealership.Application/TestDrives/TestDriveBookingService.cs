using Dealership.Application.Accounts;
using Dealership.Domain.Common;
using Dealership.Domain.TestDrives;

namespace Dealership.Application.TestDrives;

public sealed class TestDriveBookingService(ITestDriveBookingStore bookings, AppointmentSchedule schedule)
{
    public async Task<TestDriveBookingResult> BookAsync(Guid customerId, Guid vehicleId,
        TestDriveBookingRequest request, CancellationToken cancellationToken, AssistingStaff? assistedBy = null)
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
            // A replay is a read of the booking, so it is limited to the staff member's branch like a new booking.
            if (assistedBy is not null && !assistedBy.CanActAt(existing.BranchId))
            {
                return new(TestDriveBookingStatus.Forbidden);
            }

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

        if (assistedBy is not null && !assistedBy.CanActAt(context.BranchId))
        {
            return new(TestDriveBookingStatus.Forbidden);
        }

        var slot = schedule.FindSlot(normalized.StartsAt, context.Now, TestDriveBooking.Duration);
        if (slot is null || context.SlotOccupied)
        {
            return new(TestDriveBookingStatus.SlotUnavailable);
        }

        var booking = new TestDriveBooking(normalized.OperationId, customerId, vehicleId,
            context.BranchId, normalized.PublicationId, slot, context.Now);
        if (assistedBy is not null)
        {
            booking.RecordAssistance(assistedBy.Id);
        }

        return await transaction.CommitAsync(booking, cancellationToken);
    }
}
