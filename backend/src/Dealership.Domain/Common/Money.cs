namespace Dealership.Domain.Common;

public readonly record struct Money
{
    public decimal Amount { get; }

    public string Currency { get; }

    public Money(decimal amount, string currency = DealershipConstants.DefaultCurrency)
    {
        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        Currency = currency;
    }

    public static Money Zero => new(0.00m);

    public static Money From(decimal amount)
    {
        return new Money(amount);
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
