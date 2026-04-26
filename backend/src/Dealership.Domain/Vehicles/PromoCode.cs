namespace Dealership.Domain.Vehicles;

public sealed class PromoCode
{
    public const int CodeMaximumLength = 50;
    public const decimal MaximumDiscount = 100000m;

    private PromoCode()
    {
    }

    public PromoCode(Guid id, string code, decimal discount, DateTimeOffset expiresAtUtc, bool isActive)
    {
        if (id == Guid.Empty || !ValidCode(code) || expiresAtUtc.Offset != TimeSpan.Zero ||
            expiresAtUtc == DateTimeOffset.MinValue || expiresAtUtc == DateTimeOffset.MaxValue)
        {
            throw new ArgumentException("A promo requires an identity, code and finite UTC expiry.");
        }

        ValidateDiscount(discount);
        Id = id;
        Code = code.Trim();
        Discount = discount;
        ExpiresAtUtc = expiresAtUtc;
        IsActive = isActive;
    }

    public Guid Id { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public decimal Discount { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public bool IsActive { get; private set; }

    public long Version { get; private set; } = 1;

    // The code is the customer-facing identity and stays fixed; terms change under optimistic concurrency.
    public void Update(decimal discount, DateTimeOffset expiresAtUtc, bool isActive)
    {
        if (expiresAtUtc.Offset != TimeSpan.Zero || expiresAtUtc == DateTimeOffset.MinValue ||
            expiresAtUtc == DateTimeOffset.MaxValue)
        {
            throw new ArgumentException("A promo requires a finite UTC expiry.");
        }

        ValidateDiscount(discount);
        Discount = discount;
        ExpiresAtUtc = expiresAtUtc;
        IsActive = isActive;
        Version++;
    }

    public static bool ValidDiscount(decimal discount) =>
        discount is > 0m and <= MaximumDiscount && decimal.Round(discount, 2) == discount;

    public bool CanApply(decimal vehiclePrice, DateTimeOffset now) =>
        IsActive && now < ExpiresAtUtc && Discount <= vehiclePrice;

    private static void ValidateDiscount(decimal discount)
    {
        if (!ValidDiscount(discount))
        {
            throw new ArgumentOutOfRangeException(nameof(discount), "A promo discount must be a positive exact amount within the approved limit.");
        }
    }

    public static bool ValidCode(string? code) => !string.IsNullOrWhiteSpace(code) &&
        code.Trim().Length <= CodeMaximumLength && !code.Contains('\0');
}
