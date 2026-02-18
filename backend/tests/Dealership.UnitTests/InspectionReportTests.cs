using System.Text.Json;
using Dealership.Application.Inspections;
using Dealership.Domain.Inspections;
using Dealership.Domain.Intake;
using Dealership.Domain.Photos;
using Dealership.Domain.Vehicles;

namespace Dealership.UnitTests;

public sealed class InspectionReportTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 22, 1, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SigningFreezesFullCatalogIdentityOriginalAssessmentAndSelectedEvidence()
    {
        var (draft, vehicle, save, photos) = Example();
        var report = InspectionReport.Sign(Guid.NewGuid(), 1, draft, vehicle, "azlan@apexauto.test", save, photos, At.AddHours(1));
        var serialized = JsonSerializer.Serialize(report, JsonSerializerOptions.Web);
        draft.Reassign(Guid.NewGuid(), 1);
        photos.Clear();

        Assert.Equal(InspectionCatalog.Points.Count, report.Checklist.Count);
        Assert.Equal(save.ActorId, report.TechnicianId);
        Assert.Equal(1, report.AssignmentVersion);
        Assert.Equal(92000, report.Vehicle.MileageKilometres);
        Assert.Equal(vehicle.Vin, report.Vehicle.Vin);
        Assert.Equal(vehicle.IntakeAppointmentId, report.Vehicle.IntakeAppointmentId);
        Assert.Equal(InspectionGrade.B, report.Assessment.Grade);
        Assert.Equal(500m, report.Assessment.EstimatedRepairCost);
        var finding = Assert.Single(report.Checklist, point => point.Code == "EP-004");
        Assert.Equal("Bumper scratch", finding.Notes);
        Assert.Single(finding.Photos);
        Assert.Null(report.Checklist[0].Kind);
        Assert.Equal(InspectionEntryKind.NotFitted, report.Checklist.Single(point => point.Code == "IE-040").Kind);
        Assert.Throws<NotSupportedException>(() => ((IList<InspectionReportPoint>)report.Checklist).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<InspectionReportPhoto>)finding.Photos).Clear());
        Assert.Equal(serialized, JsonSerializer.Serialize(report, JsonSerializerOptions.Web));
    }

    [Fact]
    public void HistoricalDeserializationRetainsOriginalDefinitionsAndGradeWithoutCurrentRules()
    {
        var (draft, vehicle, save, photos) = Example();
        var report = InspectionReport.Sign(Guid.NewGuid(), 2, draft, vehicle, "azlan@apexauto.test", save, photos, At.AddHours(1));
        var oldDefinition = report.Checklist[0] with { Code = "RETIRED-POINT", Description = "Original definition" };
        var historical = report with
        {
            CatalogVersion = "retired-catalog",
            Assessment = new(500m, InspectionGrade.C, false),
            Checklist = [oldDefinition, .. report.Checklist.Skip(1)]
        };

        var json = JsonSerializer.Serialize(historical, JsonSerializerOptions.Web);
        var restored = JsonSerializer.Deserialize<InspectionReport>(json, JsonSerializerOptions.Web)!;
        Assert.Equal(json, JsonSerializer.Serialize(restored, JsonSerializerOptions.Web));
        Assert.Equal("RETIRED-POINT", restored.Checklist[0].Code);
        Assert.Equal(InspectionGrade.C, restored.Assessment.Grade);
    }

    [Fact]
    public void SigningRejectsUnattestedStaleMismatchedAndForgedSaveEvidence()
    {
        var (draft, vehicle, save, photos) = Example();
        InspectionDraftSaveReceipt[] invalid =
        [
            save with { ExaminedPointCodes = null },
            save with { ExaminedPointCodes = [.. save.ExaminedPointCodes!.Skip(1)] },
            save with { AssignmentVersion = 2 },
            save with { ActorId = Guid.NewGuid() },
            save with { InspectionId = Guid.NewGuid() },
            save with { CatalogVersion = "unrecognized" },
            save with { RecordedAt = At.AddHours(2) },
            save with { Assessment = new(500m, InspectionGrade.A, false) },
            save with { PhotoIds = [] }
        ];
        foreach (var candidate in invalid)
        {
            Assert.Throws<ArgumentException>(() => InspectionReport.Sign(Guid.NewGuid(), 1, draft, vehicle,
                "azlan@apexauto.test", candidate, photos, At.AddHours(1)));
        }

        Assert.Throws<ArgumentException>(() => InspectionReport.Sign(Guid.Empty, 1, draft, vehicle,
            "azlan@apexauto.test", save, photos, At.AddHours(1)));
        Assert.Throws<ArgumentException>(() => InspectionReport.Sign(Guid.NewGuid(), 0, draft, vehicle,
            "azlan@apexauto.test", save, photos, At.AddHours(1)));
        Assert.Throws<ArgumentException>(() => InspectionReport.Sign(Guid.NewGuid(), 1, draft, vehicle,
            "azlan@apexauto.test", save, [], At.AddHours(1)));
        Assert.Throws<ArgumentException>(() => InspectionReport.Sign(Guid.NewGuid(), 1, draft, vehicle,
            "azlan@apexauto.test", save, [photos[0], photos[0]], At.AddHours(1)));
    }

    [Fact]
    public void SigningRetainsRejectionAndAllowsReferencedPhotosBeyondUploadExpiry()
    {
        var (draft, vehicle, save, photos) = Example();
        var rejected = save with
        {
            Entries = [new("EP-004", InspectionEntryKind.Finding, InspectionSeverity.CriticalDisqualifier, 0, "Flood evidence")],
            ExaminedPointCodes = InspectionCatalog.Points.Select(point => point.Code).ToArray(),
            Assessment = new(0, null, true)
        };
        var report = InspectionReport.Sign(Guid.NewGuid(), 1, draft, vehicle, "azlan@apexauto.test",
            rejected, photos, At.AddDays(2));
        Assert.True(report.Assessment.IsRejected);
        Assert.Null(report.Assessment.Grade);
        Assert.Single(report.Checklist.Single(point => point.Code == "EP-004").Photos);
    }

    private static (InspectionDraft Draft, Vehicle Vehicle, InspectionDraftSaveReceipt Save, List<InspectionPhoto> Photos) Example()
    {
        var valuation = new SellerValuation("Proton", "Saga", "Standard", 2020, 90000, 18000, 23000, "MYR", 2026);
        var appointment = new IntakeAppointment(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "WYY 8821",
            new InspectionSlot(At, At.AddMinutes(90)), At.AddDays(-1), valuation);
        var checkIn = new AppointmentCheckIn(Guid.NewGuid(), appointment, Guid.NewGuid(),
            new("PL1BT65R84B123456", 92000, true, true, true, true, null), At);
        var vehicle = new Vehicle(appointment, checkIn);
        var draft = new InspectionDraft(Guid.NewGuid(), vehicle, Guid.NewGuid(), At);
        var photo = new InspectionPhoto(Guid.NewGuid(), draft.Id, draft.BranchId, draft.TechnicianId, 1, "EP-004",
            new PhotoContent("image/jpeg", 1000, new string('a', 64), 640, 480), At, TimeSpan.FromDays(1));
        photo.ConfirmAvailable(At.AddMinutes(1));
        var save = new InspectionDraftSaveReceipt(Guid.NewGuid(), draft.Id, draft.TechnicianId, 1, 0, 1,
            InspectionCatalog.Version,
            [new("EP-004", InspectionEntryKind.Finding, InspectionSeverity.MinorDefect, 500m, "Bumper scratch"),
                new("IE-040", InspectionEntryKind.NotFitted, null, 0, "No powered roof")],
            new(500m, InspectionGrade.B, false), At.AddMinutes(2))
        {
            PhotoIds = [photo.Id],
            ExaminedPointCodes = InspectionCatalog.Points.Where(point => point.Code != "IE-040").Select(point => point.Code).ToArray()
        };
        return (draft, vehicle, save, [photo]);
    }
}
