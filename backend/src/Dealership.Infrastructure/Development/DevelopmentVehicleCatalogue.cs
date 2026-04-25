using Dealership.Domain.Intake;
using Dealership.Domain.Vehicles;

namespace Dealership.Infrastructure.Development;

public sealed record DevelopmentVehicleDetails(string MediaPrefix, decimal RoadTax, VehicleFuel Fuel, string Description)
{
    public string PhotoFile(PhotoAngle angle)
    {
        var extension = MediaPrefix switch
        {
            "ativa" when angle is PhotoAngle.Driver or PhotoAngle.Passenger => "png",
            "alza" when angle != PhotoAngle.Front => "png",
            _ => "jpg"
        };
        return $"{MediaPrefix}-{angle.ToString().ToLowerInvariant()}.{extension}";
    }

    public string ContentType(PhotoAngle angle) => PhotoFile(angle).EndsWith(".png", StringComparison.Ordinal)
        ? "image/png" : "image/jpeg";
}

public static class DevelopmentVehicleCatalogue
{
    public static DevelopmentVehicleDetails For(string make, string model, string? trim = null) => (make, model) switch
    {
        ("Perodua", "Myvi") => new("myvi", 90m, VehicleFuel.Petrol,
            "Compact five-door hatchback with a 1.5-litre petrol engine and automatic transmission."),
        ("Perodua", "Ativa") => new("ativa", 20m, VehicleFuel.Petrol,
            "Compact crossover with a 1.0-litre turbocharged petrol engine and D-CVT transmission."),
        ("Proton", "Saga") => new("saga", 70m, VehicleFuel.Petrol,
            "Practical four-door saloon with a 1.3-litre petrol engine and four-speed automatic transmission."),
        ("Honda", "City") => new("city", 90m, VehicleFuel.Petrol,
            "Four-door saloon with a 1.5-litre i-VTEC petrol engine and continuously variable transmission."),
        ("Toyota", "Vios") => new("vios", 90m, VehicleFuel.Petrol,
            "Four-door saloon with a 1.5-litre Dual VVT-i petrol engine and continuously variable transmission."),
        ("Mazda", "Mazda 3") => new("mazda3", 379m, VehicleFuel.Petrol,
            "Five-door liftback with a 2.0-litre Skyactiv-G petrol engine and six-speed automatic transmission."),
        ("BYD", "Dolphin") => new("dolphin", trim == "Premium Extended" ? 160m : 20m, VehicleFuel.Electric,
            "Battery-electric five-door hatchback with front-wheel drive and a single-speed transmission."),
        ("Perodua", "Alza") => new("alza", 90m, VehicleFuel.Petrol,
            "Seven-seat MPV with a 1.5-litre petrol engine and D-CVT transmission."),
        _ => throw new InvalidOperationException("The development vehicle requires matching specifications and Wikimedia media.")
    };
}
