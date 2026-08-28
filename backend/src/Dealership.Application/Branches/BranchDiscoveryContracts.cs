namespace Dealership.Application.Branches;

public sealed record PublicBranch(Guid Id, string Name, string Address, string City, string State, string Phone,
    string ShortCode);

public sealed record PublicBranchPage(IReadOnlyList<PublicBranch> Items, int Total, int Page, int PageSize);

public interface IPublicBranchStore
{
    Task<PublicBranchPage> GetActiveAsync(int page, int pageSize, CancellationToken cancellationToken);

    Task<bool> IsActiveAsync(Guid branchId, CancellationToken cancellationToken);
}
