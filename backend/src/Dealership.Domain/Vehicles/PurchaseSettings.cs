namespace Dealership.Domain.Vehicles;

public sealed class PurchaseSettings
{
    public const int SingletonId = 1;

    private PurchaseSettings()
    {
    }

    public PurchaseSettings(decimal processingFee, decimal sstRate, Guid updatedById, DateTimeOffset updatedAtUtc)
    {
        if (updatedById == Guid.Empty || updatedAtUtc.Offset != TimeSpan.Zero ||
            updatedAtUtc == DateTimeOffset.MinValue || updatedAtUtc == DateTimeOffset.MaxValue)
        {
            throw new ArgumentException("Pricing settings require an updater and finite UTC timestamp.");
        }

        PurchasePricing.CalculatePrice(1m, processingFee, 0m, sstRate);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(processingFee, 9999999999999999.99m);
        Id = SingletonId;
        ProcessingFee = processingFee;
        SstRate = sstRate;
        UpdatedById = updatedById;
        UpdatedAtUtc = updatedAtUtc;
    }

    public int Id { get; private set; }

    public decimal ProcessingFee { get; private set; }

    public decimal SstRate { get; private set; }

    public Guid UpdatedById { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public long Version { get; private set; } = 1;
}
