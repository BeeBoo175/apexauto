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
    private static readonly string[] StaffRoles =
    [
        AccountRoles.SalesAgent, AccountRoles.WorkshopTechnician, AccountRoles.BranchManager,
        AccountRoles.GeneralManager, AccountRoles.Administrator
    ];

    public static StaffAccountQuery? Parse(string? tab, string? search, string? role, Guid? branchId, int page,
        int pageSize)
    {
        var text = search?.Trim() ?? string.Empty;
        if (!StaffAccountTabs.IsValid(tab) || !ListSearch.IsValid(text) || !Pagination.IsValid(page, pageSize) ||
            branchId == Guid.Empty || (!string.IsNullOrEmpty(role) && !StaffRoles.Contains(role)))
        {
            return null;
        }

        return new(tab ?? StaffAccountTabs.All, text, string.IsNullOrEmpty(role) ? null : role, branchId, page, pageSize);
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
    Forbidden
}

public sealed record StaffAccountReadResult(StaffAccountReadStatus Status, StaffAccountPage? Page = null);

public interface IStaffAccountDirectory
{
    Task<StaffAccountReadResult> ListAsync(Guid administratorId, StaffAccountQuery query,
        CancellationToken cancellationToken);
}
