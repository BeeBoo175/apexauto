namespace Dealership.Domain.Sales;

// One row per calendar year; the store increments it under a row lock so numbers never repeat or skip.
public sealed class DealNumberCounter
{
    private DealNumberCounter()
    {
    }

    public DealNumberCounter(int year)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(year, 2000);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(year, 9999);
        Year = year;
    }

    public int Year { get; private set; }

    public int LastValue { get; private set; }

    public string Next()
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(LastValue, 999999);
        LastValue++;
        return $"VPO-{Year:D4}-{LastValue:D6}";
    }
}
