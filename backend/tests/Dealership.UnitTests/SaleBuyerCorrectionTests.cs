using Dealership.Domain.Sales;

namespace Dealership.UnitTests;

public sealed class SaleBuyerCorrectionTests
{
    [Fact]
    public void CorrectionNormalizesOnlyBuyerNameAndAddressAndRequiresAChangeAndReason()
    {
        var original = new SaleSettlementReview("Nur Aina Abdulah", "940215101234", "18 Jalan Melati",
            new(Guid.NewGuid(), "BPK 4387", "WBA12345678901234", "BMW", "320i", "Sport", 2020, 42000));
        var correction = new SaleBuyerCorrection(" Nur Aina Abdullah ", " 18 Jalan Melati ", " Correct buyer spelling ");
        var changed = correction.Apply(original);
        Assert.Equal("Nur Aina Abdullah", changed.BuyerName);
        Assert.Equal(original.BuyerAddress, changed.BuyerAddress);
        Assert.Equal(original.BuyerNric, changed.BuyerNric);
        Assert.Equal(original.Vehicle, changed.Vehicle);
        Assert.Equal("Correct buyer spelling", correction.Normalize().Reason);
        Assert.Throws<ArgumentException>(() => (correction with { Reason = " " }).Apply(original));
        Assert.Throws<ArgumentException>(() => (correction with { BuyerName = original.BuyerName }).Apply(original));
        Assert.Throws<ArgumentException>(() => (correction with { BuyerAddress = "18\nJalan Melati" }).Apply(original));
        Assert.Throws<ArgumentException>(() => (correction with { BuyerName = new string('a', 201) }).Apply(original));
        Assert.Throws<ArgumentException>(() => (correction with { BuyerAddress = new string('a', 1001) }).Apply(original));
    }
}
