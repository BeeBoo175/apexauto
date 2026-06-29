using System.Net.Mail;
using Dealership.Domain.Accounts;
using Dealership.Domain.Customers;
using Dealership.Domain.Staff;

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

public sealed class AccountEditService(IAccountEditStore store)
{
    public Task<AccountEditStatus> EditAsync(Guid administratorId, Guid targetId, AccountEditRequest request,
        CancellationToken cancellationToken)
    {
        var edit = Validate(administratorId, targetId, request);
        return edit is null ? Task.FromResult(AccountEditStatus.Invalid)
            : store.EditAsync(administratorId, targetId, edit, cancellationToken);
    }

    private static ValidatedAccountEdit? Validate(Guid administratorId, Guid targetId, AccountEditRequest request)
    {
        var reason = request.Reason?.Trim();
        var name = request.Name?.Trim();
        var mobile = request.Mobile?.Trim();
        var address = request.Address?.Trim();
        var email = request.Email?.Trim();

        if (administratorId == Guid.Empty || targetId == Guid.Empty || request.OperationId == Guid.Empty ||
            string.IsNullOrEmpty(reason) || reason.Length > AccountEvent.ReasonMaximumLength ||
            reason.Any(char.IsControl) || string.IsNullOrEmpty(name) || string.IsNullOrEmpty(mobile) ||
            name.Length > StaffProfile.NameMaximumLength || name.Length > CustomerProfile.LegalNameMaximumLength ||
            mobile.Length > StaffProfile.MobileMaximumLength || mobile.Length > CustomerProfile.MobileMaximumLength ||
            (address is not null && (address.Length == 0 || address.Length > CustomerProfile.AddressMaximumLength)) ||
            string.IsNullOrEmpty(email) || email.Length > AccountCredentialPolicy.EmailMaximumLength ||
            !MailAddress.TryCreate(email, out var parsed) || parsed.Address != email)
        {
            return null;
        }

        return new ValidatedAccountEdit(request.OperationId, reason, name, mobile, address, email,
            request.RequirePasswordReset, request.RequireEmailVerification);
    }
}
