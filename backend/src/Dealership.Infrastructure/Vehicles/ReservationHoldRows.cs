using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;

namespace Dealership.Infrastructure.Vehicles;

internal static class ReservationHoldRows
{
    // Customers who already hold a car: an active reservation, or a deposit request waiting for them other than
    // excludedRequestId. The reservation admission and the staff customer lookup both apply this one rule.
    public static IQueryable<Guid> HeldCustomers(DealershipDbContext database, DateTimeOffset now,
        Guid excludedRequestId = default)
    {
        var requests = database.ReservationDepositRequests
            .Where(ReservationDepositRequest.PendingAt(now))
            .Where(item => item.Id != excludedRequestId)
            .Select(item => item.CustomerId);
        return database.Reservations
            .Where(item => item.IsActive)
            .Select(item => item.CustomerId)
            .Concat(requests);
    }
}