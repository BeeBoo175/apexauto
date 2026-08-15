using Dealership.Application.Common;

namespace Dealership.Application.Accounts;

// Staff who may start intake, test drive and reservation work for a customer who is in front of them or on the phone.
public static class StaffAssistanceRoles
{
    public static bool Allows(string role) =>
        role is AccountRoles.SalesAgent or AccountRoles.BranchManager or AccountRoles.GeneralManager;
}

public sealed record AssistingStaff(Guid Id, string Role, Guid? BranchId)
{
    // Branch roles act only at their own branch, and the General Manager acts at any branch.
    public bool CanActAt(Guid branchId) => Role == AccountRoles.GeneralManager || BranchId == branchId;
}

// What the picker shows: enough to recognise the person, with contact masked like the Administrator's list.
public sealed record CustomerLookupRow(Guid Id, string Name, string Email, string Mobile);

// What the staff member is booking, which decides who is offered: a customer is limited to one active reservation
// and one outstanding test drive, while intake appointments have no such limit.
public enum CustomerLookupPurpose
{
    Any,
    Intake,
    TestDrive,
    Reservation
}

public sealed record CustomerLookupQuery(string Search, CustomerLookupPurpose Purpose = CustomerLookupPurpose.Any)
{
    public const int MinimumSearchLength = 2;

    public const int MaximumResults = 10;

    // The purpose token is camelCase on the wire; an empty one means no restriction, an unknown one is refused.
    public static CustomerLookupQuery? Parse(string? search, string? purpose = null)
    {
        var text = search?.Trim() ?? string.Empty;
        var parsed = purpose switch
        {
            null or "" => CustomerLookupPurpose.Any,
            "intake" => CustomerLookupPurpose.Intake,
            "testDrive" => CustomerLookupPurpose.TestDrive,
            "reservation" => CustomerLookupPurpose.Reservation,
            _ => (CustomerLookupPurpose?)null
        };
        return text.Length < MinimumSearchLength || !ListSearch.IsValid(text) || parsed is null ? null : new(text, parsed.Value);
    }
}

public sealed record CustomerLookupResult(StaffAccountReadStatus Status, IReadOnlyList<CustomerLookupRow>? Items = null);

public interface IStaffAssistanceQuery
{
    // Null when the actor is not an active staff member whose role may act for a customer.
    Task<AssistingStaff?> FindStaffAsync(Guid staffId, CancellationToken cancellationToken);

    // Only customers who can be served now are offered: active, not suspended, with a verified email.
    Task<CustomerLookupResult> SearchCustomersAsync(Guid staffId, CustomerLookupQuery query,
        CancellationToken cancellationToken);
}
