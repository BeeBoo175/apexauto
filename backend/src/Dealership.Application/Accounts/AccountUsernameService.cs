namespace Dealership.Application.Accounts;

// Anyone may change their own display username; login and recovery stay on email.
public sealed class AccountUsernameService(IAccountUsernameStore store)
{
    public async Task<AccountUsernameStatus> ChangeAsync(Guid actorId, AccountUsernameRequest request,
        CancellationToken cancellationToken)
    {
        if (actorId == Guid.Empty)
        {
            return AccountUsernameStatus.Forbidden;
        }

        if (request.OperationId == Guid.Empty || !UsernamePolicy.IsValid(request.Username))
        {
            return AccountUsernameStatus.Invalid;
        }

        return await store.ChangeAsync(actorId, request.OperationId, request.Username!, cancellationToken);
    }
}
