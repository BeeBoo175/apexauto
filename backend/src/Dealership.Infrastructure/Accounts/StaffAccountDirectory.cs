using System.Data;
using Dealership.Application.Accounts;
using Dealership.Application.Common;
using Dealership.Domain.Accounts;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Accounts;

// The Administrator's staff directory: real accounts with a staff profile beside pending and lapsed invitations,
// as one name-ordered list. Revoked and activated invitations are not people to manage and stay out.
public sealed class StaffAccountDirectory(DealershipDbContext database) : IStaffAccountDirectory
{
    public async Task<StaffAccountReadResult> ListAsync(Guid administratorId, StaffAccountQuery query,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,
            cancellationToken);
        var actor = await database.Users.AsNoTracking()
            .Where(user => user.Id == administratorId && user.IsActive && user.Role == AccountRoles.Administrator)
            .Select(ActorRows.Current)
            .SingleOrDefaultAsync(cancellationToken);
        if (actor is null)
        {
            return new(StaffAccountReadStatus.Forbidden);
        }

        var now = await database.DatabaseNowAsync(cancellationToken);
        var everyone = Accounts().Concat(Invitations(now));
        var counts = new StaffAccountCounts(
            await Narrow(everyone, query, StaffAccountTabs.All).CountAsync(cancellationToken),
            await Narrow(everyone, query, StaffAccountTabs.Active).CountAsync(cancellationToken),
            await Narrow(everyone, query, StaffAccountTabs.Invited).CountAsync(cancellationToken),
            await Narrow(everyone, query, StaffAccountTabs.Suspended).CountAsync(cancellationToken),
            await Narrow(everyone, query, StaffAccountTabs.Deactivated).CountAsync(cancellationToken));
        var rows = Narrow(everyone, query, query.Tab);
        var total = query.Tab switch
        {
            StaffAccountTabs.Active => counts.Active,
            StaffAccountTabs.Invited => counts.Invited,
            StaffAccountTabs.Suspended => counts.Suspended,
            StaffAccountTabs.Deactivated => counts.Deactivated,
            _ => counts.All
        };
        var page = await Order(rows, query)
            .ReadPageAsync(query.Page, query.PageSize, total, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var items = page.Select(row => new StaffAccountRow(row.Id, row.Name, row.Email, row.Mobile, row.Role,
            row.BranchId, row.BranchName, row.Status, row.PictureId, row.InvitationExpiresAt)).ToArray();
        return new(StaffAccountReadStatus.Succeeded, new StaffAccountPage(actor, items, total, query.Page, query.PageSize, counts));
    }

    // The account's retained events, newest first, for an Administrator reading one account's drawer.
    public async Task<AccountEventReadResult> HistoryAsync(Guid administratorId, Guid accountId, int page,
        int pageSize, CancellationToken cancellationToken)
    {
        if (!Pagination.IsValid(page, pageSize))
        {
            return new(StaffAccountReadStatus.Invalid);
        }

        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,
            cancellationToken);
        var actor = await database.Users.AsNoTracking()
            .Where(user => user.Id == administratorId && user.IsActive && user.Role == AccountRoles.Administrator)
            .Select(ActorRows.Current)
            .SingleOrDefaultAsync(cancellationToken);
        if (actor is null)
        {
            return new(StaffAccountReadStatus.Forbidden);
        }

        if (!await database.Users.AsNoTracking().AnyAsync(user => user.Id == accountId, cancellationToken))
        {
            return new(StaffAccountReadStatus.NotFound);
        }

        var events = database.AccountEvents.AsNoTracking().Where(item => item.TargetId == accountId);
        var total = await events.CountAsync(cancellationToken);
        var rows = await events.OrderByDescending(item => item.RecordedAt).ThenByDescending(item => item.Id)
            .ReadPageAsync(page, pageSize, total, cancellationToken);
        var names = await AccountNames.ReadAsync(database, rows.Select(item => (Guid?)item.ActorId), cancellationToken);
        var items = rows.Select(item => new AccountEventRow(item.Id, item.Kind.ToString(), item.RecordedAt, item.ActorId,
            names.GetValueOrDefault(item.ActorId, string.Empty), item.Reason,
            [.. (item.Changes ?? []).Select(change => new AccountEventChange(change.Field, change.Before, change.After))])).ToArray();
        await transaction.CommitAsync(cancellationToken);
        return new(StaffAccountReadStatus.Succeeded, new AccountEventPage(actor, accountId, items, total, page, pageSize));
    }

    private IQueryable<DirectoryRow> Accounts()
    {
        return from user in database.Users.AsNoTracking()
               where user.Role != AccountRoles.Customer
               join staff in database.StaffProfiles.AsNoTracking() on user.Id equals staff.UserId
               join branch in database.Branches.AsNoTracking() on user.BranchId equals branch.Id into branches
               from branch in branches.DefaultIfEmpty()
               join picture in database.AccountPictures.AsNoTracking().Where(AccountPicture.Current)
                   on user.Id equals picture.UserId into pictures
               from picture in pictures.DefaultIfEmpty()
               select new DirectoryRow
               {
                   Id = user.Id,
                   Name = staff.Name ?? PersonLabels.FormerEmployee,
                   Email = user.Email!,
                   Mobile = staff.Mobile ?? string.Empty,
                   Role = user.Role,
                   BranchId = user.BranchId,
                   BranchName = branch.Name,
                   Status = user.ClosedAt != null ? StaffAccountStatuses.Closed
                       : user.IsActive ? StaffAccountStatuses.Active
                       : user.SuspendedAt != null ? StaffAccountStatuses.Suspended : StaffAccountStatuses.Deactivated,
                   PictureId = (Guid?)picture.Id,
                   InvitationExpiresAt = null
               };
    }

    private IQueryable<DirectoryRow> Invitations(DateTimeOffset now)
    {
        return from invitation in database.StaffInvitations.AsNoTracking()
                   // A minimized (retired) invitation has no contact left to list.
               where invitation.ActivatedAt == null && invitation.RevokedAt == null && invitation.Email != null
               join branch in database.Branches.AsNoTracking() on invitation.BranchId equals branch.Id into branches
               from branch in branches.DefaultIfEmpty()
               select new DirectoryRow
               {
                   Id = invitation.Id,
                   Name = invitation.Name!,
                   Email = invitation.Email!,
                   Mobile = invitation.Mobile!,
                   Role = invitation.Role,
                   BranchId = invitation.BranchId,
                   BranchName = branch.Name,
                   Status = invitation.ExpiresAt > now ? StaffAccountStatuses.Invited : StaffAccountStatuses.InvitationExpired,
                   PictureId = null,
                   InvitationExpiresAt = invitation.ExpiresAt
               };
    }

    // The chosen column orders the complete filtered list before paging; name then id keep equal values stable.
    private static IOrderedQueryable<DirectoryRow> Order(IQueryable<DirectoryRow> rows, StaffAccountQuery query)
    {
        var descending = query.Descending;
        var ordered = query.Sort switch
        {
            "email" => descending ? rows.OrderByDescending(row => row.Email) : rows.OrderBy(row => row.Email),
            "mobile" => descending ? rows.OrderByDescending(row => row.Mobile) : rows.OrderBy(row => row.Mobile),
            "role" => descending ? rows.OrderByDescending(row => row.Role) : rows.OrderBy(row => row.Role),
            "branchName" => descending
                ? rows.OrderByDescending(row => row.BranchName) : rows.OrderBy(row => row.BranchName),
            "status" => descending ? rows.OrderByDescending(row => row.Status) : rows.OrderBy(row => row.Status),
            "name" when descending => rows.OrderByDescending(row => row.Name),
            _ => rows.OrderBy(row => row.Name)
        };
        return ordered.ThenBy(row => row.Name).ThenBy(row => row.Id);
    }

    // The search and the role and branch filters apply to every tab, so each tab's count follows them too.
    private static IQueryable<DirectoryRow> Narrow(IQueryable<DirectoryRow> rows, StaffAccountQuery query,
        string tab)
    {
        rows = tab switch
        {
            StaffAccountTabs.Active => rows.Where(row => row.Status == StaffAccountStatuses.Active),
            StaffAccountTabs.Invited => rows.Where(row => row.Status == StaffAccountStatuses.Invited ||
                row.Status == StaffAccountStatuses.InvitationExpired),
            StaffAccountTabs.Suspended => rows.Where(row => row.Status == StaffAccountStatuses.Suspended),
            StaffAccountTabs.Deactivated => rows.Where(row => row.Status == StaffAccountStatuses.Deactivated),
            _ => rows
        };
        if (query.Role is not null)
        {
            rows = rows.Where(row => row.Role == query.Role);
        }

        if (query.BranchId is not null)
        {
            rows = rows.Where(row => row.BranchId == query.BranchId);
        }

        if (query.Name.Length > 0)
        {
            var pattern = LiteralSearch.Pattern(query.Name);
            rows = rows.Where(row => EF.Functions.ILike(row.Name, pattern, "!"));
        }

        if (query.Email.Length > 0)
        {
            var pattern = LiteralSearch.Pattern(query.Email);
            rows = rows.Where(row => EF.Functions.ILike(row.Email, pattern, "!"));
        }

        if (query.Mobile.Length > 0)
        {
            var pattern = LiteralSearch.Pattern(query.Mobile);
            rows = rows.Where(row => EF.Functions.ILike(row.Mobile, pattern, "!"));
        }

        if (query.Search.Length > 0)
        {
            var pattern = LiteralSearch.Pattern(query.Search);
            rows = rows.Where(row => EF.Functions.ILike(row.Name, pattern, "!") ||
                EF.Functions.ILike(row.Email, pattern, "!") || EF.Functions.ILike(row.Mobile, pattern, "!"));
        }

        return rows;
    }

    // Set operations need a member-initialised shape, so rows are projected here and mapped after paging.
    private sealed class DirectoryRow
    {
        public Guid Id { get; init; }

        public string Name { get; init; } = string.Empty;

        public string Email { get; init; } = string.Empty;

        public string Mobile { get; init; } = string.Empty;

        public string Role { get; init; } = string.Empty;

        public Guid? BranchId { get; init; }

        public string? BranchName { get; init; }

        public string Status { get; init; } = string.Empty;

        public Guid? PictureId { get; init; }

        public DateTimeOffset? InvitationExpiresAt { get; init; }
    }
}
