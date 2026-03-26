using Dealership.Domain.Vehicles;

namespace Dealership.UnitTests;

public sealed class PromoCodeTests
{
    [Fact]
    public void CodePreservesCaseWhileTrimmingAndExpiresAtEquality()
    {
        var expiry = DateTimeOffset.UtcNow.AddDays(1);
        var promo = new PromoCode(Guid.NewGuid(), "\u00a0APEX500\t", 500m, expiry, true);

        Assert.Equal("APEX500", promo.Code);
        Assert.True(promo.CanApply(500m, expiry.AddTicks(-1)));
        Assert.False(promo.CanApply(500m, expiry));
        Assert.False(promo.CanApply(500m, expiry.AddTicks(1)));
        Assert.False(promo.CanApply(499.99m, expiry.AddHours(-1)));
        Assert.False(new PromoCode(Guid.NewGuid(), "APEX500", 500m, expiry, false)
            .CanApply(60000m, expiry.AddHours(-1)));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \u00a0")]
    [InlineData("APEX\0")]
    public void MissingOrInvalidCodeCannotBePersisted(string code)
    {
        Assert.Throws<ArgumentException>(() => new PromoCode(Guid.NewGuid(), code, 500m, DateTimeOffset.UtcNow, true));
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(0.001)]
    public void PromoDiscountRequiresNonnegativeExactMinorUnits(decimal discount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PromoCode(Guid.NewGuid(), "APEX500", discount, DateTimeOffset.UtcNow, true));
    }

    [Fact]
    public void SettingsReuseCanonicalMoneyAndRateValidation()
    {
        var actor = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        Assert.Throws<ArgumentOutOfRangeException>(() => new PurchaseSettings(0.001m, 0.08m, actor, now));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PurchaseSettings(-1m, 0.08m, actor, now));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PurchaseSettings(500m, 1.01m, actor, now));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PurchaseSettings(500m, -0.01m, actor, now));
        Assert.Throws<ArgumentException>(() => new PurchaseSettings(500m, 0.08m, Guid.Empty, now));
        var settings = new PurchaseSettings(0m, 1m, actor, now);
        Assert.Equal(PurchaseSettings.SingletonId, settings.Id);
        Assert.Equal(1, settings.Version);
    }
}
