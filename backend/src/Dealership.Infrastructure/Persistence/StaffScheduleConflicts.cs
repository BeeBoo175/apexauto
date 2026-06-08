using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Persistence;

internal static class StaffScheduleConflicts
{
    internal static async Task<bool> HasOverlapAsync(DealershipDbContext database, Guid handlerId,
        DateTimeOffset startsAt, DateTimeOffset endsAt, Guid? appointmentId, Guid? bookingId,
        CancellationToken cancellationToken)
    {
        var intakeOverlap = await database.IntakeAppointments.AnyAsync(item => item.Id != appointmentId &&
            item.StartsAt < endsAt && startsAt < item.EndsAt && item.CancelledAt == null &&
            !database.AppointmentCheckIns.Any(checkIn => checkIn.AppointmentId == item.Id) &&
            database.AppointmentAssignments.Where(assignment => assignment.AppointmentId == item.Id)
                .OrderByDescending(assignment => assignment.Version).Select(assignment => assignment.HandlerId)
                .FirstOrDefault() == handlerId, cancellationToken);
        if (intakeOverlap)
        {
            return true;
        }

        return await database.TestDriveBookings.AnyAsync(item => item.Id != bookingId &&
            (item.StartsAt < endsAt && startsAt < item.EndsAt ||
                database.TestDriveOccupations.Any(occupation => occupation.BookingId == item.Id && occupation.OutcomeId == null)) &&
            !database.TestDriveCancellations.Any(cancellation => cancellation.BookingId == item.Id) &&
            !database.TestDriveOutcomes.Any(outcome => outcome.BookingId == item.Id) &&
            database.TestDriveAssignments.Where(assignment => assignment.BookingId == item.Id)
                .OrderByDescending(assignment => assignment.Version).Select(assignment => assignment.HandlerId)
                .FirstOrDefault() == handlerId, cancellationToken);
    }
}
