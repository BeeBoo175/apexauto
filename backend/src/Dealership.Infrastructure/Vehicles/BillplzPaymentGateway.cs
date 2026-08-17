using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
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
    public const string CallbackPath = "/api/payments/billplz/callback";

    private readonly BillplzSettings _settings = options.Value;

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
            ["callback_url"] = _settings.PublicBaseUrl!.TrimEnd('/') + CallbackPath,
            ["redirect_url"] = links.Absolute($"/confirm-deposit?request={request.RequestId:D}"),
            ["reference_1_label"] = "Request",
            ["reference_1"] = request.RequestId.ToString("D")
        };

        using var message = Message(HttpMethod.Post, "bills");
        message.Content = new FormUrlEncodedContent(form);
        using var response = await SendAsync(message, cancellationToken);
        try
        {
            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
            var root = document.RootElement;
            if (root.TryGetProperty("id", out var id) && id.GetString() is { Length: > 0 } billId &&
                root.TryGetProperty("url", out var url) && url.GetString() is { Length: > 0 } billUrl)
            {
                return new PaymentBill(billId, billUrl);
            }
        }
        catch (JsonException exception)
        {
            throw new PaymentGatewayException("The payment provider returned an unreadable answer.", exception);
        }

        throw new PaymentGatewayException("The payment provider returned a bill without an address.");
    }

    public async Task DeleteBillAsync(string billId, CancellationToken cancellationToken)
    {
        try
        {
            using var message = Message(HttpMethod.Delete, $"bills/{Uri.EscapeDataString(billId)}");
            using var response = await SendAsync(message, cancellationToken);
        }
        catch (PaymentGatewayException)
        {
            // The extra bill then simply stays unpaid until it expires.
        }
    }

    public bool VerifyCallback(IReadOnlyDictionary<string, string> fields)
    {
        return _settings.XSignatureKey is { Length: > 0 } key && BillplzSignature.Verify(fields, key);
    }

    private HttpRequestMessage Message(HttpMethod method, string path)
    {
        // A root without its trailing slash would drop the last path segment when the path is appended.
        var root = new Uri(_settings.BaseUrl.EndsWith('/') ? _settings.BaseUrl : _settings.BaseUrl + '/');
        var message = new HttpRequestMessage(method, new Uri(root, path));
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(_settings.ApiKey + ":"));
        message.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        return message;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage message, CancellationToken cancellationToken)
    {
        try
        {
            var response = await client.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var status = (int)response.StatusCode;
                response.Dispose();
                throw new PaymentGatewayException($"The payment provider answered {status}.");
            }

            return response;
        }
        catch (HttpRequestException exception)
        {
            throw new PaymentGatewayException("The payment provider could not be reached.", exception);
        }
    }
}
