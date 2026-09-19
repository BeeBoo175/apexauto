using System.Data;
using Dealership.Application.Accounts;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Accounts;

// The Administrator's customer directory. Contact is masked in the list; the account's own read shows it in full
// with the NRIC masked, and the number itself is only revealed through its own logged action.
public sealed class CustomerAccountDirectory(DealershipDbContext database) : ICustomerAccountDirectory
{
    public async Task<CustomerDirectoryResult<CustomerAccountPage>> ListAsync(Guid administratorId,
        CustomerDirectoryQuery query, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,
            cancellationToken);
        var actor = await ActorAsync(administratorId, cancellationToken);
        if (actor is null)
        {
            return new(StaffAccountReadStatus.Forbidden);
        }

        var customers = Customers();
        var counts = new CustomerAccountCounts(
            await Narrow(customers, query, CustomerAccountTabs.All).CountAsync(cancellationToken),
            await Narrow(customers, query, CustomerAccountTabs.Unverified).CountAsync(cancellationToken),
            await Narrow(customers, query, CustomerAccountTabs.Suspended).CountAsync(cancellationToken));
        var total = query.Tab switch
        {
            CustomerAccountTabs.Unverified => counts.Unverified,
            CustomerAccountTabs.Suspended => counts.Suspended,
            _ => counts.All
        };
        var rows = await Order(Narrow(customers, query, query.Tab), query)
            .ReadPageAsync(query.Page, query.PageSize, total, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var items = rows.Select(row => new CustomerAccountRow(row.Id, row.Name,
            row.Email is null ? null : PersonalDataMasking.Email(row.Email),
            row.Mobile is null ? null : PersonalDataMasking.Mobile(row.Mobile),
            row.Verified, row.Suspended, row.Closed)).ToArray();
        return new(StaffAccountReadStatus.Succeeded,
            new CustomerAccountPage(actor, items, total, query.Page, query.PageSize, counts));
    }

    public async Task<CustomerDirectoryResult<CustomerAccountDetail>> GetAsync(Guid administratorId, Guid customerId,
        CancellationToken cancellationToken)
    {
        var actor = await ActorAsync(administratorId, cancellationToken);
        if (actor is null)
        {
            return new(StaffAccountReadStatus.Forbidden);
        }

        var row = await (from user in database.Users.AsNoTracking()
                         where user.Id == customerId && user.Role == AccountRoles.Customer
                         join profile in database.CustomerProfiles.AsNoTracking() on user.Id equals profile.UserId
                         select new
                         {
                             user.Id,
                             Name = profile.LegalName ?? PersonLabels.FormerCustomer,
                             Username = user.AccountUsername,
                             user.Email,
                             profile.Mobile,
                             Address = profile.ResidentialAddress,
                             profile.Nric,
                             Verified = user.EmailConfirmed,
                             Suspended = user.SuspendedAt != null,
                             Closed = user.ClosedAt != null
                         }).SingleOrDefaultAsync(cancellationToken);
        return row is null
            ? new(StaffAccountReadStatus.NotFound)
            : new(StaffAccountReadStatus.Succeeded, new CustomerAccountDetail(actor, row.Id, row.Name, row.Username,
                row.Email, row.Mobile, row.Address, row.Nric is null ? null : PersonalDataMasking.Nric(row.Nric),
                row.Verified, row.Suspended, row.Closed));
    }

    private Task<CurrentActor?> ActorAsync(Guid administratorId, CancellationToken cancellationToken) =>
        database.Users.AsNoTracking()
            .Where(user => user.Id == administratorId && user.IsActive && user.Role == AccountRoles.Administrator)
            .Select(ActorRows.Current)
            .SingleOrDefaultAsync(cancellationToken);

    private IQueryable<CustomerRow> Customers()
    {
        return from user in database.Users.AsNoTracking()
               where user.Role == AccountRoles.Customer
               join profile in database.CustomerProfiles.AsNoTracking() on user.Id equals profile.UserId
               select new CustomerRow
               {
                   Id = user.Id,
                   Name = profile.LegalName ?? PersonLabels.FormerCustomer,
                   Email = user.Email,
                   Mobile = profile.Mobile,
                   Verified = user.EmailConfirmed,
                   Suspended = user.SuspendedAt != null,
                   Closed = user.ClosedAt != null
               };
    }

    // The chosen column orders the complete filtered list before paging; name then id keep equal values stable.
    // Status follows the label order the list shows: Active, Closed, Suspended, Unverified.
    private static IOrderedQueryable<CustomerRow> Order(IQueryable<CustomerRow> rows, CustomerDirectoryQuery query)
    {
        var descending = query.Descending;
        var ordered = query.Sort switch
        {
            "email" => descending ? rows.OrderByDescending(row => row.Email) : rows.OrderBy(row => row.Email),
            "mobile" => descending ? rows.OrderByDescending(row => row.Mobile) : rows.OrderBy(row => row.Mobile),
            "status" => descending
                ? rows.OrderByDescending(row => row.Closed ? 1 : row.Suspended ? 2 : row.Verified ? 0 : 3)
                : rows.OrderBy(row => row.Closed ? 1 : row.Suspended ? 2 : row.Verified ? 0 : 3),
            "name" when descending => rows.OrderByDescending(row => row.Name),
            _ => rows.OrderBy(row => row.Name)
        };
        return ordered.ThenBy(row => row.Name).ThenBy(row => row.Id);
    }

    // Search matches the full values even though the list shows them masked, so an Administrator can find a person
    // from what they quote.
    private static IQueryable<CustomerRow> Narrow(IQueryable<CustomerRow> rows, CustomerDirectoryQuery query, string tab)
    {
        rows = tab switch
        {
            // A closed account is neither unverified nor active, so it appears only under All.
            CustomerAccountTabs.Unverified => rows.Where(row => !row.Verified && !row.Closed),
            CustomerAccountTabs.Suspended => rows.Where(row => row.Suspended),
            _ => rows
        };
        if (query.Name.Length > 0)
        {
            var pattern = LiteralSearch.Pattern(query.Name);
            rows = rows.Where(row => EF.Functions.ILike(row.Name, pattern, "!"));
        }

        if (query.Email.Length > 0)
        {
            var pattern = LiteralSearch.Pattern(query.Email);
            rows = rows.Where(row => EF.Functions.ILike(row.Email!, pattern, "!"));
        }

        if (query.Mobile.Length > 0)
        {
            var pattern = LiteralSearch.Pattern(query.Mobile);
            rows = rows.Where(row => EF.Functions.ILike(row.Mobile!, pattern, "!"));
        }

        if (query.Search.Length > 0)
        {
            var pattern = LiteralSearch.Pattern(query.Search);
            rows = rows.Where(row => EF.Functions.ILike(row.Name, pattern, "!") ||
                EF.Functions.ILike(row.Email!, pattern, "!") || EF.Functions.ILike(row.Mobile!, pattern, "!"));
        }

        return rows;
    }

    private sealed class CustomerRow
    {
        public Guid Id { get; init; }

        public string Name { get; init; } = string.Empty;

        public string? Email { get; init; }

        public string? Mobile { get; init; }

        public bool Verified { get; init; }

        public bool Suspended { get; init; }

        public bool Closed { get; init; }
    }
}
