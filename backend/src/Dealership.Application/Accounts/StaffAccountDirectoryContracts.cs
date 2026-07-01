using Dealership.Application.Common;

namespace Dealership.Application.Accounts;

public static class StaffAccountTabs
{
    public const string All = "All";
    public const string Active = "Active";
    public const string Invited = "Invited";
    public const string Suspended = "Suspended";
    public const string Deactivated = "Deactivated";

    public static bool IsValid(string? tab) => tab is null or All or Active or Invited or Suspended or Deactivated;
}

public static class StaffAccountStatuses
{
    public const string Active = "Active";
    public const string Suspended = "Suspended";
    public const string Deactivated = "Deactivated";
    // A pending invitation that can still be activated, and one whose link has lapsed and needs a resend.
    public const string Invited = "Invited";
    public const string InvitationExpired = "InvitationExpired";
}

// Administrators read staff by name, work contact, role, branch and state; nothing about their work or any customer.
public sealed record StaffAccountQuery(string Tab, string Search, string? Role, Guid? BranchId, int Page, int PageSize)
{
    // Column filters: each narrows one column by a literal contains match.
    public string Name { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string Mobile { get; init; } = string.Empty;

    private static readonly string[] StaffRoles =
    [
        AccountRoles.SalesAgent, AccountRoles.WorkshopTechnician, AccountRoles.BranchManager,
        AccountRoles.GeneralManager, AccountRoles.Administrator
    ];

    public static StaffAccountQuery? Parse(string? tab, string? search, string? role, Guid? branchId, int page,
        int pageSize, string? name = null, string? email = null, string? mobile = null)
    {
        var text = search?.Trim() ?? string.Empty;
        var nameText = name?.Trim() ?? string.Empty;
        var emailText = email?.Trim() ?? string.Empty;
        var mobileText = mobile?.Trim() ?? string.Empty;
        if (!ListSearch.IsValid(nameText) || !ListSearch.IsValid(emailText) || !ListSearch.IsValid(mobileText) ||
            !StaffAccountTabs.IsValid(tab) || !ListSearch.IsValid(text) || !Pagination.IsValid(page, pageSize) ||
            branchId == Guid.Empty || (!string.IsNullOrEmpty(role) && !StaffRoles.Contains(role)))
        {
            return null;
        }

        return new(tab ?? StaffAccountTabs.All, text, string.IsNullOrEmpty(role) ? null : role, branchId, page, pageSize)
        {
            Name = nameText,
            Email = emailText,
            Mobile = mobileText
        };
    }
}

// Id is the account for an Active or Deactivated row and the invitation for an Invited row.
public sealed record StaffAccountRow(Guid Id, string Name, string Email, string Mobile, string Role, Guid? BranchId,
    string? BranchName, string Status, Guid? PictureId, DateTimeOffset? InvitationExpiresAt);

public sealed record StaffAccountCounts(int All, int Active, int Invited, int Suspended, int Deactivated);

public sealed record StaffAccountPage(CurrentActor Actor, IReadOnlyList<StaffAccountRow> Items, int Total, int Page,
    int PageSize, StaffAccountCounts Counts);

public enum StaffAccountReadStatus
{
    Succeeded,
    Invalid,
    Forbidden,
    NotFound
}

public sealed record StaffAccountReadResult(StaffAccountReadStatus Status, StaffAccountPage? Page = null);

// One retained administrative or self-service change to an account; the actor is read by name, never by email.
public sealed record AccountEventRow(Guid Id, string Kind, DateTimeOffset RecordedAt, Guid ActorId, string Actor,
    string? Reason, IReadOnlyList<AccountEventChange> Changes);

public sealed record AccountEventChange(string Field, string? Before, string? After);

public sealed record AccountEventPage(CurrentActor Actor, Guid AccountId, IReadOnlyList<AccountEventRow> Items,
    int TotalCount, int Page, int PageSize);

public sealed record AccountEventReadResult(StaffAccountReadStatus Status, AccountEventPage? Page = null);

public interface IStaffAccountDirectory
{
    Task<AccountEventReadResult> HistoryAsync(Guid administratorId, Guid accountId, int page, int pageSize,
        CancellationToken cancellationToken);

    Task<StaffAccountReadResult> ListAsync(Guid administratorId, StaffAccountQuery query,
        CancellationToken cancellationToken);
}
