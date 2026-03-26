namespace Dealership.Domain.Vehicles;

public enum VehicleTransmission
{
    Automatic,
    Manual
}

public enum VehicleFuel
{
    Petrol,
    Diesel,
    Hybrid,
    PlugInHybrid,
    Electric
}

public sealed class ListingRevision
{
    private ListingRevision()
    {
    }

    public ListingRevision(Guid id, Guid vehicleId, Guid branchId, Guid actorId, long version,
        decimal vehiclePrice, decimal roadTax, VehicleTransmission transmission, VehicleFuel fuel,
        string description, DateTimeOffset recordedAt)
    {
        if (id == Guid.Empty || vehicleId == Guid.Empty || branchId == Guid.Empty || actorId == Guid.Empty ||
            version < 1 || !Enum.IsDefined(transmission) || !Enum.IsDefined(fuel))
        {
            throw new ArgumentException("A listing revision requires valid identities, version and specifications.");
        }

        if (string.IsNullOrWhiteSpace(description) || description.Trim().Length > 2000 || description.Contains('\0'))
        {
            throw new ArgumentException("Provide a public description of at most 2000 characters.");
        }

        PurchasePricing.CalculatePrice(vehiclePrice, 0m, roadTax, 0m);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(vehiclePrice, 9999999999999999.99m);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(roadTax, 9999999999999999.99m);
        Id = id;
        VehicleId = vehicleId;
        BranchId = branchId;
        ActorId = actorId;
        Version = version;
        VehiclePrice = vehiclePrice;
        RoadTax = roadTax;
        Transmission = transmission;
        Fuel = fuel;
        Description = description.Trim();
        RecordedAt = recordedAt;
    }

    public Guid Id { get; private set; }
    public Guid VehicleId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid ActorId { get; private set; }
    public long Version { get; private set; }
    public decimal VehiclePrice { get; private set; }
    public decimal RoadTax { get; private set; }
    public VehicleTransmission Transmission { get; private set; }
    public VehicleFuel Fuel { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; private set; }
}
