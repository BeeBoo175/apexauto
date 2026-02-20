using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Persistence;

internal static class QueryPaging
{
    public static Task<T[]> ReadPageAsync<T>(this IQueryable<T> orderedRows, int page, int pageSize, int total,
        CancellationToken cancellationToken)
    {
        // The caller owns stable ordering and the transaction supplying the scoped total.
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        var offset = ((long)page - 1) * pageSize;
        // Only a counted row range can reach EF's integer offset; extreme pages stay empty.
        if (offset >= total)
        {
            return Task.FromResult(Array.Empty<T>());
        }

        return orderedRows.Skip((int)offset).Take(pageSize).ToArrayAsync(cancellationToken);
    }
}
