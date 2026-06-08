namespace Dealership.Application.Accounts;

public sealed record BranchStaff(bool IsActive, string Role, Guid? BranchId)
{
    public bool CanHandle(Guid branchId)
    {
        return IsActive && BranchId == branchId &&
            Role is AccountRoles.SalesAgent or AccountRoles.BranchManager;
    }
}

public sealed record StaffHandler(Guid Id, string Email);
