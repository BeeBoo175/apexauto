using Dealership.Domain.Common;
using Dealership.Application.Accounts;
using Dealership.Domain.TestDrives;

namespace Dealership.Application.TestDrives;

public sealed class TestDriveHandlingService(ITestDriveHandlingStore handling)
{
    public async Task<TestDriveHandlingResult> StartAsync(Guid actorId, Guid bookingId,
        TestDriveStartRequest request, CancellationToken cancellationToken)
    {
        if (!ValidIdentity(actorId, bookingId, request.OperationId, request.AssignmentId) ||
            !request.CustomerPresent || !request.DrivingLicenceChecked)
        {
            return new(TestDriveHandlingStatus.Invalid);
        }

        await using var transaction = await handling.LockAsync(actorId, bookingId, request.OperationId, cancellationToken);
        var context = transaction.Context;
        var denied = CheckAccess(context);
        if (denied is not null)
        {
            return new(denied.Value);
        }

        if (context.ExistingOutcome is not null)
        {
            return new(TestDriveHandlingStatus.Conflict);
        }

        if (context.ExistingStart is { } existing)
        {
            var matches = existing.BookingId == bookingId && existing.StaffId == actorId &&
                existing.AssignmentId == request.AssignmentId && existing.CustomerPresent == request.CustomerPresent &&
                existing.DrivingLicenceChecked == request.DrivingLicenceChecked;
            return new(matches ? TestDriveHandlingStatus.Succeeded : TestDriveHandlingStatus.Conflict,
                matches ? existing : null);
        }

        if (context.Cancelled || context.Start is not null || context.Outcome is not null)
        {
            return new(TestDriveHandlingStatus.Unavailable);
        }

        if (context.Current?.HandlerId != actorId || context.Current.Id != request.AssignmentId)
        {
            return new(TestDriveHandlingStatus.Conflict);
        }

        var now = await transaction.ReadDatabaseTimeAsync(cancellationToken);
        TestDriveStart start;
        try
        {
            start = new(request.OperationId, context.Booking!, context.Current, actorId,
                request.CustomerPresent, request.DrivingLicenceChecked, now);
        }
        catch (ArgumentException)
        {
            return new(TestDriveHandlingStatus.Unavailable);
        }

        if (!await transaction.IsVehicleEligibleAsync(cancellationToken))
        {
            return new(TestDriveHandlingStatus.Unavailable);
        }

        if (await transaction.HasStartConflictAsync(actorId, now, cancellationToken))
        {
            return new(TestDriveHandlingStatus.ScheduleConflict);
        }

        await transaction.CommitStartAsync(start, cancellationToken);
        return new(TestDriveHandlingStatus.Succeeded, start);
    }

    public async Task<TestDriveHandlingResult> RecordOutcomeAsync(Guid actorId, Guid bookingId,
        TestDriveOutcomeRequest request, CancellationToken cancellationToken)
    {
        if (!ValidIdentity(actorId, bookingId, request.OperationId, request.AssignmentId) || !request.CustomerPresent)
        {
            return new(TestDriveHandlingStatus.Invalid);
        }

        try
        {
            request = request with
            {
                Reason = WorkOwnership.NormalizeReason(request.Reason),
                Notes = TestDriveOutcome.NormalizeNotes(request.Notes)
            };
        }
        catch (ArgumentException)
        {
            return new(TestDriveHandlingStatus.Invalid);
        }

        await using var transaction = await handling.LockAsync(actorId, bookingId, request.OperationId, cancellationToken);
        var context = transaction.Context;
        var denied = CheckAccess(context);
        if (denied is not null)
        {
            return new(denied.Value);
        }

        if (context.ExistingStart is not null)
        {
            return new(TestDriveHandlingStatus.Conflict);
        }

        if (context.ExistingOutcome is { } existing)
        {
            var matches = existing.BookingId == bookingId && existing.StaffId == actorId &&
                existing.AssignmentId == request.AssignmentId && existing.Kind == request.Kind &&
                existing.CustomerPresent == request.CustomerPresent && existing.Reason == request.Reason &&
                existing.Notes == request.Notes;
            return new(matches ? TestDriveHandlingStatus.Succeeded : TestDriveHandlingStatus.Conflict,
                Outcome: matches ? existing : null);
        }

        if (context.Cancelled || context.Outcome is not null)
        {
            return new(TestDriveHandlingStatus.Unavailable);
        }

        if (context.Current?.HandlerId != actorId || context.Current.Id != request.AssignmentId)
        {
            return new(TestDriveHandlingStatus.Conflict);
        }

        var now = await transaction.ReadDatabaseTimeAsync(cancellationToken);
        TestDriveOutcome outcome;
        try
        {
            outcome = new(request.OperationId, context.Booking!, context.Current, actorId, context.Start,
                request.Kind, request.CustomerPresent, request.Reason, request.Notes, now);
        }
        catch (ArgumentException)
        {
            return new(TestDriveHandlingStatus.Invalid);
        }

        await transaction.CommitOutcomeAsync(outcome, cancellationToken);
        return new(TestDriveHandlingStatus.Succeeded, context.Start, outcome);
    }

    private static bool ValidIdentity(Guid actorId, Guid bookingId, Guid operationId, Guid assignmentId)
    {
        return actorId != Guid.Empty && bookingId != Guid.Empty && operationId != Guid.Empty && assignmentId != Guid.Empty;
    }

    private static TestDriveHandlingStatus? CheckAccess(TestDriveHandlingContext context)
    {
        if (context.Actor is not { IsActive: true, BranchId: not null } actor || !context.BranchActive ||
            actor.Role is not (AccountRoles.SalesAgent or AccountRoles.BranchManager))
        {
            return TestDriveHandlingStatus.Forbidden;
        }

        if (context.Booking is null || context.Booking.BranchId != actor.BranchId)
        {
            return TestDriveHandlingStatus.NotFound;
        }

        return null;
    }
}
