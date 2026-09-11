using Dealership.Domain.Customers;
using Dealership.Domain.Vehicles;

namespace Dealership.Domain.Sales;

// The agent submits the facts they reviewed; identity and vehicle facts must still match under the sale locks.
public sealed record SaleSettlementReview(string BuyerName, string BuyerNric, string BuyerAddress,
    SaleReviewedVehicle Vehicle)
{
    // Callers refuse a customer whose details were removed first, so a review is never built from NULL columns.
    public static SaleSettlementReview From(CustomerProfile profile, SaleReviewedVehicle vehicle)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.LegalName is not { } name || profile.Nric is not { } nric ||
            profile.ResidentialAddress is not { } address)
        {
            throw new InvalidOperationException("The customer's recorded details were removed.");
        }

        return new SaleSettlementReview(name, nric, address, vehicle);
    }

    public SaleSettlementReview Normalize()
    {
        if (!CustomerProfile.IsValidNric(BuyerNric) || Vehicle is null)
        {
            throw new ArgumentException("Review the reservation customer's NRIC and vehicle.");
        }

        return this with
        {
            BuyerName = RequiredText(BuyerName, CustomerProfile.LegalNameMaximumLength),
            BuyerAddress = RequiredText(BuyerAddress, CustomerProfile.AddressMaximumLength)
        };
    }

    private static string RequiredText(string? input, int maximum)
    {
        var value = input?.Trim();
        if (string.IsNullOrEmpty(value) || value.Length > maximum || value.Any(char.IsControl))
        {
            throw new ArgumentException("Reviewed buyer details must be bounded text without control characters.");
        }

        return value;
    }
}

public sealed record SaleReviewedVehicle(Guid Id, string RegistrationPlate, string Vin, string Make,
    string Model, string Trim, int ManufactureYear, int MileageKilometres)
{
    public static SaleReviewedVehicle Of(Vehicle vehicle) => new(vehicle.Id, vehicle.RegistrationPlate,
        vehicle.Vin, vehicle.Make, vehicle.Model, vehicle.Trim, vehicle.ManufactureYear, vehicle.MileageKilometres);
}
