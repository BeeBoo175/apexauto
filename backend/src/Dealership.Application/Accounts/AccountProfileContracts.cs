namespace Dealership.Application.Accounts;

// The signed-in account's own identity for any role. Work contact details exist for staff only.
public sealed record AccountProfile(
    Guid UserId,
    string Role,
    string Name,
    string Username,
    string Email,
    bool EmailVerified,
    Guid? BranchId,
    string? BranchName,
    string? Mobile);

public interface IAccountProfileQuery
{
    Task<AccountProfile?> GetProfileAsync(Guid userId, CancellationToken cancellationToken);
}
