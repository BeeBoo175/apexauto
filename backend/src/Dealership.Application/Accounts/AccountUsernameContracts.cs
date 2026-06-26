namespace Dealership.Application.Accounts;

public enum AccountUsernameStatus
{
    Changed,
    Invalid,
    Taken,
    Forbidden
}

public sealed record AccountUsernameRequest(Guid OperationId, string? Username);

public interface IAccountUsernameStore
{
    // Changes the account's username and records the account event in one transaction. An operation that was
    // already applied returns Changed again, so an uncertain retry is safe.
    Task<AccountUsernameStatus> ChangeAsync(Guid userId, Guid operationId, string username,
        CancellationToken cancellationToken);
}
