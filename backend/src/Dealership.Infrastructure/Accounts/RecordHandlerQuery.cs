using Dealership.Application.Accounts;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Accounts;

public sealed class RecordHandlerQuery(DealershipDbContext database) : IRecordHandlerQuery
{
    public async Task<bool> HandlesCustomerAsync(Guid staffId, Guid customerId, CancellationToken cancellationToken)
    {
        if (await database.SalesOrders.AsNoTracking()
                .AnyAsync(sale => sale.CustomerId == customerId && sale.AgentId == staffId, cancellationToken))
        {
            return true;
        }

        // Only the latest assignment names the current handler of an appointment or test drive.
        var handlesAppointment = await (from assignment in database.AppointmentAssignments.AsNoTracking()
                                        join appointment in database.IntakeAppointments.AsNoTracking()
                                            on assignment.AppointmentId equals appointment.Id
                                        where appointment.OwnerId == customerId && assignment.HandlerId == staffId &&
                                            assignment.Version == database.AppointmentAssignments
                                                .Where(other => other.AppointmentId == assignment.AppointmentId)
                                                .Max(other => other.Version)
                                        select assignment.Id).AnyAsync(cancellationToken);

        if (handlesAppointment)
        {
            return true;
        }

        return await (from assignment in database.TestDriveAssignments.AsNoTracking()
                      join booking in database.TestDriveBookings.AsNoTracking()
                          on assignment.BookingId equals booking.Id
                      where booking.CustomerId == customerId && assignment.HandlerId == staffId &&
                          assignment.Version == database.TestDriveAssignments
                              .Where(other => other.BookingId == assignment.BookingId)
                              .Max(other => other.Version)
                      select assignment.Id).AnyAsync(cancellationToken);
    }
}
