using Microsoft.AspNetCore.Identity;

namespace Dealership.Infrastructure.Identity;

public sealed class AccountPasswordValidator : IPasswordValidator<AccountUser>
{
    public Task<IdentityResult> ValidateAsync(UserManager<AccountUser> manager, AccountUser user, string? password)
    {
        // Identity's non-alphanumeric rule also accepts whitespace; the agreed symbol rule does not.
        var hasSymbol = password?.Any(character => !char.IsLetterOrDigit(character) &&
            !char.IsWhiteSpace(character) && !char.IsControl(character)) == true;

        if (hasSymbol)
        {
            return Task.FromResult(IdentityResult.Success);
        }

        return Task.FromResult(IdentityResult.Failed(new IdentityError
        {
            Code = "PasswordRequiresSymbol",
            Description = "Passwords must include a non-whitespace symbol."
        }));
    }
}
