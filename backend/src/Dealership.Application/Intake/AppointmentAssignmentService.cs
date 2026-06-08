using Dealership.Domain.Common;
using Dealership.Application.Accounts;
using Dealership.Domain.Intake;

namespace Dealership.Application.Intake;

public sealed class AppointmentAssignmentService(IAppointmentAssignmentStore assignments)
{
    public async Task<AppointmentAssignmentResult> ExecuteAsync(Guid actorId, Guid appointmentId, string kind,
        AppointmentAssignmentRequest request, CancellationToken cancellationToken)
    {
        if (actorId == Guid.Empty || appointmentId == Guid.Empty || request.OperationId == Guid.Empty ||
            request.ExpectedVersion is < 0 or long.MaxValue || request.HandlerId == Guid.Empty)
        {
            return new(AppointmentAssignmentStatus.Invalid);
        }

        try
        {
            request = request with { Reason = AppointmentAssignment.NormalizeReason(request.Reason) };
        }
        catch (ArgumentException)
        {
            return new(AppointmentAssignmentStatus.Invalid);
        }

        var handlerId = kind == AppointmentAssignmentKind.Claimed ? actorId : request.HandlerId;
        if (kind is not (AppointmentAssignmentKind.Claimed or AppointmentAssignmentKind.Assigned or AppointmentAssignmentKind.Released) ||
            kind == AppointmentAssignmentKind.Claimed && (request.HandlerId is not null || request.Reason is not null) ||
            kind == AppointmentAssignmentKind.Assigned && handlerId is null ||
            kind == AppointmentAssignmentKind.Released && handlerId is not null)
        {
            return new(AppointmentAssignmentStatus.Invalid);
        }

        await using var transaction = await assignments.LockAsync(actorId, appointmentId,
            request with { HandlerId = handlerId }, cancellationToken);
        var context = transaction.Context;
        var actor = context.Actor;
        if (actor is not { IsActive: true, BranchId: not null } || !context.BranchActive ||
            actor.Role is not (AccountRoles.SalesAgent or AccountRoles.BranchManager) ||
            kind == AppointmentAssignmentKind.Assigned && actor.Role != AccountRoles.BranchManager)
        {
            return new(AppointmentAssignmentStatus.Forbidden);
        }

        var appointment = context.Appointment;
        if (appointment is null || appointment.BranchId != actor.BranchId)
        {
            return new(AppointmentAssignmentStatus.NotFound);
        }

        var existing = context.Existing;
        if (existing is not null)
        {
            var matches = existing.ActorId == actorId && existing.AppointmentId == appointmentId &&
                existing.Kind == kind && existing.HandlerId == handlerId &&
                existing.ExpectedVersion == request.ExpectedVersion && existing.Reason == request.Reason;
            return new(matches ? AppointmentAssignmentStatus.Succeeded : AppointmentAssignmentStatus.Conflict,
                matches ? existing : null, context.Current);
        }

        var current = context.Current;
        if ((current?.Version ?? 0) != request.ExpectedVersion)
        {
            return new(AppointmentAssignmentStatus.Conflict, Current: current);
        }

        if (kind == AppointmentAssignmentKind.Released && actor.Role == AccountRoles.SalesAgent &&
            current?.HandlerId != actorId)
        {
            return new(AppointmentAssignmentStatus.Forbidden);
        }

        var now = await transaction.ReadDatabaseTimeAsync(cancellationToken);
        if (context.CheckedIn || appointment.CancelledAt is not null || now >= AppointmentDay.EndsAt(appointment.StartsAt))
        {
            return new(AppointmentAssignmentStatus.Unavailable, Current: current);
        }

        if (handlerId is not null && (context.Handler is not { IsActive: true } handler ||
            handler.BranchId != appointment.BranchId ||
            handler.Role is not (AccountRoles.SalesAgent or AccountRoles.BranchManager)))
        {
            return new(AppointmentAssignmentStatus.Unavailable, Current: current);
        }

        AppointmentAssignment assignment;
        try
        {
            assignment = new AppointmentAssignment(request.OperationId, appointment, actorId, kind,
                handlerId, current, request.Reason, now);
        }
        catch (ArgumentException)
        {
            return new(AppointmentAssignmentStatus.Invalid, Current: current);
        }

        if (handlerId is not null && await transaction.HasOverlapAsync(handlerId.Value, cancellationToken))
        {
            return new(AppointmentAssignmentStatus.ScheduleConflict, Current: current);
        }

        await transaction.CommitAsync(assignment, cancellationToken);
        return new(AppointmentAssignmentStatus.Succeeded, assignment, assignment);
    }
}
