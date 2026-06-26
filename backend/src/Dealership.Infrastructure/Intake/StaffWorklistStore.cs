using System.Data;
using System.Text;
using Dealership.Application.Accounts;
using Dealership.Application.Intake;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Intake;

public sealed class StaffWorklistStore(DealershipDbContext database) : IStaffWorklistStore, IStaffExportWriter
{
    public Task<StaffWorklistPage?> ListAsync(Guid staffId, StaffWorklistQuery query, CancellationToken cancellationToken)
    {
        return ReadAsync(staffId, query, 0, cancellationToken);
    }

    public Task<StaffWorklistPage?> ExportAsync(Guid staffId, StaffWorklistQuery query, CancellationToken cancellationToken)
    {
        const int maximum = StaffExportLimits.ImmediateRows;
        return ReadAsync(staffId, query with { Page = 1, PageSize = maximum }, maximum, cancellationToken);
    }

    public async Task<bool> WriteAsync(CurrentActor actor, StaffWorklistQuery query, Stream destination,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var current = await database.Users.AsNoTracking().Where(user => user.Id == actor.UserId && user.IsActive)
            .Select(user => new CurrentActor(user.Id, user.Role, user.BranchId, user.EmailConfirmed)).SingleOrDefaultAsync(cancellationToken);
        if (current != actor)
        {
            return false;
        }

        var records = await StaffWorklistRows.CreateAsync(database, actor.UserId, query, cancellationToken);
        if (records is null)
        {
            return false;
        }

        await using var writer = new StreamWriter(destination, new UTF8Encoding(false), 4096, leaveOpen: true);
        await writer.WriteAsync(StaffIntakeCsv.Header.AsMemory(), cancellationToken);
        await foreach (var row in StaffWorklistRows.Order(records, query).ThenBy(row => row.Id)
            .AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            await writer.WriteAsync(StaffIntakeCsv.Row(StaffWorklistRows.ToItem(row)).AsMemory(), cancellationToken);
        }

        await writer.FlushAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private async Task<StaffWorklistPage?> ReadAsync(Guid staffId, StaffWorklistQuery query, int maximumRows,
        CancellationToken cancellationToken)
    {
        // Authorization, scoped count and rows must describe one snapshot.
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var records = await StaffWorklistRows.CreateAsync(database, staffId, query, cancellationToken);
        if (records is null)
        {
            return null;
        }

        var total = await records.CountAsync(cancellationToken);
        if (maximumRows > 0 && total > maximumRows)
        {
            return new StaffWorklistPage([], total, 1, maximumRows);
        }

        var page = await StaffWorklistRows.Order(records, query).ThenBy(row => row.Id)
            .ReadPageAsync(query.Page, query.PageSize, total, cancellationToken);
        var items = page.Select(StaffWorklistRows.ToItem).ToArray();

        var counts = maximumRows == 0
            ? await StaffWorklistRows.CountsAsync(database, staffId, query, cancellationToken)
            : null;

        await transaction.CommitAsync(cancellationToken);
        return new StaffWorklistPage(items, total, query.Page, query.PageSize, counts);
    }
}
