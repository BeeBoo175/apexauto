namespace Dealership.Domain.Intake;

public sealed class IntakeAppointment
{
    private IntakeAppointment()
    {
    }

    public IntakeAppointment(Guid id, Guid ownerId, Guid branchId, string registrationPlate,
        InspectionSlot slot, DateTimeOffset createdAt, SellerValuation valuation)
    {
        ArgumentNullException.ThrowIfNull(valuation);

        if (id == Guid.Empty || ownerId == Guid.Empty || branchId == Guid.Empty ||
            string.IsNullOrWhiteSpace(registrationPlate) || registrationPlate.Length > 20 ||
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
}
