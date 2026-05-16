using Dealership.Domain.Customers;

namespace Dealership.Domain.Sales;

// Reissuing a document can fix spelling without changing its contractual or lifecycle evidence.
public sealed record SaleBuyerCorrection(string BuyerName, string BuyerAddress, string Reason)
{
    public SaleBuyerCorrection Normalize() => new(
        Required(BuyerName, CustomerProfile.LegalNameMaximumLength),
        Required(BuyerAddress, CustomerProfile.AddressMaximumLength), Required(Reason, 1000));

    public SaleSettlementReview Apply(SaleSettlementReview original)
    {
        var correction = Normalize();
        if (original.BuyerName == correction.BuyerName && original.BuyerAddress == correction.BuyerAddress)
        {
            throw new ArgumentException("A correction must change the buyer name or address.");
        }

        return original with { BuyerName = correction.BuyerName, BuyerAddress = correction.BuyerAddress };
    }

    private static string Required(string? input, int maximum)
    {
        var value = input?.Trim();
        if (string.IsNullOrEmpty(value) || value.Length > maximum || value.Any(char.IsControl))
        {
            throw new ArgumentException("Provide bounded buyer details and a correction reason without control characters.");
        }

        return value;
    }
}
