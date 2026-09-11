namespace Dealership.Infrastructure.Mail;

// A recipient is removed once its account closes; a message that lost it must never be composed or sent.
internal static class MailRecipient
{
    public static string Require(string? email)
    {
        return email ?? throw new InvalidOperationException("The message recipient was removed after the account closed.");
    }
}
