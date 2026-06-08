using Dealership.Domain.Common;
using System.Data;
using Dealership.Application.Intake;
using Dealership.Domain.Intake;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Intake;

public sealed class AppointmentHistoryStore(DealershipDbContext database) : IAppointmentHistoryStore
{
    public async Task<AppointmentHistoryPage?> ListAsync(Guid ownerId, AppointmentHistoryQuery query,
        CancellationToken cancellationToken)
    {
        // Count and page describe one database snapshot, including current customer eligibility.
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);

        if (!await VerifiedCustomerQuery.ForOwner(database, ownerId, lockUser: false).AnyAsync(cancellationToken))
        {
            return null;
        }

        var databaseNow = await database.DatabaseNowAsync(cancellationToken);
        var today = AppointmentDay.StartUtc(databaseNow);
        var appointments = database.IntakeAppointments.AsNoTracking().Where(item => item.OwnerId == ownerId);

        if (query.From is not null)
        {
            appointments = appointments.Where(item => item.StartsAt >= query.From && item.StartsAt < query.Until);
        }

        var total = await appointments.CountAsync(cancellationToken);
        var items = await appointments.Join(database.Branches, item => item.BranchId, branch => branch.Id,
                    (item, branch) => new { Appointment = item, BranchName = branch.Name })
                .OrderByDescending(row => row.Appointment.StartsAt).ThenByDescending(row => row.Appointment.Id)
                .Select(row => new AppointmentHistoryItem(row.Appointment.Id, row.Appointment.BranchId, row.BranchName,
                    row.Appointment.RegistrationPlate, row.Appointment.StartsAt, row.Appointment.EndsAt, row.Appointment.CreatedAt,
                    row.Appointment.CancelledAt,
                    row.Appointment.StartsAt < today &&
                        row.Appointment.CancelledAt == null &&
                        !database.AppointmentCheckIns.Any(item => item.AppointmentId == row.Appointment.Id)
                            ? (DateTimeOffset?)row.Appointment.StartsAt : null,
                    database.AppointmentCheckIns.Where(item => item.AppointmentId == row.Appointment.Id)
                        .Select(item => (DateTimeOffset?)item.CheckedInAt).SingleOrDefault()))
                .ReadPageAsync(query.Page, query.PageSize, total, cancellationToken);
        items = [.. items.Select(item => item.NoShowAt is null ? item :
            item with { NoShowAt = AppointmentDay.EndsAt(item.StartsAt) })];
        await transaction.CommitAsync(cancellationToken);
        return new AppointmentHistoryPage(items, total, query.Page, query.PageSize);
    }
}
