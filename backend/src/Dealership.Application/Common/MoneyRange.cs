namespace Dealership.Application.Common;

public static class MoneyRange
{
    public static bool IsValid(decimal? minimum, decimal? maximum)
    {
        return IsAmount(minimum) && IsAmount(maximum) &&
            (minimum is null || maximum is null || minimum <= maximum);
    }

    private static bool IsAmount(decimal? amount)
    {
        return amount is null || amount >= 0 && amount <= 9999999999999999.99m &&
            decimal.Round(amount.Value, 2) == amount;
    }
}
