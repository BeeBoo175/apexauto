using Dealership.Domain.Sales;
using System.Data;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dealership.Infrastructure.Vehicles;

internal static class ReservationReadSnapshot
{
    public static async Task<T> ReadAsync<T>(DealershipDbContext database, Func<Task<T>> read,
        T unavailable, CancellationToken cancellationToken)
    {
        // A competing release invalidates the whole snapshot, including its eligibility reads.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await using var transaction = await database.Database.BeginTransactionAsync(
                    IsolationLevel.RepeatableRead, cancellationToken);
                var result = await read();
                // Normal denials still retain any authoritative refund performed by this read.
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch (Exception exception) when (IsSerializationFailure(exception))
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        return unavailable;
    }

    public static async Task ReconcileAsync(DealershipDbContext database, Guid vehicleId,
        CancellationToken cancellationToken)
    {
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"Vehicles\" WHERE \"Id\" = {vehicleId} FOR UPDATE", cancellationToken);
        // Vehicle locks alone do not detect a release committed after a repeatable-read snapshot.
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"Reservations\" WHERE \"VehicleId\" = {vehicleId} AND \"IsActive\" FOR UPDATE", cancellationToken);
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT release_unavailable_reservation({vehicleId})", cancellationToken);
    }

    public static async Task<bool> ReconcileDueAsync(DealershipDbContext database, IQueryable<Guid> vehicleIds,
        int batchSize, CancellationToken cancellationToken)
    {
        var now = await database.DatabaseNowAsync(cancellationToken);
        // Only an open sale runs against a deadline; a handled one keeps its paused time however long it takes.
        var due = database.SalesOrders.Where(item => item.Status == SalesOrderStatus.Open && item.DueAt <= now &&
            vehicleIds.Contains(item.VehicleId));
        // Bound each transaction and retain PostgreSQL UUID lock order across all callers.
        var candidates = await due.OrderBy(item => item.VehicleId).Select(item => item.VehicleId)
            .Take(batchSize).ToArrayAsync(cancellationToken);
        foreach (var vehicleId in candidates)
        {
            await ReconcileAsync(database, vehicleId, cancellationToken);
        }

        return !await due.AnyAsync(cancellationToken);
    }

    private static bool IsSerializationFailure(Exception exception)
    {
        if (exception is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure })
        {
            return true;
        }

        return exception.InnerException is not null && IsSerializationFailure(exception.InnerException);
    }
}
