using Dealership.Domain.Common;

namespace Dealership.Domain.Sales;

public sealed class HandoverNumberCounter : YearlyNumberCounter
{
    private HandoverNumberCounter()
    {
    }

    public HandoverNumberCounter(int year) : base(year)
    {
    }

    protected override string Prefix => "VHN";
}
