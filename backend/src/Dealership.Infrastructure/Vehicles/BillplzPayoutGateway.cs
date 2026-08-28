using System.Globalization;
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
    private readonly BillplzClient billplz = new(client, options.Value);

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
            // The application's own refund mail is the one customer notice; the provider's receipt would go to the dealership.
            ["recipient_notification"] = "false",
            ["epoch"] = epoch,
            ["checksum"] = BillplzChecksum.Compute(_settings.XSignatureKey!, collection, request.AccountNumber, total, epoch)
        };

        using var response = await billplz.SendAsync(
            HttpMethod.Post, billplz.PaymentOrderAddress("payment_orders"), new FormUrlEncodedContent(form), cancellationToken);
        using var document = await BillplzClient.ReadAsync(response, cancellationToken);
        var root = document.RootElement;
        if (root.TryGetProperty("id", out var id) && id.GetString() is { Length: > 0 } payoutId)
        {
            var status = root.TryGetProperty("status", out var value) ? value.GetString() : null;
            return new PayoutResult(payoutId, OutcomeOf(status));
        }

        throw new PaymentGatewayException("The payment provider returned a payment order without a reference.");
    }

    public async Task<PayoutOutcome> GetPayoutOutcomeAsync(string payoutId, CancellationToken cancellationToken)
    {
        var epoch = clock.GetUtcNow().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var checksum = BillplzChecksum.Compute(_settings.XSignatureKey!, payoutId, epoch);
        var address = billplz.PaymentOrderAddress(
            $"payment_orders/{Uri.EscapeDataString(payoutId)}?epoch={epoch}&checksum={checksum}");
        using var response = await billplz.SendAsync(HttpMethod.Get, address, null, cancellationToken);
        using var document = await BillplzClient.ReadAsync(response, cancellationToken);
        return OutcomeOf(document.RootElement.TryGetProperty("status", out var status) ? status.GetString() : null);
    }
    public PayoutNotice? ReadNotice(IReadOnlyDictionary<string, string> fields)
    {
        if (_settings.XSignatureKey is not { Length: > 0 } key || !BillplzChecksum.VerifyPayoutCallback(fields, key) ||
            !fields.TryGetValue("id", out var payoutId) || string.IsNullOrEmpty(payoutId))
        {
            return null;
        }

        return new PayoutNotice(payoutId, fields.GetValueOrDefault("reference_id"),
            OutcomeOf(fields.GetValueOrDefault("status")));
    }

    // Billplz reports completed for a paid out order and refunded when the money came back.
    private static PayoutOutcome OutcomeOf(string? status) => status?.ToLowerInvariant() switch
    {
        "completed" => PayoutOutcome.Completed,
        "refunded" => PayoutOutcome.Failed,
        _ => PayoutOutcome.Processing
    };
}
