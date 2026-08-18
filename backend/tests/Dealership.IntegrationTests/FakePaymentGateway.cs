using System.Globalization;
using Dealership.Application.Vehicles;

namespace Dealership.IntegrationTests;

// A provider that is not Billplz: bills are never created remotely, and a message is genuine only when it carries the
// shared key. The services under test therefore work against the port alone.
internal sealed class FakePaymentGateway(string signatureKey) : IPaymentGateway
{
    private readonly List<string> _bills = [];
    private readonly List<string> _deleted = [];

    public bool IsEnabled => true;

    public IReadOnlyList<string> Bills => _bills;

    public IReadOnlyList<string> Deleted => _deleted;

    public Task<PaymentBill> CreateBillAsync(PaymentBillRequest request, CancellationToken cancellationToken)
    {
        var id = $"bill{_bills.Count + 1:D3}{request.RequestId:N}"[..16];
        _bills.Add(id);
        return Task.FromResult(new PaymentBill(id, $"https://pay.example/bills/{id}"));
    }

    public Task DeleteBillAsync(string billId, CancellationToken cancellationToken)
    {
        _deleted.Add(billId);
        return Task.CompletedTask;
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
        return new PaymentNotice(billId, paid, amount);
    }

    // What the customer's browser brings back from the payment page: no amount.
    public Dictionary<string, string> Return(string billId, bool paid, bool signed = true) => new()
    {
        ["bill"] = billId,
        ["paid"] = paid ? "yes" : "no",
        ["signature"] = signed ? signatureKey : "bad"
    };

    public Dictionary<string, string> Callback(string billId, bool paid, int paidAmountSen, bool signed = true)
    {
        var fields = Return(billId, paid, signed);
        fields["amount"] = paidAmountSen.ToString(CultureInfo.InvariantCulture);
        return fields;
    }
}
