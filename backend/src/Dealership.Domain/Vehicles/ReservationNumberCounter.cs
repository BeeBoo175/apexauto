using Dealership.Domain.Common;

namespace Dealership.Domain.Vehicles;

// Numbers reservations per year as VRS-YYYY-NNNNNN, the way deals are numbered VPO-YYYY-NNNNNN.
public sealed class ReservationNumberCounter : YearlyNumberCounter
{
    private ReservationNumberCounter()
    {
    }

    public ReservationNumberCounter(int year) : base(year)
    {
    }

    protected override string Prefix => "VRS";
}
