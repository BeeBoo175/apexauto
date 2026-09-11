using System.Globalization;
using Dealership.Application.Vehicles;
using Dealership.Infrastructure.Configuration;
using Dealership.Infrastructure.Identity;
using Microsoft.Extensions.Options;

namespace Dealership.Infrastructure.Vehicles;

// Billplz v3: a bill per deposit, created with the secret key as the Basic username and a blank password. The
// callback goes to this API and the browser returns to the confirm page of the same request.
public sealed class BillplzPaymentGateway(HttpClient client, IOptions<BillplzSettings> options, AuthMailLinks links)
    : IPaymentGateway
{
    private readonly BillplzSettings _settings = options.Value;
    private readonly BillplzClient billplz = new(client, options.Value);

    public bool IsEnabled => _settings.Enabled;

    public async Task<PaymentBill> CreateBillAsync(PaymentBillRequest request, CancellationToken cancellationToken)
    {
        var form = new Dictionary<string, string>
        {
            ["collection_id"] = _settings.CollectionId!,
            ["name"] = request.PayerName,
            ["email"] = request.PayerEmail,
            ["amount"] = request.AmountSen.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["description"] = request.Description,
            ["callback_url"] = _settings.PublicBaseUrl!.TrimEnd('/') + PaymentRoutes.Callback,
            ["redirect_url"] = links.Absolute($"/confirm-deposit?request={request.RequestId:D}"),
            ["reference_1_label"] = "Request",
            ["reference_1"] = request.RequestId.ToString("D")
        };

        using var response = await billplz.SendAsync(
            HttpMethod.Post, billplz.BillAddress("bills"), new FormUrlEncodedContent(form), cancellationToken);
        using var document = await BillplzClient.ReadAsync(response, cancellationToken);
        var root = document.RootElement;
        if (root.TryGetProperty("id", out var id) && id.GetString() is { Length: > 0 } billId &&
            root.TryGetProperty("url", out var url) && url.GetString() is { Length: > 0 } billUrl)
        {
            return new PaymentBill(billId, billUrl);
        }

        throw new PaymentGatewayException("The payment provider returned a bill without an address.");
    }

    public async Task<bool> DeleteBillAsync(string billId, CancellationToken cancellationToken)
    {
        try
        {
            var address = billplz.BillAddress($"bills/{Uri.EscapeDataString(billId)}");
            using var response = await billplz.SendAsync(HttpMethod.Delete, address, null, cancellationToken);
            return true;
        }
        catch (PaymentGatewayException)
        {
            // A refused or unreachable delete leaves the bill payable, which the caller must not assume away.
            return false;
        }
    }

    public PaymentNotice? ReadNotice(IReadOnlyDictionary<string, string> fields)
    {
        if (_settings.XSignatureKey is not { Length: > 0 } key || !BillplzSignature.Verify(fields, key))
        {
            return null;
        }

        var values = FlatFields(fields);
        if (!values.TryGetValue("id", out var billId) || string.IsNullOrEmpty(billId))
        {
            return null;
        }

        var paid = values.TryGetValue("paid", out var state) &&
            string.Equals(state, "true", StringComparison.OrdinalIgnoreCase);
        int? paidAmount = values.TryGetValue("paid_amount", out var amount) && int.TryParse(amount, out var sen)
            ? sen
            : null;
        // Billplz writes the time as "2015-03-09 16:23:59 +0800".
        DateTimeOffset? paidAt = values.TryGetValue("paid_at", out var time) && DateTimeOffset.TryParse(time,
            CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : null;
        return new PaymentNotice(billId, paid, paidAmount, paidAt);
    }

    // The browser return names its fields billplz[id] and the server callback sends them flat; both read as id.
    private static Dictionary<string, string> FlatFields(IReadOnlyDictionary<string, string> fields)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, value) in fields)
        {
            var flat = name.Replace("[", string.Empty).Replace("]", string.Empty)
                .Replace("billplz", string.Empty, StringComparison.Ordinal);
            values.TryAdd(flat, value);
        }

        return values;
    }
}
