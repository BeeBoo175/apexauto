using Dealership.Application.Accounts;
using Dealership.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace Dealership.Infrastructure.Accounts;

internal static class ClosurePasswords
{
    // A missing hash still costs one hash, so an account without a password is no timing oracle.
    public static ClosurePasswordCheck Verify(AccountUser user, string password,
        IPasswordHasher<AccountUser> passwords, MissingAccountPasswordHash missingHash)
    {
        if (user.PasswordHash is null)
        {
            missingHash.Verify(passwords, password);
            return new(ClosurePasswordOutcome.WrongPassword);
        }

        return passwords.VerifyHashedPassword(user, user.PasswordHash, password) == PasswordVerificationResult.Failed
            ? new(ClosurePasswordOutcome.WrongPassword)
            : new(ClosurePasswordOutcome.Verified, user.SecurityStamp);
    }
}
