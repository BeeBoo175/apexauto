using Dealership.Domain.Customers;

namespace Dealership.Domain.Vehicles;

public sealed record ReservationBuyer(string LegalName, string Email, string Mobile, string Nric);

public sealed class Reservation
{
    public const decimal DepositAmount = 500m;

    private Reservation()
    {
    }

    public Reservation(Guid id, CustomerProfile customer, string email, ListingPublication publication,
        ListingRevision listing, PurchaseSettings settings, PromoCode? promo, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(customer);
        ArgumentNullException.ThrowIfNull(publication);
        ArgumentNullException.ThrowIfNull(listing);
        ArgumentNullException.ThrowIfNull(settings);
        if (id == Guid.Empty || publication.RevisionId != listing.Id ||
            publication.VehicleId != listing.VehicleId || publication.BranchId != listing.BranchId ||
            now.Offset != TimeSpan.Zero || now <= DateTimeOffset.MinValue ||
            now > DateTimeOffset.MaxValue.AddHours(-settings.ReservationHoldHours) || now < publication.RecordedAt ||
            now < settings.UpdatedAtUtc || string.IsNullOrWhiteSpace(email) ||
            email.Length > 256 || email.Any(char.IsControl))
        {
            throw new ArgumentException("A reservation requires its customer, approved listing and current UTC terms.");
        }

        if (promo is not null && !promo.CanApply(listing.VehiclePrice, now))
        {
            throw new ArgumentException("The selected promo is unavailable for this reservation.");
        }

        Id = id;
        CustomerId = customer.UserId;
        VehicleId = publication.VehicleId;
        BranchId = publication.BranchId;
        PublicationId = publication.Id;
        Buyer = new(customer.LegalName, email, customer.Mobile, customer.Nric);
        PromoId = promo?.Id;
        PromoCode = promo?.Code;
        SstRate = settings.SstRate;
        Price = PurchasePricing.CalculatePrice(listing.VehiclePrice, settings.ProcessingFee,
            listing.RoadTax, settings.SstRate, promo?.Discount ?? 0m);
        CreatedAt = now;
        ExpiresAt = now.AddHours(settings.ReservationHoldHours);
    }

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid VehicleId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid PublicationId { get; private set; }
    public ReservationBuyer Buyer { get; private set; } = null!;
    public Guid? PromoId { get; private set; }
    public string? PromoCode { get; private set; }
    public decimal SstRate { get; private set; }
    public PurchasePrice Price { get; private set; } = null!;
    public decimal Deposit { get; private set; } = DepositAmount;
    public DateTimeOffset CreatedAt { get; private set; }

    // The deadline while the hold is open; a claimed hold ignores it and resumes from FrozenRemaining on release.
    public DateTimeOffset ExpiresAt { get; private set; }

    // The agent handling the hold, when someone has claimed it or been given it.
    public Guid? ClaimedById { get; private set; }
    public DateTimeOffset? ClaimedAt { get; private set; }

    // Time that was left when the hold was claimed; the timer stands still until the claim is released.
    public TimeSpan? FrozenRemaining { get; private set; }

    // SQL maintains this projection from retained release receipts for unique active slots.
    public bool IsActive { get; private set; } = true;

    public bool IsClaimed => ClaimedById is not null;

    public bool HasExpired(DateTimeOffset now) => !IsClaimed && now >= ExpiresAt;

    // Takes an open hold for an agent; the actor is the agent when they claim it themselves, else a manager assigning it.
    public ReservationEvent Claim(Guid agentId, Guid actorId, int sequence, DateTimeOffset now)
    {
        RequireClaimCommand(agentId, actorId, sequence, now);
        if (IsClaimed)
        {
            throw new InvalidOperationException("The hold is already being handled.");
        }

        if (HasExpired(now))
        {
            throw new InvalidOperationException("An expired hold cannot be claimed.");
        }

        FrozenRemaining = ExpiresAt - now;
        ClaimedById = agentId;
        ClaimedAt = now;
        var kind = agentId == actorId ? ReservationEventKind.Claimed : ReservationEventKind.Assigned;
        return new ReservationEvent(Id, sequence, kind, actorId, agentId, null, now);
    }

    public ReservationEvent Reassign(Guid agentId, Guid actorId, int sequence, DateTimeOffset now)
    {
        RequireClaimCommand(agentId, actorId, sequence, now);
        if (!IsClaimed || now < ClaimedAt)
        {
            throw new InvalidOperationException("Only a hold being handled can be reassigned.");
        }

        if (ClaimedById == agentId)
        {
            throw new InvalidOperationException("The hold is already assigned to that agent.");
        }

        var previous = ClaimedById;
        ClaimedById = agentId;
        ClaimedAt = now;
        return new ReservationEvent(Id, sequence, ReservationEventKind.Reassigned, actorId, agentId, previous, now);
    }

    // Gives the hold back with the time it had left, rather than a fresh hold period.
    public ReservationEvent Release(Guid actorId, int sequence, DateTimeOffset now)
    {
        RequireClaimCommand(actorId, actorId, sequence, now);
        if (!IsClaimed || now < ClaimedAt)
        {
            throw new InvalidOperationException("Only a hold being handled can be released.");
        }

        var previous = ClaimedById;
        ExpiresAt = now + FrozenRemaining!.Value;
        ClaimedById = null;
        ClaimedAt = null;
        FrozenRemaining = null;
        return new ReservationEvent(Id, sequence, ReservationEventKind.Released, actorId, null, previous, now);
    }

    private void RequireClaimCommand(Guid agentId, Guid actorId, int sequence, DateTimeOffset now)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("A released hold cannot be claimed or reassigned.");
        }

        if (agentId == Guid.Empty || actorId == Guid.Empty || sequence <= 0 || now.Offset != TimeSpan.Zero ||
            now < CreatedAt)
        {
            throw new ArgumentException("A hold command requires its agent, actor and a UTC time within the hold.");
        }
    }
}
