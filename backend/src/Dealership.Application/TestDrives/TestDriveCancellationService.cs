using Dealership.Application.Accounts;
using Dealership.Domain.Intake;
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

        if (context.Actor is not { IsActive: true, Role: AccountRoles.Customer })
        {
            return new(TestDriveCancellationStatus.Forbidden);
        }

        if (context.Existing is { } existing)
        {
            var matches = existing.Kind == TestDriveCancellationKind.CustomerCancelled &&
                existing.CustomerId == customerId && existing.BookingId == bookingId;
            return matches ? new(TestDriveCancellationStatus.Succeeded, existing) : new(TestDriveCancellationStatus.Conflict);
        }

        var booking = context.Booking;
        if (booking is null || booking.CustomerId != customerId)
        {
            return new(TestDriveCancellationStatus.NotFound);
        }

        if (context.Cancelled || context.Started || context.Completed)
        {
            return new(TestDriveCancellationStatus.Conflict);
        }

        if (context.Now < booking.CreatedAt || context.Now >= booking.StartsAt)
        {
            return new(TestDriveCancellationStatus.TooLate);
        }

        return await transaction.CommitAsync(new(operationId, booking, customerId, context.Now), cancellationToken);
    }

    public async Task<TestDriveCancellationResult> CancelByStaffAsync(Guid actorId, Guid bookingId,
        Guid operationId, string reason, CancellationToken cancellationToken)
    {
        if (actorId == Guid.Empty || bookingId == Guid.Empty || operationId == Guid.Empty)
        {
            return new(TestDriveCancellationStatus.Invalid);
        }

        try
        {
            reason = TestDriveCancellation.NormalizeReason(reason);
        }
        catch (ArgumentException)
        {
            return new(TestDriveCancellationStatus.Invalid);
        }

        await using var transaction = await cancellations.LockAsync(actorId, bookingId, operationId, cancellationToken);
        var context = transaction.Context;
        var actor = context.Actor;
        if (context.Status != TestDriveCancellationStatus.Ready)
        {
            return new(context.Status);
        }

        if (actor is not { IsActive: true, BranchId: not null } || !context.BranchActive ||
            actor.Role is not (AccountRoles.SalesAgent or AccountRoles.BranchManager))
        {
            return new(TestDriveCancellationStatus.Forbidden);
        }

        var existing = context.Existing;
        if (existing is not null && (existing.Kind != TestDriveCancellationKind.StaffCancelled || existing.StaffId != actorId ||
            existing.BookingId != bookingId || existing.Reason != reason))
        {
            return new(TestDriveCancellationStatus.Conflict);
        }

        var booking = context.Booking;
        if (booking is null || booking.BranchId != actor.BranchId)
        {
            return new(TestDriveCancellationStatus.NotFound);
        }

        if (existing is not null)
        {
            return new(TestDriveCancellationStatus.Succeeded, existing);
        }

        if (actor.Role == AccountRoles.SalesAgent && context.Current?.HandlerId != actorId)
        {
            return new(TestDriveCancellationStatus.Forbidden);
        }

        if (context.Cancelled || context.Started || context.Completed)
        {
            return new(TestDriveCancellationStatus.Conflict);
        }

        if (context.Now < booking.CreatedAt || context.Now >= AppointmentDay.EndsAt(booking.StartsAt))
        {
            return new(TestDriveCancellationStatus.TooLate);
        }

        var cancellation = TestDriveCancellation.ByStaff(operationId, booking, actorId, reason, context.Now);
        return await transaction.CommitAsync(cancellation, cancellationToken);
    }
}
