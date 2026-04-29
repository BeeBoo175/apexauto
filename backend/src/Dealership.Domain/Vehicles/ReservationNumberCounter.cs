namespace Dealership.Domain.Vehicles;

// Numbers reservations per year as VRS-YYYY-NNNNNN, the way deals are numbered VPO-YYYY-NNNNNN.
public sealed class ReservationNumberCounter
{
    private ReservationNumberCounter()
    {
    }

    public ReservationNumberCounter(int year)
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
        return $"VRS-{Year:D4}-{LastValue:D6}";
    }
}
