using Dealership.Application.Vehicles;
using Dealership.Infrastructure.Configuration;
using Dealership.Infrastructure.Vehicles;
using Microsoft.Extensions.Options;

namespace Dealership.UnitTests;

// Billplz is one provider behind the payment and payout ports; these tests pin how its messages become the
// application's own notices.
public sealed class BillplzNoticeTests
{
    private const string Key = "S-secret-key";

    private static readonly IOptions<BillplzSettings> Settings =
        Options.Create(new BillplzSettings { XSignatureKey = Key });

    // Reading a notice touches neither the network nor the mail links.
    private static readonly BillplzPaymentGateway Payments = new(new HttpClient(), Settings, null!);

    private static readonly BillplzPayoutGateway Payouts = new(new HttpClient(), Settings, TimeProvider.System);

    [Fact]
    public void AServerCallbackReadsAsAPaymentWithItsPaidAmount()
    {
        var fields = new Dictionary<string, string> { ["id"] = "abc123", ["paid"] = "true", ["paid_amount"] = "50000" };
        fields["x_signature"] = BillplzSignature.Compute(fields, Key);

        var notice = Payments.ReadNotice(fields);

        Assert.Equal(new PaymentNotice("abc123", true, 50000, null), notice);
    }

    [Fact]
    public void ABrowserReturnReadsAsAPaymentInFullWithoutAnAmount()
    {
        var fields = new Dictionary<string, string> { ["billplz[id]"] = "abc123", ["billplz[paid]"] = "true" };
        fields["billplz[x_signature]"] = BillplzSignature.Compute(fields, Key);

        var notice = Payments.ReadNotice(fields);

        Assert.Equal(new PaymentNotice("abc123", true, null, null), notice);
    }

    // Billplz writes the payment time as "2015-03-09 16:23:59 +0800". The application stores and compares UTC only,
    // so the reader must hand over the same instant with a zero offset, whatever offset the provider used.
    [Theory]
    [InlineData("2026-11-17 01:51:15 +0800", "2026-11-16T17:51:15Z")]
    [InlineData("2026-11-16 12:51:15 -0500", "2026-11-16T17:51:15Z")]
    [InlineData("2026-11-16 17:51:15 +0000", "2026-11-16T17:51:15Z")]
    [InlineData("2026-11-16 17:51:15", "2026-11-16T17:51:15Z")]
    public void APaymentTimeInAnyOffsetReadsAsTheSameInstantInUtc(string written, string expectedUtc)
    {
        var fields = new Dictionary<string, string>
        {
            ["billplz[id]"] = "abc123",
            ["billplz[paid]"] = "true",
            ["billplz[paid_at]"] = written
        };
        fields["billplz[x_signature]"] = BillplzSignature.Compute(fields, Key);

        var notice = Payments.ReadNotice(fields);

        Assert.NotNull(notice);
        Assert.Equal(DateTimeOffset.Parse(expectedUtc, System.Globalization.CultureInfo.InvariantCulture), notice.PaidAt);
        Assert.Equal(TimeSpan.Zero, notice.PaidAt!.Value.Offset);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a time")]
    [InlineData("2026-13-45 99:99:99 +0800")]
    public void AnUnreadablePaymentTimeKeepsThePaymentAndDropsOnlyTheTime(string written)
    {
        var fields = new Dictionary<string, string>
        {
            ["billplz[id]"] = "abc123",
            ["billplz[paid]"] = "true",
            ["billplz[paid_at]"] = written
        };
        fields["billplz[x_signature]"] = BillplzSignature.Compute(fields, Key);

        Assert.Equal(new PaymentNotice("abc123", true, null, null), Payments.ReadNotice(fields));
    }

    [Fact]
    public void AnUnsignedOrTamperedPaymentMessageReadsAsNothing()
    {
        var fields = new Dictionary<string, string> { ["id"] = "abc123", ["paid"] = "false" };
        fields["x_signature"] = BillplzSignature.Compute(fields, Key);
        fields["paid"] = "true";

        Assert.Null(Payments.ReadNotice(fields));
        Assert.Null(Payments.ReadNotice(new Dictionary<string, string> { ["id"] = "abc123" }));
    }

    [Theory]
    [InlineData("completed", PayoutOutcome.Completed)]
    [InlineData("refunded", PayoutOutcome.Failed)]
    [InlineData("processing", PayoutOutcome.Processing)]
    [InlineData("pending", PayoutOutcome.Processing)]
    public void APayoutCallbackReadsAsTheMatchingOutcome(string status, PayoutOutcome expected)
    {
        var fields = new Dictionary<string, string>
        {
            ["id"] = "po-1",
            ["bank_account_number"] = "1234567890",
            ["status"] = status,
            ["total"] = "50000",
            ["reference_id"] = "ref-1",
            ["epoch"] = "1681895891"
        };
        fields["checksum"] = BillplzChecksum.Compute(Key, "po-1", "1234567890", status, "50000", "ref-1", "1681895891");

        Assert.Equal(new PayoutNotice("po-1", "ref-1", expected), Payouts.ReadNotice(fields));

        fields["checksum"] = "bad";
        Assert.Null(Payouts.ReadNotice(fields));
    }
}
