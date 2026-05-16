using Dealership.Domain.Common;

namespace Dealership.Domain.Sales;

public sealed class ReceiptNumberCounter : YearlyNumberCounter
{
    private ReceiptNumberCounter()
    {
    }

    public ReceiptNumberCounter(int year) : base(year)
    {
    }

    protected override string Prefix => "VRC";
}
