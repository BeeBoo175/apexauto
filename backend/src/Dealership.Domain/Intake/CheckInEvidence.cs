namespace Dealership.Domain.Intake;

public sealed record CheckInEvidence(string? Vin, int MileageKilometres, bool CustomerPresent,
    bool IdentityConfirmed, bool OwnershipConfirmed, bool VehicleConfirmed, string? Notes)
{
    public const int NotesMaximumLength = 1000;

    public CheckInEvidence Normalize()
    {
        var vin = Vin?.Trim().ToUpperInvariant();
        var notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim();

        if (vin is null || vin.Length != 17 ||
            vin.Any(character => !char.IsAsciiDigit(character) && (character is < 'A' or > 'Z' or 'I' or 'O' or 'Q')) ||
            MileageKilometres < 0 || !CustomerPresent || !IdentityConfirmed || !OwnershipConfirmed || !VehicleConfirmed ||
            notes?.Length > NotesMaximumLength)
        {
            throw new ArgumentException("Check-in requires a VIN, actual odometer and all physical verification affirmations.");
        }

        return this with { Vin = vin, Notes = notes };
    }
}
