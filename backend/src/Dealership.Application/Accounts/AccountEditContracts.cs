namespace Dealership.Application.Accounts;

// An Administrator's edit of another person's recorded details. Address applies to customers only. The two security
// choices apply when the email changes: verification asks the person to confirm the new address, and a reset
// replaces the password so they must recover it. Every edit carries the reason that is recorded with it.
public sealed record AccountEditRequest(Guid OperationId, string? Reason, string? Name, string? Mobile,
    string? Address, string? Email, bool RequirePasswordReset, bool RequireEmailVerification);

public sealed record ValidatedAccountEdit(Guid OperationId, string Reason, string Name, string Mobile,
    string? Address, string Email, bool RequirePasswordReset, bool RequireEmailVerification);

public enum AccountEditStatus
{
    Succeeded,
    Invalid,
    Forbidden,
    NotFound,
    // Administrators edit others from here (their own details live on their account page), the email must not
    // belong to another account or pending invitation, and an edit that changes nothing is refused.
    Conflict
}

public interface IAccountEditStore
{
    Task<AccountEditStatus> EditAsync(Guid administratorId, Guid targetId, ValidatedAccountEdit edit,
        CancellationToken cancellationToken);
}
