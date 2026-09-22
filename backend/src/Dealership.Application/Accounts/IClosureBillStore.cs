namespace Dealership.Application.Accounts;

// The one port owner of the customer's unpaid provider bills, shared by closing and by an Administrator's erasure.
public interface IClosureBillStore
{
    // Requests whose unpaid provider bill still exists and must be deleted before the account can close.
    Task<IReadOnlyList<Guid>> UnreleasedBillRequestsAsync(Guid customerId, CancellationToken cancellationToken);
}
