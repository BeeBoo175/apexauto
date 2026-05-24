using Dealership.Application.Intake;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Intake;

public sealed class AppointmentAssignmentStore(DealershipDbContext database) : IAppointmentAssignmentStore
{
    public async Task<IAppointmentAssignmentTransaction> LockAsync(Guid actorId, Guid appointmentId,
        AppointmentAssignmentRequest request, CancellationToken cancellationToken)
    {
        var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({"intake-assignment:" + request.OperationId}, 0))", cancellationToken);
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({"appointment:" + appointmentId}, 0))", cancellationToken);
            var users = await database.Users.FromSqlInterpolated($"""
                SELECT * FROM "AspNetUsers" WHERE "Id" = {actorId} OR "Id" = {request.HandlerId}
                ORDER BY "Id" FOR SHARE
                """).AsNoTracking().ToArrayAsync(cancellationToken);
            var actor = users.SingleOrDefault(user => user.Id == actorId);
            var handler = users.SingleOrDefault(user => user.Id == request.HandlerId);
            var branchActive = false;
            if (actor?.BranchId is not null)
            {
                branchActive = await database.Branches.FromSqlInterpolated(
                    $"SELECT * FROM \"Branches\" WHERE \"Id\" = {actor.BranchId} FOR UPDATE")
                    .AnyAsync(branch => branch.IsActive, cancellationToken);
            }

            var appointment = await database.IntakeAppointments.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == appointmentId, cancellationToken);
            var existing = await database.AppointmentAssignments.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == request.OperationId, cancellationToken);
            var current = await database.AppointmentAssignments.AsNoTracking()
                .Where(item => item.AppointmentId == appointmentId).OrderByDescending(item => item.Version)
                .FirstOrDefaultAsync(cancellationToken);
            var checkedIn = await database.AppointmentCheckIns.AnyAsync(item => item.AppointmentId == appointmentId, cancellationToken);
            var context = new AppointmentAssignmentContext(appointment,
                actor is null ? null : new CheckInStaff(actor.IsActive, actor.Role, actor.BranchId),
                handler is null ? null : new CheckInStaff(handler.IsActive, handler.Role, handler.BranchId),
                branchActive, checkedIn, existing, current);
            return new AppointmentAssignmentTransaction(database, transaction, context);
        }
        catch
        {
            await transaction.DisposeAsync();
            database.ChangeTracker.Clear();
            throw;
        }
    }
}
