namespace Dealership.Domain.Vehicles;

public sealed class ListingWithdrawal
{
    private ListingWithdrawal()
    {
    }

    public ListingWithdrawal(Guid id, ListingPublication publication, Guid actorId, string reason,
        DateTimeOffset recordedAt)
    {
        ArgumentNullException.ThrowIfNull(publication);
        if (id == Guid.Empty || actorId == Guid.Empty || recordedAt < publication.RecordedAt ||
            string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1000 || reason.Any(char.IsControl))
        {
            throw new ArgumentException("Withdrawal requires a publication, actor and reason of at most 1000 characters.");
        }

        Id = id;
        PublicationId = publication.Id;
        VehicleId = publication.VehicleId;
        BranchId = publication.BranchId;
        ActorId = actorId;
        Reason = reason.Trim();
        RecordedAt = recordedAt;
    }

    public Guid Id { get; private set; }
    public Guid PublicationId { get; private set; }
    public Guid VehicleId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid ActorId { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; private set; }
}
