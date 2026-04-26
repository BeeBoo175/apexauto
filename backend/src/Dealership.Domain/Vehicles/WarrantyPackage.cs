using Dealership.Domain.Inspections;

namespace Dealership.Domain.Vehicles;

public sealed class WarrantyPackage
{
    public const int NameMaximumLength = 100;
    public const int MaximumDurationMonths = 120;
    public const int MaximumMileageKm = 500000;
    public const decimal MaximumPrice = 20000m;

    private WarrantyPackage()
    {
    }

    public WarrantyPackage(Guid id, string name, int durationMonths, int mileageCapKm, decimal price,
        InspectionGrade minimumGrade, bool isActive)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A warranty package requires an identity.");
        }

        Id = id;
        Apply(name, durationMonths, mileageCapKm, price, minimumGrade, isActive);
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public int DurationMonths { get; private set; }

    public int MileageCapKm { get; private set; }

    public decimal Price { get; private set; }

    public InspectionGrade MinimumGrade { get; private set; }

    public bool IsActive { get; private set; }

    public long Version { get; private set; } = 1;

    public void Update(string name, int durationMonths, int mileageCapKm, decimal price, InspectionGrade minimumGrade,
        bool isActive)
    {
        Apply(name, durationMonths, mileageCapKm, price, minimumGrade, isActive);
        Version++;
    }

    // Grades order A (best) to C; a rejected vehicle has no grade and is never eligible.
    public bool IsEligibleFor(InspectionGrade? vehicleGrade) =>
        IsActive && vehicleGrade is { } grade && grade <= MinimumGrade;

    public static bool ValidName(string? name) => !string.IsNullOrWhiteSpace(name) &&
        name.Trim().Length <= NameMaximumLength && !name.Any(char.IsControl);

    public static bool ValidTerms(int durationMonths, int mileageCapKm, decimal price, InspectionGrade minimumGrade) =>
        durationMonths is >= 1 and <= MaximumDurationMonths && mileageCapKm is >= 1 and <= MaximumMileageKm &&
        price is >= 0m and <= MaximumPrice && decimal.Round(price, 2) == price && Enum.IsDefined(minimumGrade);

    private void Apply(string name, int durationMonths, int mileageCapKm, decimal price, InspectionGrade minimumGrade,
        bool isActive)
    {
        if (!ValidName(name))
        {
            throw new ArgumentException("A warranty package requires a name of at most 100 characters.");
        }

        if (!ValidTerms(durationMonths, mileageCapKm, price, minimumGrade))
        {
            throw new ArgumentOutOfRangeException(nameof(price), "Warranty terms are outside the approved bounds.");
        }

        Name = name.Trim();
        DurationMonths = durationMonths;
        MileageCapKm = mileageCapKm;
        Price = price;
        MinimumGrade = minimumGrade;
        IsActive = isActive;
    }
}
