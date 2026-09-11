using Dealership.Infrastructure.Mail;
using Microsoft.Extensions.Configuration;

namespace Dealership.Infrastructure.Sales;

public sealed class SaleDeliveryMailComposer
{
    private readonly string _baseUrl;

    public SaleDeliveryMailComposer(IConfiguration configuration)
    {
        if (!Uri.TryCreate(configuration["APP_BASE_URL"], UriKind.Absolute, out var origin) ||
            origin.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException("Delivery mail requires an absolute frontend URL.");
        }

        _baseUrl = configuration["APP_BASE_URL"]!.TrimEnd('/');
    }

    public OutboundMail Confirmation(SaleDeliveryMail message, SaleDocument document)
    {
        var sale = document.Contents().Sale;
        var link = $"{_baseUrl}/account/sales/{message.SalesOrderId}/documents";
        var layout = new MailLayout(
            "Your vehicle handover is complete",
            ["Congratulations on your ApexAuto vehicle. Your handover is complete and your documents are ready."],
            [
                new MailDetail("Purchase order", sale.OrderNumber ?? string.Empty),
                new MailDetail("Handover note", document.Number)
            ],
            ["Sign in to your own account to view your issued documents."],
            "View your documents",
            link);
        return new OutboundMail(MailRecipient.Require(message.RecipientEmail), "ApexAuto vehicle handover confirmation",
            MailTemplate.RenderText(layout), MailTemplate.Render(layout));
    }
}
