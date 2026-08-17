using Dealership.Application.Vehicles;
using Dealership.Infrastructure.Vehicles;

namespace Dealership.IntegrationTests;

// Stands in for Billplz: bills are never created remotely, and callbacks are signed with the same algorithm.
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

    public bool VerifyCallback(IReadOnlyDictionary<string, string> fields) =>
        BillplzSignature.Verify(fields, signatureKey);

    // What the customer's browser brings back from the payment page: nested names and no amount.
    public Dictionary<string, string> Return(string billId, bool paid, bool signed = true)
    {
        var fields = new Dictionary<string, string>
        {
            ["billplz[id]"] = billId,
            ["billplz[paid]"] = paid ? "true" : "false",
            ["billplz[paid_at]"] = "2026-11-13 09:00:00 +0800"
        };
        fields["billplz[x_signature]"] = signed ? BillplzSignature.Compute(fields, signatureKey) : "bad";
        return fields;
    }

    public Dictionary<string, string> Callback(string billId, bool paid, int paidAmountSen, bool signed = true)
    {
        var fields = new Dictionary<string, string>
        {
            ["id"] = billId,
            ["collection_id"] = "collection",
            ["paid"] = paid ? "true" : "false",
            ["state"] = paid ? "paid" : "due",
            ["amount"] = "50000",
            ["paid_amount"] = paidAmountSen.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["name"] = "Nur Aina",
            ["email"] = "customer@example.invalid"
        };
        fields["x_signature"] = signed ? BillplzSignature.Compute(fields, signatureKey) : "bad";
        return fields;
    }
}
