namespace Dealership.Domain.Intake;

public sealed class SellerValuation
{
    private SellerValuation()
    {
    }

    public SellerValuation(string make, string model, string trim, int manufactureYear,
        int mileageKilometres, decimal minimum, decimal maximum, string currency, int valuationYear)
    {
        if (string.IsNullOrWhiteSpace(make) || string.IsNullOrWhiteSpace(model) || string.IsNullOrWhiteSpace(trim) ||
            manufactureYear < 1 || manufactureYear > valuationYear || mileageKilometres < 0 ||
            minimum < 0 || maximum < minimum || currency != "MYR")
        {
            throw new ArgumentException("A valuation snapshot requires a valid vehicle and confirmed quote.");
        }

        Make = make;
        Model = model;
        Trim = trim;
        ManufactureYear = manufactureYear;
        MileageKilometres = mileageKilometres;
        Minimum = minimum;
        Maximum = maximum;
        Currency = currency;
        ValuationYear = valuationYear;
    }

    public string Make { get; private set; } = string.Empty;
    public string Model { get; private set; } = string.Empty;
    public string Trim { get; private set; } = string.Empty;
    public int ManufactureYear { get; private set; }
    public int MileageKilometres { get; private set; }
    public decimal Minimum { get; private set; }
    public decimal Maximum { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public int ValuationYear { get; private set; }
}
