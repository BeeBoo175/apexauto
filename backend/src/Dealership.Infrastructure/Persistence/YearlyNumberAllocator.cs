using Dealership.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Persistence;

internal static class YearlyNumberAllocator
{
    // Creates the year's row if needed, then locks it so concurrent transactions queue instead of repeating a number.
    // The table name comes from the EF model, never from input, so the raw statements are safe.
    public static async Task<string> NextAsync<TCounter>(DealershipDbContext database, int year,
        CancellationToken cancellationToken) where TCounter : YearlyNumberCounter
    {
        var table = database.Model.FindEntityType(typeof(TCounter))!.GetTableName()!;
#pragma warning disable EF1003
        await database.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"" + table + "\" (\"Year\", \"LastValue\") VALUES ({0}, 0) ON CONFLICT (\"Year\") DO NOTHING",
            [year], cancellationToken);
        var counter = await database.Set<TCounter>().FromSqlRaw(
            "SELECT * FROM \"" + table + "\" WHERE \"Year\" = {0} FOR UPDATE", year).SingleAsync(cancellationToken);
#pragma warning restore EF1003
        return counter.Next();
    }
}
