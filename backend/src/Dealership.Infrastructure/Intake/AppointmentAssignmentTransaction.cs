using Dealership.Application.Intake;
using Dealership.Domain.Intake;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Dealership.Infrastructure.Intake;

internal sealed class AppointmentAssignmentTransaction(DealershipDbContext database, IDbContextTransaction transaction,
    AppointmentAssignmentContext context) : IAppointmentAssignmentTransaction
{
    public AppointmentAssignmentContext Context => context;

    public Task<DateTimeOffset> ReadDatabaseTimeAsync(CancellationToken cancellationToken)
    {
        return database.DatabaseNowAsync(cancellationToken);
    }

    public Task<bool> HasOverlapAsync(Guid handlerId, CancellationToken cancellationToken)
    {
        var appointment = context.Appointment!;
        return StaffScheduleConflicts.HasOverlapAsync(database, handlerId, appointment.StartsAt, appointment.EndsAt,
            appointment.Id, null, cancellationToken);
    }

    public async Task CommitAsync(AppointmentAssignment assignment, CancellationToken cancellationToken)
    {
        database.AppointmentAssignments.Add(assignment);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await transaction.DisposeAsync();
        }
        finally
        {
            database.ChangeTracker.Clear();
        }
    }
}
