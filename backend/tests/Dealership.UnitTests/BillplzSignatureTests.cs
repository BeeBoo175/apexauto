using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Vehicles;

namespace Dealership.UnitTests;

public sealed class BillplzSignatureTests
{
    private const string Key = "S-secret-key";

    private static readonly Dictionary<string, string> Fields = new()
    {
        ["id"] = "abc123",
        ["paid"] = "true",
        ["paid_amount"] = "50000",
        ["Paid_at"] = "2026-11-13 10:00:00 +0800"
    };

    [Fact]
    public void ComputeSortsPairsByLowerCaseJoinsThemWithBarsAndMatchesAFixedVector()
    {
        // Message: idabc123|paid_amount50000|Paid_at2026-11-13 10:00:00 +0800|paidtrue. The underscore sorts before
        // letters in lower case, so paid_amount precedes paidtrue; the expected digest was computed independently.
        var signature = BillplzSignature.Compute(Fields, Key);

        Assert.Equal("6dcc70a7224909b7fe8eab37064691bc56ec495aa3fe06686d2832d24ba4401c", signature);
    }

    [Fact]
    public void NestedNamesJoinTheirBracketsAndTheSignatureFieldIsNeverSigned()
    {
        var redirect = new Dictionary<string, string>
        {
            ["billplz[id]"] = "abc123",
            ["billplz[paid]"] = "true"
        };
        var flat = new Dictionary<string, string> { ["billplzid"] = "abc123", ["billplzpaid"] = "true" };
        var signed = new Dictionary<string, string>(redirect) { ["billplz[x_signature]"] = "anything" };

        Assert.Equal(BillplzSignature.Compute(flat, Key), BillplzSignature.Compute(redirect, Key));
        Assert.Equal(BillplzSignature.Compute(redirect, Key), BillplzSignature.Compute(signed, Key));
    }

    [Fact]
    public void VerifyAcceptsTheRightSignatureAndRejectsATamperedFieldAKeyOrAMissingSignature()
    {
        var fields = new Dictionary<string, string>(Fields)
        {
            ["x_signature"] = BillplzSignature.Compute(Fields, Key)
        };

        Assert.True(BillplzSignature.Verify(fields, Key));
        Assert.False(BillplzSignature.Verify(fields, "other-key"));
        Assert.False(BillplzSignature.Verify(fields, string.Empty));

        fields["paid_amount"] = "100";
        Assert.False(BillplzSignature.Verify(fields, Key));

        fields.Remove("x_signature");
        Assert.False(BillplzSignature.Verify(fields, Key));
    }

    [Fact]
    public void ADepositPaymentRecordsTheFirstDeliveryOnly()
    {
        var now = new DateTimeOffset(2026, 11, 13, 2, 0, 0, TimeSpan.Zero);
        var payment = new ReservationDepositPayment(Guid.NewGuid(), "bill1", "https://pay.example/bill1", 50000, now);

        Assert.True(payment.RecordPaid(50000, true, now.AddMinutes(1)));
        Assert.False(payment.RecordPaid(0, false, now.AddMinutes(2)));
        Assert.Equal(DepositPaymentState.Paid, payment.State);
        Assert.Equal(50000, payment.PaidAmountSen);

        var short_ = new ReservationDepositPayment(Guid.NewGuid(), "bill2", "https://pay.example/bill2", 50000, now);
        Assert.True(short_.RecordPaid(100, true, now));
        Assert.Equal(DepositPaymentState.PaidUnmatched, short_.State);

        var lapsed = new ReservationDepositPayment(Guid.NewGuid(), "bill3", "https://pay.example/bill3", 50000, now);
        Assert.True(lapsed.RecordPaid(50000, false, now));
        Assert.Equal(DepositPaymentState.PaidUnmatched, lapsed.State);
    }
}
