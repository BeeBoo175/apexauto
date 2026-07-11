using System.Net.Mail;
using Dealership.Domain.Accounts;
using Dealership.Domain.Customers;
using Dealership.Domain.Staff;

namespace Dealership.Application.Accounts;

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
            !AccountEvent.IsValidReason(reason) || string.IsNullOrEmpty(name) || string.IsNullOrEmpty(mobile) ||
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
