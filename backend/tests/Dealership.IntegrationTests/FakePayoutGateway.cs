using Dealership.Application.Vehicles;

namespace Dealership.IntegrationTests;

// A provider that is not Billplz: nothing leaves the process, and a callback is genuine only when it carries the
// shared key.
internal sealed class FakePayoutGateway(string signatureKey) : IPayoutGateway
{
    private readonly List<PayoutRequest> _payouts = [];

    public bool IsEnabled => true;

    public IReadOnlyList<PayoutRequest> Payouts => _payouts;

    public PayoutOutcome ReportedOutcome { get; set; } = PayoutOutcome.Processing;

    public bool FailNext { get; set; }

    public Task<PayoutResult> CreatePayoutAsync(PayoutRequest request, CancellationToken cancellationToken)
    {
        if (FailNext)
        {
            FailNext = false;
            throw new PaymentGatewayException("The payment provider could not be reached.");
        }

        _payouts.Add(request);
        return Task.FromResult(new PayoutResult($"po-{_payouts.Count}", PayoutOutcome.Processing));
    }

    public Task<PayoutOutcome> GetPayoutOutcomeAsync(string payoutId, CancellationToken cancellationToken) =>
        Task.FromResult(ReportedOutcome);

    public PayoutNotice? ReadNotice(IReadOnlyDictionary<string, string> fields)
    {
        if (!fields.TryGetValue("signature", out var signature) || signature != signatureKey ||
            !fields.TryGetValue("payout", out var payoutId))
        {
            return null;
        }

        var outcome = fields.GetValueOrDefault("outcome") switch
        {
            "completed" => PayoutOutcome.Completed,
            "failed" => PayoutOutcome.Failed,
            _ => PayoutOutcome.Processing
        };
        return new PayoutNotice(payoutId, outcome);
    }

    public Dictionary<string, string> Callback(string payoutId, string outcome, bool signed = true) => new()
    {
        ["payout"] = payoutId,
        ["outcome"] = outcome,
        ["signature"] = signed ? signatureKey : "bad"
    };
}
