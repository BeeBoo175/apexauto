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

    public OutboundMail PaymentConfirmed(SaleDeliveryMail message, SaleDocument receipt)
    {
        var contents = receipt.Contents();
        var link = $"{_baseUrl}/account/sales/{message.SalesOrderId}/documents";
        var layout = new MailLayout(
            "Your payment is confirmed",
            ["We have confirmed your full payment. Your payment receipt is ready in your ApexAuto account."],
            [
                new MailDetail("Purchase order", contents.Sale.OrderNumber ?? string.Empty),
                new MailDetail("Payment receipt", receipt.Number),
                new MailDetail("Collection showroom", contents.BranchName),
                new MailDetail("Showroom address", contents.BranchAddress)
            ],
            [
                $"Collect your car at {contents.BranchName}. Contact the showroom to arrange a time before you visit.",
                "Bring your original MyKad."
            ],
            "View your documents",
            link);
        return new OutboundMail(MailRecipient.Require(message.RecipientEmail), "ApexAuto payment confirmed",
            MailTemplate.RenderText(layout), MailTemplate.Render(layout));
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
