using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Vehicles;

namespace Dealership.UnitTests;

public sealed class BillplzChecksumTests
{
    private const string Key = "S-secret-key";

    private static readonly DateTimeOffset Now = new(2026, 11, 13, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ComputeConcatenatesValuesWithNoSeparatorAndMatchesAFixedVector()
    {
        // Message: po-11234567890completed50000ref-11681895891; the digest was computed independently.
        var checksum = BillplzChecksum.Compute(Key, "po-1", "1234567890", "completed", "50000", "ref-1", "1681895891");

        Assert.Equal(
            "559043a9b03a3c42d8e08928983ea0a60ffd6b9b6ef995ed1f5cdf3537fde913c000aca5302493efb4477a9354ebf4033fcda56d326e392ef7874c8a4014e2c8",
            checksum);
    }

    [Fact]
    public void ACallbackIsVerifiedOverItsSixSignedFieldsOnly()
    {
        var fields = new Dictionary<string, string>
        {
            ["id"] = "po-1",
            ["bank_account_number"] = "1234567890",
            ["status"] = "completed",
            ["total"] = "50000",
            ["reference_id"] = "ref-1",
            ["epoch"] = "1681895891",
            ["name"] = "Not signed"
        };
        fields["checksum"] = BillplzChecksum.Compute(Key, "po-1", "1234567890", "completed", "50000", "ref-1", "1681895891");

        Assert.True(BillplzChecksum.VerifyPayoutCallback(fields, Key));
        fields["name"] = "Changed";
        Assert.True(BillplzChecksum.VerifyPayoutCallback(fields, Key));
        fields["status"] = "refunded";
        Assert.False(BillplzChecksum.VerifyPayoutCallback(fields, Key));
        Assert.False(BillplzChecksum.VerifyPayoutCallback(fields, "other-key"));
        fields.Remove("checksum");
        Assert.False(BillplzChecksum.VerifyPayoutCallback(fields, Key));
    }

    [Fact]
    public void ARefundKeepsOnlyTheLastFourDigitsAndStartsAgainOnlyAfterFailing()
    {
        var refund = new ReservationRefund(Guid.NewGuid(), Guid.NewGuid(), 50000, "MBBEMYKL", "1234567890",
            " Nur Aina ", Now);

        Assert.Equal("7890", refund.AccountLast4);
        Assert.Equal("Nur Aina", refund.AccountHolder);
        Assert.Equal($"{refund.PaymentRequestId:N}-1", refund.Reference);
        Assert.False(refund.Retry("MBBEMYKL", "1234567890", "Nur Aina", Now));

        refund.AttachPayout("po-1");
        Assert.True(refund.Settle(false, Now.AddMinutes(1)));
        Assert.False(refund.Settle(true, Now.AddMinutes(2)));
        Assert.Equal(RefundState.Failed, refund.State);

        Assert.True(refund.Retry("PBBEMYKL", "9876543210", "Nur Aina", Now.AddMinutes(3)));
        Assert.Equal(RefundState.Processing, refund.State);
        Assert.Equal(2, refund.Attempt);
        Assert.Null(refund.PayoutId);
        Assert.Equal("3210", refund.AccountLast4);
        Assert.True(refund.Settle(true, Now.AddMinutes(4)));
        Assert.Equal(RefundState.Completed, refund.State);
    }

    [Theory]
    [InlineData("MBBEMYKL", "12345", "Nur Aina")]
    [InlineData("MBBEMYKL", "12345678a0", "Nur Aina")]
    [InlineData("MBB EMYKL", "1234567890", "Nur Aina")]
    [InlineData("MBBEMYKL", "1234567890", "   ")]
    public void ARefundRejectsABadBankAccountNumberOrHolder(string bank, string number, string holder)
    {
        Assert.Throws<ArgumentException>(() =>
            new ReservationRefund(Guid.NewGuid(), Guid.NewGuid(), 50000, bank, number, holder, Now));
    }
}
