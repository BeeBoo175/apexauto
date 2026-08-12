using System.Text.RegularExpressions;
using Dealership.Domain.Common;
namespace Dealership.Domain.Intake;

public sealed partial class IntakeAppointment
{
    public const int RegistrationPlateMaximumLength = 20;

    [GeneratedRegex("^[A-Z]{1,3} ?[1-9][0-9]{0,3}(?: ?[A-Z])?$", RegexOptions.CultureInvariant)]
    private static partial Regex MalaysianPlatePattern();

    private IntakeAppointment()
    {
    }

    public IntakeAppointment(Guid id, Guid ownerId, Guid branchId, string registrationPlate,
        AppointmentSlot slot, DateTimeOffset createdAt, SellerValuation valuation)
    {
        ArgumentNullException.ThrowIfNull(valuation);

        if (id == Guid.Empty || ownerId == Guid.Empty || branchId == Guid.Empty ||
            !IsValidRegistrationPlate(registrationPlate) ||
            slot.StartsAt <= createdAt || slot.EndsAt <= slot.StartsAt)
        {
            throw new ArgumentException("An appointment requires valid identity, registration and future slot.");
        }

        Id = id;
        OwnerId = ownerId;
        BranchId = branchId;
        RegistrationPlate = registrationPlate;
        StartsAt = slot.StartsAt.ToUniversalTime();
        EndsAt = slot.EndsAt.ToUniversalTime();
        CreatedAt = createdAt.ToUniversalTime();
        Valuation = valuation;
    }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public Guid BranchId { get; private set; }
    public string RegistrationPlate { get; private set; } = string.Empty;
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset EndsAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }
    public DateTimeOffset? CancellationPhotosExpireAt { get; private set; }
    public SellerValuation Valuation { get; private set; } = null!;
    public Guid? BookedByStaffId { get; private set; }

    // Set when a staff member made the booking for the customer, so the record still names who acted.
    public void RecordAssistance(Guid staffId)
    {
        if (staffId == Guid.Empty)
        {
            throw new ArgumentException("Assisting staff require an identity.", nameof(staffId));
        }

        BookedByStaffId = staffId;
    }

    public void Cancel(DateTimeOffset now, TimeSpan photoRetention)
    {
        if (CancelledAt is not null)
        {
            return;
        }

        if (now < CreatedAt || now >= StartsAt || photoRetention <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Only future appointments can be cancelled.");
        }

        CancelledAt = now.ToUniversalTime();
        CancellationPhotosExpireAt = CancelledAt + photoRetention;
    }

    public static bool IsValidRegistrationPlate(string? registrationPlate)
    {
        return !string.IsNullOrWhiteSpace(registrationPlate) &&
            registrationPlate.Length <= RegistrationPlateMaximumLength &&
            MalaysianPlatePattern().IsMatch(registrationPlate);
    }
}
