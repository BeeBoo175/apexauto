using System.Globalization;
using Dealership.Application.Vehicles;

namespace Dealership.IntegrationTests;

// A provider that is not Billplz: bills are never created remotely, and a message is genuine only when it carries the
// shared key. The services under test therefore work against the port alone.
internal sealed class FakePaymentGateway(string signatureKey, bool enabled = true) : IPaymentGateway
{
    private readonly List<string> _bills = [];
    private readonly List<string> _deleted = [];

    public bool IsEnabled => enabled;

    public IReadOnlyList<string> Bills => _bills;

    public IReadOnlyList<string> Deleted => _deleted;

    public Task<PaymentBill> CreateBillAsync(PaymentBillRequest request, CancellationToken cancellationToken)
    {
        var id = $"bill{_bills.Count + 1:D3}{request.RequestId:N}"[..16];
        _bills.Add(id);
        return Task.FromResult(new PaymentBill(id, $"https://pay.example/bills/{id}"));
    }

    // A provider outage: the delete is attempted and recorded but never confirmed.
    public bool DeleteFails { get; set; }

    public Task<bool> DeleteBillAsync(string billId, CancellationToken cancellationToken)
    {
        _deleted.Add(billId);
        return Task.FromResult(!DeleteFails);
    }

    public PaymentNotice? ReadNotice(IReadOnlyDictionary<string, string> fields)
    {
        if (!fields.TryGetValue("signature", out var signature) || signature != signatureKey ||
            !fields.TryGetValue("bill", out var billId))
        {
            return null;
        }

        var paid = fields.TryGetValue("paid", out var state) && state == "yes";
        int? amount = fields.TryGetValue("amount", out var sum) && int.TryParse(sum, out var sen) ? sen : null;
        // Kept exactly as the provider wrote it, offset included, so tests can prove the services normalise it.
        DateTimeOffset? paidAt = fields.TryGetValue("paid_at", out var time) &&
            DateTimeOffset.TryParse(time, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : null;
        return new PaymentNotice(billId, paid, amount, paidAt);
    }

    // What the customer's browser brings back from the payment page: no amount.
    public Dictionary<string, string> Return(string billId, bool paid, bool signed = true,
        DateTimeOffset? paidAt = null)
    {
        var fields = new Dictionary<string, string>
        {
            ["bill"] = billId,
            ["paid"] = paid ? "yes" : "no",
            ["signature"] = signed ? signatureKey : "bad"
        };
        if (paidAt is { } time)
        {
            fields["paid_at"] = time.ToString("O", CultureInfo.InvariantCulture);
        }

        return fields;
    }

    public Dictionary<string, string> Callback(string billId, bool paid, int paidAmountSen, bool signed = true)
    {
        var fields = Return(billId, paid, signed);
        fields["amount"] = paidAmountSen.ToString(CultureInfo.InvariantCulture);
        return fields;
    }
}
