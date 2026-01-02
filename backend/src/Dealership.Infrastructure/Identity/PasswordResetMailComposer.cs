using Dealership.Application.Accounts;
using Dealership.Infrastructure.Mail;

namespace Dealership.Infrastructure.Identity;

public sealed class PasswordResetMailComposer(AuthMailLinks links, PasswordResetToken tokens)
{
    public OutboundMail Compose(PasswordResetChallenge challenge)
    {
        var link = links.Create("/reset-password", tokens.Create(challenge));
        var lifetime = PasswordRecoveryPolicy.LifetimeHours == 1
            ? "one hour"
            : $"{PasswordRecoveryPolicy.LifetimeHours} hours";
        var body = $"Open this link and deliberately submit a new password to recover your ApexAuto account:\n\n{link}\n\n" +
            $"This link expires in {lifetime}. If you did not request a password reset, ignore this message. " +
            "Opening the link does not change your password.";
        return new OutboundMail(challenge.RecipientEmail, "Reset your ApexAuto password", body);
    }
}
