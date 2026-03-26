namespace Dealership.Domain.Vehicles;

public sealed class ListingPublication
{
    private ListingPublication()
    {
    }

    public ListingPublication(Guid id, ListingRevision revision, Guid signedRevisionId,
        Guid actorId, DateTimeOffset recordedAt)
    {
        ArgumentNullException.ThrowIfNull(revision);
        if (id == Guid.Empty || signedRevisionId == Guid.Empty || actorId == Guid.Empty || recordedAt < revision.RecordedAt)
        {
            throw new ArgumentException("Publication requires an approved listing revision and signed inspection.");
        }

        Id = id;
        RevisionId = revision.Id;
        VehicleId = revision.VehicleId;
        BranchId = revision.BranchId;
        SignedRevisionId = signedRevisionId;
        ActorId = actorId;
        RecordedAt = recordedAt;
    }

    public Guid Id { get; private set; }
    public Guid RevisionId { get; private set; }
    public Guid VehicleId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid SignedRevisionId { get; private set; }
    public Guid ActorId { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }
}
