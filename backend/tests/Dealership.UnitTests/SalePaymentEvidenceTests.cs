using Dealership.Domain.Common;
using Dealership.Domain.Sales;

namespace Dealership.UnitTests;

public sealed class SalePaymentEvidenceTests
{
    private static readonly SalePaymentEvidence Cash = new(SalePaymentMethod.Cash, 50000m,
        "TRX-20261102-1842", new DateOnly(2026, 11, 2), null, 0m, null, false, false);

    private static readonly SalePaymentEvidence HirePurchase = new(SalePaymentMethod.HirePurchase, 5000m,
        null, null, "Maybank", 45000m, "LOU-2026-1842", true, false);

    private static readonly SalePaymentEvidence Zero = new(SalePaymentMethod.ZeroBalance, 0m,
        null, null, null, 0m, null, false, true);

    [Theory]
    [InlineData(SalePaymentMethod.Cash)]
    [InlineData(SalePaymentMethod.BankTransfer)]
    public void ReceivedPaymentRequiresTheExactApprovedBalance(SalePaymentMethod method)
    {
        var evidence = Cash with { Method = method };

        Assert.Equal(evidence, evidence.Normalize(50000m));
        Assert.Throws<ArgumentException>(() => evidence.Normalize(49999.99m));
        Assert.Throws<ArgumentException>(() => evidence.Normalize(50000.01m));
    }

    [Fact]
    public void HirePurchaseAddsVerifiedFinancingAndReceivedContributionOnce()
    {
        Assert.Equal(HirePurchase, HirePurchase.Normalize(50000m));
        Assert.Throws<ArgumentException>(() => HirePurchase.Normalize(49999.99m));
        Assert.Throws<ArgumentException>(() => HirePurchase.Normalize(50000.01m));

        var fullyFinanced = HirePurchase with { ReceivedAmount = 0m, FinancingAmount = 50000m };
        Assert.Equal(fullyFinanced, fullyFinanced.Normalize(50000m));
    }

    [Fact]
    public void ApprovedBalanceAlreadyIncludesDepositAndTradeInCredits()
    {
        var price = SalePricing.Calculate(new SaleTerms(60000m, 90m, 500m, 0.08m, 1000m,
            0m, 0m, 500m, 9130m));
        Assert.Equal(50000m, price.BalancePayable);
        Assert.Equal(Cash, Cash.Normalize(price.BalancePayable));
        Assert.Throws<ArgumentException>(() =>
            (Cash with { ReceivedAmount = 40370m }).Normalize(price.BalancePayable));
    }

    [Fact]
    public void SettlementReviewRequiresApprovalAndTheHandlingAgentWithoutChangingTheSale()
    {
        var reservation = ReservationTests.Create();
        var sale = SalesOrder.Open(reservation, Guid.NewGuid(), out _);
        var agentId = Guid.NewGuid();
        var now = reservation.CreatedAt.AddHours(1);
        var evidence = Cash with { ReceivedAmount = sale.BalancePayable };

        Assert.Throws<InvalidOperationException>(() => sale.ReviewSettlement(evidence, agentId));
        sale.Claim(Guid.NewGuid(), agentId, agentId, now);
        Assert.Throws<InvalidOperationException>(() => sale.ReviewSettlement(evidence, agentId));
        var settings = new Dealership.Domain.Vehicles.PurchaseSettings(500m, 0.08m, Guid.NewGuid(), now);
        sale.Submit(Guid.NewGuid(), settings, "VPO-2026-000001", agentId, now);
        var before = SaleSnapshot.Of(sale);

        Assert.Throws<InvalidOperationException>(() => sale.ReviewSettlement(evidence, Guid.NewGuid()));
        Assert.Throws<InvalidOperationException>(() => sale.ReviewSettlement(evidence, Guid.Empty));
        Assert.Equal(evidence, sale.ReviewSettlement(evidence, agentId));
        Assert.Equal(before, SaleSnapshot.Of(sale));
    }

    [Fact]
    public void ZeroBalanceRequiresExplicitConfirmationAndNoPaymentFacts()
    {
        Assert.Equal(Zero, Zero.Normalize(0m));
        SalePaymentEvidence[] invalid =
        [
            Zero with { ZeroBalanceConfirmed = false },
            Zero with { Method = SalePaymentMethod.Cash },
            Zero with { ReceivedAmount = 0.01m },
            Zero with { FinancingAmount = 0.01m },
            Zero with { PaymentReference = "TRX-1" },
            Zero with { PaymentDate = Cash.PaymentDate },
            Zero with { Bank = "Maybank" },
            Zero with { LouReference = "LOU-1" },
            Zero with { LouVerified = true }
        ];

        foreach (var evidence in invalid)
        {
            Assert.Throws<ArgumentException>(() => evidence.Normalize(0m));
        }

        Assert.Throws<ArgumentException>(() => Zero.Normalize(0.01m));
        Assert.Throws<ArgumentException>(() => (Cash with { ZeroBalanceConfirmed = true }).Normalize(50000m));
    }

    [Fact]
    public void CashRequiresItsReferenceAndDateWithoutUnrelatedFinancingEvidence()
    {
        SalePaymentEvidence[] invalid =
        [
            Cash with { PaymentReference = " " },
            Cash with { PaymentDate = null },
            Cash with { FinancingAmount = 1m },
            Cash with { Bank = "Maybank" },
            Cash with { LouReference = "LOU-1" },
            Cash with { LouVerified = true },
            Cash with { Method = (SalePaymentMethod)int.MaxValue }
        ];

        foreach (var evidence in invalid)
        {
            Assert.Throws<ArgumentException>(() => evidence.Normalize(50000m));
        }
    }

    [Fact]
    public void HirePurchaseRequiresItsBankAndVerifiedLouWithoutCashOnlyFields()
    {
        SalePaymentEvidence[] invalid =
        [
            HirePurchase with { Bank = " " },
            HirePurchase with { LouReference = null },
            HirePurchase with { LouVerified = false },
            HirePurchase with { PaymentReference = "TRX-1" },
            HirePurchase with { PaymentDate = Cash.PaymentDate },
            HirePurchase with { FinancingAmount = 0m, ReceivedAmount = 50000m }
        ];

        foreach (var evidence in invalid)
        {
            Assert.Throws<ArgumentException>(() => evidence.Normalize(50000m));
        }
    }

    [Fact]
    public void NormalizationTrimsTextAndRetainsExactRecordedFacts()
    {
        Assert.Equal(Cash, (Cash with { PaymentReference = "  TRX-20261102-1842  " }).Normalize(50000m));
        Assert.Equal(HirePurchase, (HirePurchase with
        {
            Bank = "  Maybank  ",
            LouReference = "  LOU-2026-1842  "
        }).Normalize(50000m));
        Assert.Equal(Zero, (Zero with { PaymentReference = " ", Bank = " ", LouReference = " " }).Normalize(0m));
        Assert.Equal(Cash, Cash.Normalize(50000m));
    }

    [Theory]
    [InlineData("-0.01")]
    [InlineData("0.001")]
    [InlineData("10000000000000000")]
    public void EveryPaymentAmountRejectsNegativeFractionalSenOrStorageOverflow(string input)
    {
        var amount = decimal.Parse(input, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Throws<ArgumentOutOfRangeException>(() => Cash.Normalize(amount));
        Assert.Throws<ArgumentOutOfRangeException>(() => (Cash with { ReceivedAmount = amount }).Normalize(50000m));
        Assert.Throws<ArgumentOutOfRangeException>(() => (HirePurchase with { FinancingAmount = amount }).Normalize(50000m));
    }

    [Fact]
    public void MaximumStoredAmountAndOneSenPaymentsAreAcceptedExactly()
    {
        var maximum = Cash with { ReceivedAmount = MoneyStorage.MaximumAmount };
        Assert.Equal(maximum, maximum.Normalize(MoneyStorage.MaximumAmount));
        var oneSen = Cash with { ReceivedAmount = 0.01m };
        Assert.Equal(oneSen, oneSen.Normalize(0.01m));
    }

    [Fact]
    public void EvidenceTextUsesExistingNameAndReasonBoundsAndRejectsControlCharacters()
    {
        var cash = Cash with { PaymentReference = new string('R', SalePaymentEvidence.ReferenceMaximumLength) };
        Assert.Equal(cash, cash.Normalize(50000m));
        var financing = HirePurchase with
        {
            Bank = new string('B', SalePaymentEvidence.BankMaximumLength),
            LouReference = new string('L', SalePaymentEvidence.ReferenceMaximumLength)
        };
        Assert.Equal(financing, financing.Normalize(50000m));
        SalePaymentEvidence[] invalid =
        [
            cash with { PaymentReference = cash.PaymentReference + "R" },
            Cash with { PaymentReference = "TRX\n1" },
            financing with { Bank = financing.Bank + "B" },
            HirePurchase with { Bank = "May\nbank" },
            financing with { LouReference = financing.LouReference + "L" },
            HirePurchase with { LouReference = "LOU\t1" }
        ];

        foreach (var evidence in invalid)
        {
            Assert.Throws<ArgumentException>(() => evidence.Normalize(50000m));
        }
    }
}
