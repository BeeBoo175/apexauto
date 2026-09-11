using Dealership.Application.Accounts;
using Dealership.Infrastructure.Mail;

namespace Dealership.Infrastructure.Identity;

public sealed class VerificationMailComposer(AuthMailLinks links, EmailVerificationToken tokens)
{
    public OutboundMail Compose(EmailVerificationChallenge challenge)
    {
        var link = links.Create("/verify-email", tokens.Create(challenge));
        var layout = new MailLayout(
            "Confirm your email address",
            ["Thanks for creating an ApexAuto account. Confirm your email address to finish setting it up."],
            [],
            [$"This link expires in {EmailVerificationPolicy.LifetimeHours} hours. If you did not create this account, ignore this message."],
            "Verify email",
            link);
        return new OutboundMail(MailRecipient.Require(challenge.RecipientEmail), "Verify your ApexAuto email",
            MailTemplate.RenderText(layout), MailTemplate.Render(layout));
    }
}
