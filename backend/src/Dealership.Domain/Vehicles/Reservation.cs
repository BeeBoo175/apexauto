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
    public DateTimeOffset ExpiresAt { get; private set; }

    // SQL maintains this projection from retained release receipts for unique active slots.
    public bool IsActive { get; private set; } = true;

    public bool HasExpired(DateTimeOffset now) => now >= ExpiresAt;
}
