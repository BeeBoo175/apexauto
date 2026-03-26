namespace Dealership.Domain.Vehicles;

public sealed class PromoCode
{
    public const int CodeMaximumLength = 50;

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

        PurchasePricing.CalculatePrice(9999999999999999.99m, 0m, 0m, 0m, discount);
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

    public bool CanApply(decimal vehiclePrice, DateTimeOffset now) =>
        IsActive && now < ExpiresAtUtc && Discount <= vehiclePrice;

    public static bool ValidCode(string? code) => !string.IsNullOrWhiteSpace(code) &&
        code.Trim().Length <= CodeMaximumLength && !code.Contains('\0');
}
