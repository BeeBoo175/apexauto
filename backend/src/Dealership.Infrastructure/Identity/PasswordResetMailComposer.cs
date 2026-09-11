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
        var layout = new MailLayout(
            "Reset your password",
            ["We received a request to reset the password for your ApexAuto account. Choose a new password to continue."],
            [],
            [$"This link expires in {lifetime}. If you did not request a password reset, ignore this message. Opening the link does not change your password."],
            "Choose a new password",
            link);
        return new OutboundMail(MailRecipient.Require(challenge.RecipientEmail), "Reset your ApexAuto password",
            MailTemplate.RenderText(layout), MailTemplate.Render(layout));
    }
}
