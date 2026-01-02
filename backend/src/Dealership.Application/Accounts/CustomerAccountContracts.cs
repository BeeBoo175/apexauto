namespace Dealership.Application.Accounts;

public sealed record CustomerAccount(Guid UserId, string LegalName, string Email, bool EmailVerified);

public interface ICustomerAccountQuery
{
    Task<CustomerAccount?> GetAccountAsync(Guid userId, CancellationToken cancellationToken);
}
