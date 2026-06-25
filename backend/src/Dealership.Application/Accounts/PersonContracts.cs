namespace Dealership.Application.Accounts;

// What another signed-in person may see of an account. Contact details are filled only where the viewer may see them.
public sealed record Person(
    Guid UserId,
    string Role,
    string Name,
    string Username,
    bool IsActive,
    Guid? BranchId,
    string? BranchName,
    string? Email,
    string? Mobile,
    Guid? PictureId);

public interface IPersonQuery
{
    Task<Person?> FindAsync(Guid userId, CancellationToken cancellationToken);
}
