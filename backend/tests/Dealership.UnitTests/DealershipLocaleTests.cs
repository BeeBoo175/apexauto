using Dealership.Domain.Common;
using Xunit;

namespace Dealership.UnitTests;

public class DealershipLocaleTests
{
    [Fact]
    public void SharedLocaleUsesMalaysianCurrencyAndTimeZone()
    {
        Assert.Equal("MYR", DealershipLocale.DefaultCurrency);
        Assert.Equal("Asia/Kuala_Lumpur", DealershipLocale.MalaysiaTimeZoneId);
    }

    [Fact]
    public void MoneyAdditionAndSubtractionShouldPreserveSenPrecision()
    {
        var price = new Money(45000.50m);
        var fee = new Money(500.00m);

        var total = price + fee;

        Assert.Equal(45500.50m, total.Amount);
        Assert.Equal("MYR", total.Currency);
    }
}
