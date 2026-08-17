using Dealership.Application.Vehicles;
using Dealership.Infrastructure.Vehicles;

namespace Dealership.IntegrationTests;

// Stands in for Billplz payment orders: nothing leaves the process, and callbacks carry the real checksum.
internal sealed class FakePayoutGateway(string signatureKey) : IPayoutGateway
{
    private readonly List<PayoutRequest> _payouts = [];

    public bool IsEnabled => true;

    public IReadOnlyList<PayoutRequest> Payouts => _payouts;

    public string ReportedStatus { get; set; } = "processing";

    public bool FailNext { get; set; }

    public Task<PayoutResult> CreatePayoutAsync(PayoutRequest request, CancellationToken cancellationToken)
    {
        if (FailNext)
        {
            FailNext = false;
            throw new PaymentGatewayException("The payment provider could not be reached.");
        }

        _payouts.Add(request);
        return Task.FromResult(new PayoutResult($"po-{_payouts.Count}", "processing"));
    }

    public Task<string> GetPayoutStatusAsync(string payoutId, CancellationToken cancellationToken) =>
        Task.FromResult(ReportedStatus);

    public bool VerifyPayoutCallback(IReadOnlyDictionary<string, string> fields) =>
        BillplzChecksum.VerifyPayoutCallback(fields, signatureKey);

    public Dictionary<string, string> Callback(string payoutId, string status, PayoutRequest payout, bool signed = true)
    {
        var fields = new Dictionary<string, string>
        {
            ["id"] = payoutId,
            ["bank_account_number"] = payout.AccountNumber,
            ["status"] = status,
            ["total"] = payout.AmountSen.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["reference_id"] = payout.Reference,
            ["epoch"] = "1681895891"
        };
        fields["checksum"] = signed
            ? BillplzChecksum.Compute(signatureKey, payoutId, payout.AccountNumber, status, fields["total"],
                payout.Reference, fields["epoch"])
            : "bad";
        return fields;
    }
}
