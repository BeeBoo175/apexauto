using Dealership.Infrastructure.Mail;

namespace Dealership.UnitTests;

public sealed class MailTemplateTests
{
    [Fact]
    public void RenderEncodesEveryValueAndKeepsTheActionLink()
    {
        var html = MailTemplate.Render(new MailLayout(
            "Reservation <receipt>",
            ["Hello & welcome"],
            [new MailDetail("Branch", "Glenmarie <main>")],
            ["Ignore this message."],
            "Open",
            "https://example.invalid/verify?a=1&b=2"));

        Assert.Contains("Reservation &lt;receipt&gt;", html);
        Assert.Contains("Glenmarie &lt;main&gt;", html);
        Assert.Contains("https://example.invalid/verify?a=1&amp;b=2", html);
        Assert.DoesNotContain("<main>", html);
    }

    [Fact]
    public void RenderOmitsTheActionWhenNoLinkIsGiven()
    {
        var html = MailTemplate.Render(new MailLayout("Cancelled", ["Done."], [], []));

        Assert.DoesNotContain("<a href", html);
    }
}
