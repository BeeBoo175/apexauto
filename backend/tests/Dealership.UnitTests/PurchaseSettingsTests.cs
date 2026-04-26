using Dealership.Domain.Vehicles;

namespace Dealership.UnitTests;

public sealed class PurchaseSettingsTests
{
    [Fact]
    public void NewSettingsDefaultTheAcceptedDiscountLimitAndHoldDuration()
    {
        var settings = new PurchaseSettings(500m, 0.08m, Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.Equal(500m, settings.MaxAgentDiscountAmount);
        Assert.Equal(48, settings.ReservationHoldHours);
        Assert.Equal(1, settings.Version);
    }

    [Theory]
    [InlineData(-0.01, 0.08, 500, 48)]
    [InlineData(5000.01, 0.08, 500, 48)]
    [InlineData(500, -0.01, 500, 48)]
    [InlineData(500, 0.21, 500, 48)]
    [InlineData(500, 0.08, -0.01, 48)]
    [InlineData(500, 0.08, 5000.01, 48)]
    [InlineData(500.001, 0.08, 500, 48)]
    [InlineData(500, 0.08, 500.001, 48)]
    [InlineData(500, 0.08, 500, 0)]
    [InlineData(500, 0.08, 500, 169)]
    public void ValuesOutsideTheApprovedBoundsAreRejected(double fee, double rate, double discount, int hours)
    {
        Assert.False(PurchaseSettings.ValidValues((decimal)fee, (decimal)rate, (decimal)discount, hours));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PurchaseSettings((decimal)fee, (decimal)rate,
            Guid.NewGuid(), DateTimeOffset.UtcNow, (decimal)discount, hours));
    }

    [Theory]
    [InlineData(0, 0, 0, 1)]
    [InlineData(5000, 0.20, 5000, 168)]
    public void BoundaryValuesAreAccepted(double fee, double rate, double discount, int hours)
    {
        Assert.True(PurchaseSettings.ValidValues((decimal)fee, (decimal)rate, (decimal)discount, hours));
    }

    [Fact]
    public void UpdateReplacesValuesRecordsTheUpdaterAndAdvancesTheVersion()
    {
        var created = DateTimeOffset.UtcNow;
        var updater = Guid.NewGuid();
        var settings = new PurchaseSettings(500m, 0.08m, Guid.NewGuid(), created);

        settings.Update(600m, 0.06m, 750m, 72, updater, created.AddMinutes(1));

        Assert.Equal((600m, 0.06m, 750m, 72), (settings.ProcessingFee, settings.SstRate,
            settings.MaxAgentDiscountAmount, settings.ReservationHoldHours));
        Assert.Equal(updater, settings.UpdatedById);
        Assert.Equal(created.AddMinutes(1), settings.UpdatedAtUtc);
        Assert.Equal(2, settings.Version);
    }

    [Fact]
    public void UpdateRejectsInvalidValuesAndEarlierTimestampsWithoutChangingState()
    {
        var created = DateTimeOffset.UtcNow;
        var settings = new PurchaseSettings(500m, 0.08m, Guid.NewGuid(), created);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            settings.Update(500m, 0.08m, 500m, 0, Guid.NewGuid(), created));
        Assert.Throws<ArgumentException>(() =>
            settings.Update(500m, 0.08m, 500m, 48, Guid.NewGuid(), created.AddTicks(-1)));
        Assert.Throws<ArgumentException>(() =>
            settings.Update(500m, 0.08m, 500m, 48, Guid.Empty, created));

        Assert.Equal(1, settings.Version);
        Assert.Equal(48, settings.ReservationHoldHours);
    }
}
