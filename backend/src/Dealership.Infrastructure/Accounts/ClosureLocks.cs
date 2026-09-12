using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dealership.Infrastructure.Accounts;

// Lock steps shared by every command that closes a customer, whether the customer or an Administrator asked.
internal static class ClosureLocks
{
    // Vehicles by id with due holds released, then the customer's deposit requests and payments.
    public static async Task LockObligationsAsync(DealershipDbContext database, Guid customerId,
        CancellationToken cancellationToken)
    {
        var vehicleIds = await database.Reservations.Where(item => item.CustomerId == customerId && item.IsActive)
            .Select(item => item.VehicleId).Distinct().ToArrayAsync(cancellationToken);
        if (vehicleIds.Length > 0)
        {
            var vehicles = await database.Vehicles.FromSqlInterpolated(
                $"SELECT * FROM \"Vehicles\" WHERE \"Id\" = ANY({vehicleIds}) ORDER BY \"Id\" FOR UPDATE")
                .AsNoTracking().ToListAsync(cancellationToken);
            foreach (var vehicle in vehicles)
            {
                await database.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT release_unavailable_reservation({vehicle.Id})", cancellationToken);
            }
        }

        var requestIds = await database.Database.SqlQuery<Guid>(
            $"SELECT \"Id\" AS \"Value\" FROM \"ReservationDepositRequests\" WHERE \"CustomerId\" = {customerId} ORDER BY \"Id\" FOR UPDATE")
            .ToListAsync(cancellationToken);
        if (requestIds.Count > 0)
        {
            var ids = requestIds.ToArray();
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"ReservationDepositPayments\" WHERE \"RequestId\" = ANY({ids}) ORDER BY \"RequestId\" FOR UPDATE",
                cancellationToken);
        }
    }

    // The two advisory locks every closure takes first: the picture owner's, then the closure's own.
    public static async Task LockOwnerAsync(DealershipDbContext database, Guid userId,
        CancellationToken cancellationToken)
    {
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({"account-picture:" + userId}, 0))", cancellationToken);
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({"account-closure:" + userId}, 0))", cancellationToken);
    }

    public static bool IsLockConflict(Exception failure)
    {
        for (var current = failure; current is not null; current = current.InnerException)
        {
            if (current is PostgresException
                {
                    SqlState: PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.SerializationFailure
                })
            {
                return true;
            }
        }

        return false;
    }
}
