using System.Numerics;

namespace Dealership.Domain.Common;

public readonly record struct Money
{
    public decimal Amount { get; }

    public string Currency { get; }

    public Money(decimal amount, string currency = DealershipLocale.DefaultCurrency)
    {
        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        Currency = currency;
    }

    public static Money Zero => new(0.00m);

    public static Money From(decimal amount)
    {
        return new Money(amount);
    }

    public static Money FromProduct(decimal amount, decimal multiplier)
    {
        var (amountUnits, amountScale) = DecimalParts(amount);
        var (multiplierUnits, multiplierScale) = DecimalParts(multiplier);
        var product = amountUnits * multiplierUnits;
        var divisor = BigInteger.Pow(10, amountScale + multiplierScale);
        var minorUnits = BigInteger.DivRem(product * 100, divisor, out var remainder);

        // Round once, after multiplication, so decimal precision cannot move a value onto a half-sen tie.
        if (BigInteger.Abs(remainder) * 2 >= divisor)
        {
            minorUnits += product.Sign;
        }

        var whole = BigInteger.DivRem(minorUnits, 100, out var sen);
        return From((decimal)whole + (decimal)sen / 100m);
    }

    private static (BigInteger Units, int Scale) DecimalParts(decimal value)
    {
        var bits = decimal.GetBits(value);
        var units = (BigInteger)(uint)bits[0] | (BigInteger)(uint)bits[1] << 32 | (BigInteger)(uint)bits[2] << 64;
        if (bits[3] < 0)
        {
            units = -units;
        }

        return (units, (bits[3] >> 16) & 0xff);
    }

    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator -(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount - right.Amount, left.Currency);
    }

    public static Money operator *(Money left, decimal multiplier)
    {
        return new Money(left.Amount * multiplier, left.Currency);
    }

    private static void EnsureSameCurrency(Money left, Money right)
    {
        if (!string.Equals(left.Currency, right.Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Cannot operate on mismatched currencies: {left.Currency} and {right.Currency}.");
        }
    }

    public override string ToString()
    {
        return $"{Currency} {Amount:N2}";
    }
}
