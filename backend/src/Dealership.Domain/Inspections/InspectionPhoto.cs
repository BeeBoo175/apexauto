using Dealership.Domain.Photos;

namespace Dealership.Domain.Inspections;

public sealed class InspectionPhoto : IPhotoObject
{
    private InspectionPhoto()
    {
    }

    public InspectionPhoto(Guid id, Guid inspectionId, Guid branchId, Guid actorId, long assignmentVersion,
        string pointCode, PhotoContent content, DateTimeOffset createdAt, TimeSpan lifetime)
    {
        InspectionCatalog.GetPoint(pointCode);
        if (id == Guid.Empty || inspectionId == Guid.Empty || branchId == Guid.Empty || actorId == Guid.Empty ||
            assignmentVersion < 1 || !content.IsValid || lifetime <= TimeSpan.Zero)
        {
            throw new ArgumentException("Inspection photo identity, assignment, content and lifetime must be valid.");
        }

        Id = id;
        InspectionId = inspectionId;
        BranchId = branchId;
        ActorId = actorId;
        AssignmentVersion = assignmentVersion;
        PointCode = pointCode;
        ObjectKey = $"inspection/photos/{id:N}";
        ContentType = content.ContentType;
        Bytes = content.Bytes;
        Sha256 = content.Sha256;
        Width = content.Width;
        Height = content.Height;
        CreatedAt = createdAt;
        ExpiresAt = createdAt + lifetime;
    }

    public Guid Id { get; private set; }
    public Guid InspectionId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid ActorId { get; private set; }
    public long AssignmentVersion { get; private set; }
    public string PointCode { get; private set; } = string.Empty;
    public string ObjectKey { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long Bytes { get; private set; }
    public string Sha256 { get; private set; } = string.Empty;
    public int Width { get; private set; }
    public int Height { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? AvailableAt { get; private set; }

    public bool Matches(Guid inspectionId, Guid actorId, long assignmentVersion, string pointCode, PhotoContent content)
    {
        return InspectionId == inspectionId && ActorId == actorId && AssignmentVersion == assignmentVersion &&
            PointCode == pointCode && ContentType == content.ContentType && Bytes == content.Bytes &&
            Sha256 == content.Sha256 && Width == content.Width && Height == content.Height;
    }

    public void ConfirmAvailable(DateTimeOffset now)
    {
        if (now < CreatedAt || now >= ExpiresAt)
        {
            throw new InvalidOperationException("Expired or future inspection uploads cannot become available.");
        }

        AvailableAt ??= now;
    }
}
