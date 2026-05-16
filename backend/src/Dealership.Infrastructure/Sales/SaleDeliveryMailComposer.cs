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
        var body = $"Your ApexAuto vehicle handover is complete.\n" +
            $"Purchase order: {sale.OrderNumber}\nHandover note: {document.Number}\n\n" +
            $"Sign in to your own account to view your issued documents:\n{link}";
        return new OutboundMail(message.RecipientEmail, "ApexAuto vehicle handover confirmation", body);
    }
}
