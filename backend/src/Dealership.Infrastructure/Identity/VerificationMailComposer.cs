using Dealership.Application.Accounts;
using Dealership.Infrastructure.Mail;

namespace Dealership.Infrastructure.Identity;

public sealed class VerificationMailComposer(AuthMailLinks links, EmailVerificationToken tokens)
{
    public OutboundMail Compose(EmailVerificationChallenge challenge)
    {
        var link = links.Create("/verify-email", tokens.Create(challenge));
        var body = $"Open this link and choose Verify email to confirm your address:\n\n{link}\n\n" +
            $"This link expires in {EmailVerificationPolicy.LifetimeHours} hours. If you did not create this account, ignore this message.";
        return new OutboundMail(challenge.RecipientEmail, "Verify your ApexAuto email", body);
    }
}
