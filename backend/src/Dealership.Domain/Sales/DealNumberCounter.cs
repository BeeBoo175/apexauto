using Dealership.Domain.Common;

namespace Dealership.Domain.Sales;

// Numbers deals per year as VPO-YYYY-NNNNNN.
public sealed class DealNumberCounter : YearlyNumberCounter
{
    private DealNumberCounter()
    {
    }

    public DealNumberCounter(int year) : base(year)
    {
    }

    protected override string Prefix => "VPO";
}
