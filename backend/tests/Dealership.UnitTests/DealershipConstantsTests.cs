using Dealership.Domain.Common;
using Xunit;

namespace Dealership.UnitTests;

public class DealershipConstantsTests
{
    [Fact]
    public void ConstantsShouldReflectMalaysianStatutoryStandards()
    {
        Assert.Equal(210, DealershipConstants.StandardInspectionPointCount);
        Assert.Equal(0.08m, DealershipConstants.StandardSstRate);
        Assert.Equal("MYR", DealershipConstants.DefaultCurrency);
        Assert.Equal("Asia/Kuala_Lumpur", DealershipConstants.MalaysiaTimeZoneId);
        Assert.Equal(500.00m, DealershipConstants.ReservationBookingDeposit);
        Assert.Equal(48, DealershipConstants.DefaultReservationHoldHours);
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
