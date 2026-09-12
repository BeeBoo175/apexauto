using Dealership.Application.Accounts;
using Dealership.Application.Intake;
using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Intake;

public sealed class AppointmentCheckInStore(DealershipDbContext database, MailDeliverySignal mail) : IAppointmentCheckInStore
{
    public async Task<IAppointmentCheckInTransaction> LockAsync(Guid staffId, Guid appointmentId,
        CancellationToken cancellationToken)
    {
        var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({"appointment:" + appointmentId}, 0))", cancellationToken);
            var appointment = await database.IntakeAppointments.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == appointmentId, cancellationToken);
            var ownerId = appointment?.OwnerId ?? staffId;
            var users = await database.Users.FromSqlInterpolated($"""
                SELECT * FROM "AspNetUsers" WHERE "Id" IN ({staffId}, {ownerId}) ORDER BY "Id" FOR SHARE
                """).AsNoTracking().ToArrayAsync(cancellationToken);
            var staff = users.SingleOrDefault(user => user.Id == staffId);
            var customer = users.SingleOrDefault(user => user.Id == ownerId);
            var branchActive = false;

            if (staff?.BranchId is not null)
            {
                branchActive = await database.Branches.FromSqlInterpolated(
                    $"SELECT * FROM \"Branches\" WHERE \"Id\" = {staff.BranchId} FOR UPDATE")
                    .AnyAsync(branch => branch.IsActive, cancellationToken);
            }

            var existing = await database.AppointmentCheckIns.AsNoTracking()
                .SingleOrDefaultAsync(item => item.AppointmentId == appointmentId, cancellationToken);
            var handlerId = await database.AppointmentAssignments.AsNoTracking()
                .Where(item => item.AppointmentId == appointmentId).OrderByDescending(item => item.Version)
                .Select(item => item.HandlerId).FirstOrDefaultAsync(cancellationToken);
            var context = new CheckInContext(appointment,
                staff is null ? null : new BranchStaff(staff.IsActive, staff.Role, staff.BranchId),
                customer is null ? null : new CheckInCustomer(customer.IsActive, customer.EmailConfirmed, customer.Role,
                    customer.ClosedAt is not null),
                branchActive, existing, handlerId);
            return new AppointmentCheckInTransaction(database, transaction, context, appointmentId, customer?.Email, mail);
        }
        catch
        {
            try
            {
                await transaction.DisposeAsync();
            }
            finally
            {
                database.ChangeTracker.Clear();
            }

            throw;
        }
    }
}
