using Dealership.Domain.TestDrives;

namespace Dealership.Application.TestDrives;

public sealed class TestDriveCancellationService(ITestDriveCancellationStore cancellations)
{
    public async Task<TestDriveCancellationResult> CancelAsync(Guid customerId, Guid bookingId, Guid operationId,
        CancellationToken cancellationToken)
    {
        if (customerId == Guid.Empty || bookingId == Guid.Empty || operationId == Guid.Empty)
        {
            return new(TestDriveCancellationStatus.Invalid);
        }

        await using var transaction = await cancellations.LockAsync(customerId, bookingId, operationId, cancellationToken);
        var context = transaction.Context;
        if (context.Status != TestDriveCancellationStatus.Ready)
        {
            return new(context.Status);
        }

        if (context.Existing is { } existing)
        {
            var matches = existing.CustomerId == customerId && existing.BookingId == bookingId;
            return matches ? new(TestDriveCancellationStatus.Succeeded, existing) : new(TestDriveCancellationStatus.Conflict);
        }

        var booking = context.Booking;
        if (booking is null || booking.CustomerId != customerId)
        {
            return new(TestDriveCancellationStatus.NotFound);
        }

        if (context.Cancelled)
        {
            return new(TestDriveCancellationStatus.Conflict);
        }

        if (context.Now < booking.CreatedAt || context.Now >= booking.StartsAt)
        {
            return new(TestDriveCancellationStatus.TooLate);
        }

        return await transaction.CommitAsync(new(operationId, booking, customerId, context.Now), cancellationToken);
    }
}
