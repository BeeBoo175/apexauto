namespace Dealership.Application.Inspections;

public sealed class InspectionReportDocumentService(IInspectionReportStore reports, InspectionPhotoService photos,
    IInspectionReportDocumentRenderer renderer)
{
    public async Task<InspectionReportDocumentResult> ReadAsync(Guid actorId, Guid inspectionId, long revision,
        CancellationToken cancellationToken)
    {
        var read = await reports.ReadAsync(actorId, inspectionId, revision, cancellationToken);
        if (read.Status != InspectionAssignmentStatus.Succeeded)
        {
            return new(read.Status);
        }

        var report = read.Report!;
        var evidence = new Dictionary<Guid, byte[]>();
        foreach (var photo in report.Checklist.SelectMany(point => point.Photos))
        {
            // The same actor scope that admitted the report admits its signed evidence.
            using var content = new MemoryStream();
            var downloaded = await photos.DownloadAsync(actorId, inspectionId, photo.Id, content, cancellationToken);
            if (downloaded.Status == InspectionPhotoStatus.Available)
            {
                evidence[photo.Id] = content.ToArray();
            }
        }

        return new(InspectionAssignmentStatus.Succeeded, renderer.Render(report, evidence));
    }
}
