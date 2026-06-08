using System.Data;
using Dealership.Application.WorkCalendar;
using Dealership.Domain.Common;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.WorkCalendar;

public sealed class WorkCalendarStore(DealershipDbContext database) : IWorkCalendarStore
{

    public async Task<WorkCalendarReadResult<WorkCalendarSnapshot>> ScheduledAsync(Guid actorId,
        WorkCalendarQuery query, CancellationToken cancellationToken)
    {
        if (!query.IsValid)
        {
            return new(WorkCalendarReadStatus.Invalid);
        }

        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var scope = await WorkCalendarReadScope.ResolveAsync(database, actorId, query.Scope, cancellationToken);
        if (scope is null)
        {
            return new(WorkCalendarReadStatus.Forbidden);
        }

        var items = await WorkCalendarScheduledRows.ReadAsync(database, scope, query, cancellationToken);
        if (items is null)
        {
            return new(WorkCalendarReadStatus.TooMany);
        }

        var from = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(query.Dates.From!.Value, DealershipLocale.Malaysia).DateTime);
        var to = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(query.Dates.Until!.Value, DealershipLocale.Malaysia).DateTime).AddDays(-1);
        await transaction.CommitAsync(cancellationToken);
        return new(WorkCalendarReadStatus.Succeeded,
            new(scope.CurrentActor, items, items.Length, from, to, scope.BranchId, scope.Employee));
    }

    public async Task<WorkCalendarReadResult<UnscheduledWorkPage>> UnscheduledAsync(Guid actorId,
        UnscheduledWorkQuery query, CancellationToken cancellationToken)
    {
        if (!query.IsValid)
        {
            return new(WorkCalendarReadStatus.Invalid);
        }

        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var scope = await WorkCalendarReadScope.ResolveAsync(database, actorId, query.Scope, cancellationToken);
        if (scope is null)
        {
            return new(WorkCalendarReadStatus.Forbidden);
        }

        var page = await UnscheduledWorkRows.ReadAsync(database, scope, query, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(WorkCalendarReadStatus.Succeeded, page);
    }
}
