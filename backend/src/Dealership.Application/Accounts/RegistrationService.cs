using System.Net.Mail;
using Dealership.Domain.Customers;

namespace Dealership.Application.Accounts;

public sealed class RegistrationService(ICustomerRegistrationStore store)
{
    public const int EmailMaximumLength = 254;
    public const int PasswordMaximumLength = 128;

    public async Task<RegistrationResult> RegisterAsync(RegistrationRequest request, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        var legalName = ValidateText(request.LegalName, "legalName", CustomerProfile.LegalNameMaximumLength, errors);
        var mobile = ValidateText(request.Mobile, "mobile", CustomerProfile.MobileMaximumLength, errors);
        var address = ValidateText(request.ResidentialAddress, "residentialAddress", CustomerProfile.AddressMaximumLength, errors);
        var email = request.Email?.Trim() ?? string.Empty;
        var nric = request.Nric?.Trim() ?? string.Empty;

        if (request.RegistrationKey == Guid.Empty)
        {
            errors["registrationKey"] = ["Provide a non-empty UUID registration key and reuse it when retrying."];
        }

        if (!CustomerProfile.IsValidNric(nric))
        {
            errors["nric"] = ["Enter a 12-digit NRIC without separators."];
        }

        if (email.Length > EmailMaximumLength || !MailAddress.TryCreate(email, out var parsedEmail) || parsedEmail.Address != email)
        {
            errors["email"] = ["Enter a valid email address."];
        }

        if (string.IsNullOrEmpty(request.Password) || request.Password.Length > PasswordMaximumLength)
        {
            errors["password"] = [$"Enter a password no longer than {PasswordMaximumLength} characters."];
        }

        if (errors.Count > 0)
        {
            return new RegistrationResult(RegistrationStatus.Invalid, errors);
        }

        var registration = new ValidatedRegistration(request.RegistrationKey, legalName, email,
            request.Password!, nric, mobile, address);
        return await store.RegisterAsync(registration, cancellationToken);
    }

    private static string ValidateText(string? value, string field, int maximumLength, Dictionary<string, string[]> errors)
    {
        var trimmed = value?.Trim() ?? string.Empty;

        if (trimmed.Length == 0 || trimmed.Length > maximumLength)
        {
            errors[field] = [$"Enter {field} using at most {maximumLength} characters."];
        }

        return trimmed;
    }
}
