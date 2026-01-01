namespace Dealership.Domain.Intake;

public sealed record ValuedVehicle(string Make, string Model, string Trim, int ManufactureYear,
    int MileageKilometres, decimal Minimum, decimal Maximum, string Currency, int ValuationYear);

public sealed class SellerValuation
{
    private SellerValuation()
    {
    }

    public SellerValuation(Guid appointmentId, Guid ownerId, ValuedVehicle vehicle)
    {
        if (appointmentId == Guid.Empty || ownerId == Guid.Empty || string.IsNullOrWhiteSpace(vehicle.Make) ||
            string.IsNullOrWhiteSpace(vehicle.Model) || string.IsNullOrWhiteSpace(vehicle.Trim) ||
            vehicle.ManufactureYear < 1 || vehicle.ManufactureYear > vehicle.ValuationYear || vehicle.MileageKilometres < 0 ||
            vehicle.Minimum < 0 || vehicle.Maximum < vehicle.Minimum || vehicle.Currency != "MYR")
        {
            throw new ArgumentException("A valuation snapshot requires a valid owned vehicle and confirmed quote.");
        }

        AppointmentId = appointmentId;
        OwnerId = ownerId;
        Make = vehicle.Make;
        Model = vehicle.Model;
        Trim = vehicle.Trim;
        ManufactureYear = vehicle.ManufactureYear;
        MileageKilometres = vehicle.MileageKilometres;
        Minimum = vehicle.Minimum;
        Maximum = vehicle.Maximum;
        Currency = vehicle.Currency;
        ValuationYear = vehicle.ValuationYear;
    }

    public Guid AppointmentId { get; private set; }
    public Guid OwnerId { get; private set; }
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
