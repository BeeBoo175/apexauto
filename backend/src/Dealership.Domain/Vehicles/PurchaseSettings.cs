namespace Dealership.Domain.Vehicles;

public sealed class PurchaseSettings
{
    public const int SingletonId = 1;
    public const decimal MaximumFee = 5000m;
    public const decimal MaximumAgentDiscountAmount = 5000m;
    public const decimal MaximumSstRate = 0.20m;
    public const int DefaultReservationHoldHours = 48;
    public const int MinimumReservationHoldHours = 1;
    public const int MaximumReservationHoldHours = 168;
    public const decimal DefaultMaxAgentDiscountAmount = 500m;

    private PurchaseSettings()
    {
    }

    public PurchaseSettings(decimal processingFee, decimal sstRate, Guid updatedById, DateTimeOffset updatedAtUtc,
        decimal maxAgentDiscountAmount = DefaultMaxAgentDiscountAmount,
        int reservationHoldHours = DefaultReservationHoldHours)
    {
        ValidateAudit(updatedById, updatedAtUtc);
        ValidateValues(processingFee, sstRate, maxAgentDiscountAmount, reservationHoldHours);
        Id = SingletonId;
        ProcessingFee = processingFee;
        SstRate = sstRate;
        MaxAgentDiscountAmount = maxAgentDiscountAmount;
        ReservationHoldHours = reservationHoldHours;
        UpdatedById = updatedById;
        UpdatedAtUtc = updatedAtUtc;
    }

    public int Id { get; private set; }

    public decimal ProcessingFee { get; private set; }

    public decimal SstRate { get; private set; }

    public decimal MaxAgentDiscountAmount { get; private set; }

    public int ReservationHoldHours { get; private set; }

    public Guid UpdatedById { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public long Version { get; private set; } = 1;

    public void Update(decimal processingFee, decimal sstRate, decimal maxAgentDiscountAmount, int reservationHoldHours,
        Guid updatedById, DateTimeOffset updatedAtUtc)
    {
        ValidateAudit(updatedById, updatedAtUtc);
        ValidateValues(processingFee, sstRate, maxAgentDiscountAmount, reservationHoldHours);
        if (updatedAtUtc < UpdatedAtUtc)
        {
            throw new ArgumentException("Pricing settings cannot be updated before their latest change.");
        }

        ProcessingFee = processingFee;
        SstRate = sstRate;
        MaxAgentDiscountAmount = maxAgentDiscountAmount;
        ReservationHoldHours = reservationHoldHours;
        UpdatedById = updatedById;
        UpdatedAtUtc = updatedAtUtc;
        Version++;
    }

    public static bool ValidValues(decimal processingFee, decimal sstRate, decimal maxAgentDiscountAmount,
        int reservationHoldHours) =>
        processingFee is >= 0m and <= MaximumFee && decimal.Round(processingFee, 2) == processingFee &&
        sstRate is >= 0m and <= MaximumSstRate &&
        maxAgentDiscountAmount is >= 0m and <= MaximumAgentDiscountAmount &&
        decimal.Round(maxAgentDiscountAmount, 2) == maxAgentDiscountAmount &&
        reservationHoldHours is >= MinimumReservationHoldHours and <= MaximumReservationHoldHours;

    private static void ValidateAudit(Guid updatedById, DateTimeOffset updatedAtUtc)
    {
        if (updatedById == Guid.Empty || updatedAtUtc.Offset != TimeSpan.Zero ||
            updatedAtUtc == DateTimeOffset.MinValue || updatedAtUtc == DateTimeOffset.MaxValue)
        {
            throw new ArgumentException("Pricing settings require an updater and finite UTC timestamp.");
        }
    }

    private static void ValidateValues(decimal processingFee, decimal sstRate, decimal maxAgentDiscountAmount,
        int reservationHoldHours)
    {
        if (!ValidValues(processingFee, sstRate, maxAgentDiscountAmount, reservationHoldHours))
        {
            throw new ArgumentOutOfRangeException(nameof(processingFee), "Pricing settings are outside the approved bounds.");
        }
    }
}
