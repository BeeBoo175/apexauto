using Dealership.Domain.Intake;
using Dealership.Application.Accounts;
using Dealership.Domain.TestDrives;

namespace Dealership.Application.TestDrives;

public sealed class TestDriveAssignmentService(ITestDriveAssignmentStore assignments)
{
    public async Task<TestDriveAssignmentResult> ExecuteAsync(Guid actorId, Guid bookingId, string kind,
        TestDriveAssignmentRequest request, CancellationToken cancellationToken)
    {
        if (actorId == Guid.Empty || bookingId == Guid.Empty || request.OperationId == Guid.Empty ||
            request.ExpectedVersion is < 0 or long.MaxValue || request.HandlerId == Guid.Empty)
        {
            return new(TestDriveAssignmentStatus.Invalid);
        }

        try
        {
            request = request with { Reason = TestDriveAssignment.NormalizeReason(request.Reason) };
        }
        catch (ArgumentException)
        {
            return new(TestDriveAssignmentStatus.Invalid);
        }

        var handlerId = kind == TestDriveAssignmentKind.Claimed ? actorId : request.HandlerId;
        if (kind is not (TestDriveAssignmentKind.Claimed or TestDriveAssignmentKind.Assigned or TestDriveAssignmentKind.Released) ||
            kind == TestDriveAssignmentKind.Claimed && (request.HandlerId is not null || request.Reason is not null) ||
            kind == TestDriveAssignmentKind.Assigned && handlerId is null ||
            kind == TestDriveAssignmentKind.Released && handlerId is not null)
        {
            return new(TestDriveAssignmentStatus.Invalid);
        }

        await using var transaction = await assignments.LockAsync(actorId, bookingId,
            request with { HandlerId = handlerId }, cancellationToken);
        var context = transaction.Context;
        var actor = context.Actor;
        if (actor is not { IsActive: true, BranchId: not null } || !context.BranchActive ||
            actor.Role is not (AccountRoles.SalesAgent or AccountRoles.BranchManager) ||
            kind == TestDriveAssignmentKind.Assigned && actor.Role != AccountRoles.BranchManager)
        {
            return new(TestDriveAssignmentStatus.Forbidden);
        }

        var booking = context.Booking;
        if (booking is null || booking.BranchId != actor.BranchId)
        {
            return new(TestDriveAssignmentStatus.NotFound);
        }

        var existing = context.Existing;
        if (existing is not null)
        {
            var matches = existing.ActorId == actorId && existing.BookingId == bookingId &&
                existing.Kind == kind && existing.HandlerId == handlerId &&
                existing.ExpectedVersion == request.ExpectedVersion && existing.Reason == request.Reason;
            return new(matches ? TestDriveAssignmentStatus.Succeeded : TestDriveAssignmentStatus.Conflict,
                matches ? existing : null, context.Current);
        }

        var current = context.Current;
        if ((current?.Version ?? 0) != request.ExpectedVersion)
        {
            return new(TestDriveAssignmentStatus.Conflict, Current: current);
        }

        if (kind == TestDriveAssignmentKind.Released && actor.Role == AccountRoles.SalesAgent &&
            current?.HandlerId != actorId)
        {
            return new(TestDriveAssignmentStatus.Forbidden);
        }

        var now = await transaction.ReadDatabaseTimeAsync(cancellationToken);
        if (context.Cancelled || now >= AppointmentDay.EndsAt(booking.StartsAt))
        {
            return new(TestDriveAssignmentStatus.Unavailable, Current: current);
        }

        if (handlerId is not null && (context.Handler is not { IsActive: true } handler ||
            handler.BranchId != booking.BranchId ||
            handler.Role is not (AccountRoles.SalesAgent or AccountRoles.BranchManager)))
        {
            return new(TestDriveAssignmentStatus.Unavailable, Current: current);
        }

        TestDriveAssignment assignment;
        try
        {
            assignment = new TestDriveAssignment(request.OperationId, booking, actorId, kind,
                handlerId, current, request.Reason, now);
        }
        catch (ArgumentException)
        {
            return new(TestDriveAssignmentStatus.Invalid, Current: current);
        }

        if (handlerId is not null && await transaction.HasOverlapAsync(handlerId.Value, cancellationToken))
        {
            return new(TestDriveAssignmentStatus.ScheduleConflict, Current: current);
        }

        await transaction.CommitAsync(assignment, cancellationToken);
        return new(TestDriveAssignmentStatus.Succeeded, assignment, assignment);
    }
}
