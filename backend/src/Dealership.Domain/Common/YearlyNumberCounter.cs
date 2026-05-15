namespace Dealership.Domain.Common;

// One row per calendar year; the store increments it under a row lock so numbers never repeat or skip.
// Each document type keeps its own table and sequence and supplies its prefix.
public abstract class YearlyNumberCounter
{
    protected YearlyNumberCounter()
    {
    }

    protected YearlyNumberCounter(int year)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(year, 2000);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(year, 9999);
        Year = year;
    }

    public int Year { get; private set; }

    public int LastValue { get; private set; }

    protected abstract string Prefix { get; }

    public string Next()
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(LastValue, 999999);
        LastValue++;
        return $"{Prefix}-{Year:D4}-{LastValue:D6}";
    }
}
