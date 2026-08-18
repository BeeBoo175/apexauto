using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Dealership.Application.Vehicles;
using Dealership.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Dealership.Infrastructure.Vehicles;

// Billplz v5 payment orders: money out to a bank account the customer names, from the account's funded payment order
// limit. Requests carry an epoch and an HMAC-SHA512 checksum made with the X Signature key.
public sealed class BillplzPayoutGateway(HttpClient client, IOptions<BillplzSettings> options, TimeProvider clock)
    : IPayoutGateway
{
    private readonly BillplzSettings _settings = options.Value;

    public bool IsEnabled => _settings.Enabled && !string.IsNullOrWhiteSpace(_settings.PaymentOrderCollectionId);

    public async Task<PayoutResult> CreatePayoutAsync(PayoutRequest request, CancellationToken cancellationToken)
    {
        var epoch = clock.GetUtcNow().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var total = request.AmountSen.ToString(CultureInfo.InvariantCulture);
        var collection = _settings.PaymentOrderCollectionId!;
        var form = new Dictionary<string, string>
        {
            ["payment_order_collection_id"] = collection,
            ["bank_code"] = request.BankCode,
            ["bank_account_number"] = request.AccountNumber,
            ["name"] = request.AccountHolder,
            ["description"] = request.Description,
            ["total"] = total,
            ["reference_id"] = request.Reference,
            ["epoch"] = epoch,
            ["checksum"] = BillplzChecksum.Compute(_settings.XSignatureKey!, collection, request.AccountNumber, total, epoch)
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, new Uri(Root(), "payment_orders"))
        {
            Content = new FormUrlEncodedContent(form)
        };
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(_settings.ApiKey + ":"));
        message.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        try
        {
            using var response = await client.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new PaymentGatewayException($"The payment provider answered {(int)response.StatusCode}.");
            }

            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
            var root = document.RootElement;
            if (root.TryGetProperty("id", out var id) && id.GetString() is { Length: > 0 } payoutId)
            {
                var status = root.TryGetProperty("status", out var value) ? value.GetString() : null;
                return new PayoutResult(payoutId, OutcomeOf(status));
            }
        }
        catch (HttpRequestException exception)
        {
            throw new PaymentGatewayException("The payment provider could not be reached.", exception);
        }
        catch (JsonException exception)
        {
            throw new PaymentGatewayException("The payment provider returned an unreadable answer.", exception);
        }

        throw new PaymentGatewayException("The payment provider returned a payment order without a reference.");
    }

    public async Task<PayoutOutcome> GetPayoutOutcomeAsync(string payoutId, CancellationToken cancellationToken)
    {
        var epoch = clock.GetUtcNow().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var checksum = BillplzChecksum.Compute(_settings.XSignatureKey!, payoutId, epoch);
        var address = new Uri(Root(),
            $"payment_orders/{Uri.EscapeDataString(payoutId)}?epoch={epoch}&checksum={checksum}");
        using var message = new HttpRequestMessage(HttpMethod.Get, address);
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(_settings.ApiKey + ":"));
        message.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        try
        {
            using var response = await client.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new PaymentGatewayException($"The payment provider answered {(int)response.StatusCode}.");
            }

            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
            return OutcomeOf(document.RootElement.TryGetProperty("status", out var status) ? status.GetString() : null);
        }
        catch (HttpRequestException exception)
        {
            throw new PaymentGatewayException("The payment provider could not be reached.", exception);
        }
        catch (JsonException exception)
        {
            throw new PaymentGatewayException("The payment provider returned an unreadable answer.", exception);
        }
    }

    public PayoutNotice? ReadNotice(IReadOnlyDictionary<string, string> fields)
    {
        if (_settings.XSignatureKey is not { Length: > 0 } key || !BillplzChecksum.VerifyPayoutCallback(fields, key) ||
            !fields.TryGetValue("id", out var payoutId) || string.IsNullOrEmpty(payoutId))
        {
            return null;
        }

        return new PayoutNotice(payoutId, OutcomeOf(fields.GetValueOrDefault("status")));
    }

    // Billplz reports completed for a paid out order and refunded or cancelled when the money came back.
    private static PayoutOutcome OutcomeOf(string? status) => status?.ToLowerInvariant() switch
    {
        "completed" => PayoutOutcome.Completed,
        "refunded" or "cancelled" => PayoutOutcome.Failed,
        _ => PayoutOutcome.Processing
    };

    // Payment orders live under v5 of the same host the bills use.
    private Uri Root()
    {
        var root = _settings.BaseUrl.TrimEnd('/');
        var v5 = root.EndsWith("/v3", StringComparison.Ordinal) ? root[..^3] + "/v5" : root;
        return new Uri(v5 + "/");
    }
}
