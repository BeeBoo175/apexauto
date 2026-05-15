namespace Dealership.Domain.Common;

public static class MoneyStorage
{
    // The decimal(18,2) column limit; individual business policies may impose narrower bounds.
    public const decimal MaximumAmount = 9999999999999999.99m;
}
