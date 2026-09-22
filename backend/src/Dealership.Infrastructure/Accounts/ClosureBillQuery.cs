using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Accounts;

internal static class ClosureBillQuery
{
    // Requests whose pending deposit payment still has a provider bill.
    public static async Task<IReadOnlyList<Guid>> UnreleasedRequestsAsync(DealershipDbContext database,
        Guid customerId, CancellationToken cancellationToken)
    {
        return await (from payment in database.ReservationDepositPayments.AsNoTracking()
                      join request in database.ReservationDepositRequests.AsNoTracking() on payment.RequestId equals request.Id
                      where request.CustomerId == customerId && payment.State == DepositPaymentState.Pending &&
                          payment.BillDeletedAt == null
                      select payment.RequestId).ToListAsync(cancellationToken);
    }
}
