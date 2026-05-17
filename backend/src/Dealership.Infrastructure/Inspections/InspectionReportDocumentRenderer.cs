using System.Globalization;
using System.Text.RegularExpressions;
using Dealership.Domain.Common;
using Dealership.Application.Inspections;
using Dealership.Domain.Inspections;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Dealership.Infrastructure.Inspections;

public sealed partial class InspectionReportDocumentRenderer : IInspectionReportDocumentRenderer
{
    private const string Graphite = "#0e1b2a";
    private const string Muted = "#485766";
    private const string Line = "#becbd8";
    private const string SoftLine = "#dce3eb";
    private const string Warning = "#8a4a00";
    private const string Signal = "#b81c10";

    private static readonly TimeZoneInfo Malaysia = TimeZoneInfo.FindSystemTimeZoneById(DealershipLocale.MalaysiaTimeZoneId);

    static InspectionReportDocumentRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Render(InspectionReport report, IReadOnlyDictionary<Guid, byte[]> evidence)
    {
        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(14, Unit.Millimetre);
            page.MarginBottom(18, Unit.Millimetre);
            page.DefaultTextStyle(style => style.FontSize(9.5f).FontColor(Graphite));
            page.Content().Column(column =>
            {
                column.Spacing(14);
                column.Item().Element(container => Heading(container, report));
                column.Item().Element(container => Identity(container, report.Vehicle));
                column.Item().Element(container => Summary(container, report));
                column.Item().Element(container => Findings(container, report, evidence));
                column.Item().Element(container => OtherPoints(container, report));
                column.Item().Element(container => SignOff(container, report));
            });
            page.Footer().Row(row =>
            {
                row.RelativeItem().Text("ApexAuto · Signed inspection report").FontSize(8).FontColor(Muted);
                row.RelativeItem().AlignRight().Text(text =>
                {
                    text.DefaultTextStyle(style => style.FontSize(8).FontColor(Muted));
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });
        })).GeneratePdf();
    }

    private static void Heading(IContainer container, InspectionReport report)
    {
        var vehicle = report.Vehicle;
        var grade = report.Assessment.IsRejected ? "Rejected" : $"Grade {report.Assessment.Grade}";
        container.BorderBottom(2).BorderColor(Graphite).PaddingBottom(8).Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text("Inspection report").FontSize(20).Bold();
                column.Item().Text($"{vehicle.ManufactureYear} {vehicle.Make} {vehicle.Model} {vehicle.Trim}")
                    .FontSize(11).FontColor(Muted);
            });
            row.AutoItem().Text(grade).FontSize(20).Bold()
                .FontColor(report.Assessment.IsRejected ? Signal : Graphite);
        });
    }

    private static void Identity(IContainer container, InspectionReportVehicle vehicle)
    {
        container.Row(row =>
        {
            Fact(row, "Registration", vehicle.RegistrationPlate, bold: true);
            Fact(row, "VIN", vehicle.Vin);
            Fact(row, "Recorded mileage", $"{vehicle.MileageKilometres.ToString("N0", CultureInfo.InvariantCulture)} km");
            Fact(row, "Checked in", Moment(vehicle.CheckedInAt));
        });
    }

    private static void Summary(IContainer container, InspectionReport report)
    {
        var findings = report.Checklist.Count(point => point.Kind == InspectionEntryKind.Finding);
        var notFitted = report.Checklist.Count(point => point.Kind == InspectionEntryKind.NotFitted);
        var passed = report.Checklist.Count - findings - notFitted;
        container.BorderTop(1).BorderBottom(1).BorderColor(Line).PaddingVertical(8).Text(text =>
        {
            text.DefaultTextStyle(style => style.FontSize(10.5f));
            text.Span($"{findings} {(findings == 1 ? "finding" : "findings")} recorded with an original repair estimate of ");
            text.Span(Money(report.Assessment.EstimatedRepairCost)).Bold();
            text.Span($". Of {report.Checklist.Count} checklist points, {passed} passed and {notFitted} " +
                $"{(notFitted == 1 ? "is" : "are")} not fitted.");
        });
    }

    private static void Findings(IContainer container, InspectionReport report, IReadOnlyDictionary<Guid, byte[]> evidence)
    {
        var findings = report.Checklist.Where(point => point.Kind == InspectionEntryKind.Finding)
            .OrderByDescending(point => point.Severity).ThenBy(point => point.Code, StringComparer.Ordinal).ToArray();
        container.Column(column =>
        {
            column.Item().PaddingBottom(6).Text("Findings").FontSize(12).Bold();
            if (findings.Length == 0)
            {
                column.Item().Text("No findings were recorded. Every examined point passed.").FontColor(Muted);
            }

            foreach (var point in findings)
            {
                column.Item().ShowEntire().BorderTop(1).BorderColor(Line).PaddingVertical(8)
                    .Element(entry => Finding(entry, point, evidence));
            }
        });
    }

    private static void Finding(IContainer container, InspectionReportPoint point, IReadOnlyDictionary<Guid, byte[]> evidence)
    {
        container.Column(column =>
        {
            column.Spacing(3);
            column.Item().Row(row =>
            {
                row.RelativeItem().Text(point.Description).FontSize(10.5f).Bold();
                row.AutoItem().Text(Money(point.EstimatedRepairCost)).FontSize(10.5f).Bold();
            });
            column.Item().Text(text =>
            {
                text.DefaultTextStyle(style => style.FontSize(8.5f).FontColor(Muted));
                text.Span($"{point.Code}  ");
                text.Span($"{Label(point.Category.ToString())} · ");
                text.Span(Label(point.Severity.ToString()!)).Bold().FontColor(SeverityColor(point.Severity));
            });
            if (!string.IsNullOrWhiteSpace(point.Notes))
            {
                column.Item().PaddingTop(3).Text(point.Notes);
            }

            if (point.Photos.Count == 0)
            {
                column.Item().PaddingTop(3).Text("No photo attached").FontSize(8.5f).FontColor(Muted);
                return;
            }

            column.Item().PaddingTop(6).Row(row =>
            {
                row.Spacing(8);
                for (var index = 0; index < point.Photos.Count; index++)
                {
                    var photo = point.Photos[index];
                    row.RelativeItem().Element(item => Photo(item, photo, index + 1, evidence));
                }

                for (var filler = point.Photos.Count; filler < 3; filler++)
                {
                    row.RelativeItem();
                }
            });
        });
    }

    private static void Photo(IContainer container, InspectionReportPhoto photo, int number,
        IReadOnlyDictionary<Guid, byte[]> evidence)
    {
        container.Column(column =>
        {
            if (evidence.TryGetValue(photo.Id, out var bytes))
            {
                column.Item().Height(120).Image(bytes).FitArea();
            }
            else
            {
                column.Item().Height(120).Background(SoftLine).AlignCenter().AlignMiddle()
                    .Text("Photo unavailable").FontSize(8.5f).FontColor(Muted);
            }

            column.Item().PaddingTop(2).Text($"Photo {number}").FontSize(8).FontColor(Muted);
        });
    }

    private static void OtherPoints(IContainer container, InspectionReport report)
    {
        container.Column(column =>
        {
            column.Item().Text("Other checklist points").FontSize(12).Bold();
            column.Item().PaddingBottom(4).Text("Examined with no finding recorded. Points marked not fitted do not apply to this vehicle.")
                .FontColor(Muted);
            foreach (var category in report.Checklist.Select(point => point.Category).Distinct())
            {
                var points = report.Checklist.Where(point => point.Category == category &&
                    point.Kind != InspectionEntryKind.Finding).ToArray();
                if (points.Length > 0)
                {
                    column.Item().PaddingTop(10).Element(item => Category(item, category, points));
                }
            }
        });
    }

    private static void Category(IContainer container, InspectionCategory category, IReadOnlyList<InspectionReportPoint> points)
    {
        container.Column(column =>
        {
            column.Item().BorderBottom(1).BorderColor(Line).PaddingBottom(3).Text(text =>
            {
                text.Span(Label(category.ToString())).FontSize(10).Bold();
                text.Span($"   {points.Count} points").FontSize(8.5f).FontColor(Muted);
            });
            column.Item().PaddingTop(3).MultiColumn(multiColumn =>
            {
                multiColumn.Columns(2);
                multiColumn.Spacing(20);
                multiColumn.Content().Column(list =>
                {
                    foreach (var point in points)
                    {
                        list.Item().PaddingVertical(1.5f).Text(text =>
                        {
                            text.DefaultTextStyle(style => style.FontSize(8.5f));
                            text.Span($"{point.Code}  ").FontColor(Muted);
                            text.Span(point.Description);
                            if (point.Kind == InspectionEntryKind.NotFitted)
                            {
                                text.Span(" · Not fitted").FontColor(Muted).Italic();
                            }
                        });
                    }
                });
            });
        });
    }

    private static void SignOff(IContainer container, InspectionReport report)
    {
        container.ShowEntire().BorderTop(2).BorderColor(Graphite).PaddingTop(10).Column(column =>
        {
            column.Spacing(8);
            column.Item().Text("Sign-off").FontSize(12).Bold();
            column.Item().Row(row =>
            {
                Fact(row, "Signed by", report.TechnicianIdentity, weight: 2.2f);
                Fact(row, "Signed", Moment(report.SignedAt), weight: 1.6f);
                Fact(row, "Revision", report.Revision.ToString(CultureInfo.InvariantCulture), weight: 0.6f);
                Fact(row, "Checklist catalog", report.CatalogVersion, weight: 1.2f);
            });
            column.Item().Text("This is the retained signed revision. Linked repair work does not change its original findings, estimates or grade.")
                .FontSize(8.5f).FontColor(Muted);
            column.Item().BorderTop(1).BorderColor(SoftLine).PaddingTop(6).Row(row =>
            {
                Fact(row, "Inspection", report.InspectionId.ToString(), small: true);
                Fact(row, "Revision receipt", report.OperationId.ToString(), small: true);
            });
            column.Item().Row(row =>
            {
                Fact(row, "Intake appointment", report.Vehicle.IntakeAppointmentId.ToString(), small: true);
                Fact(row, "Saved / assignment versions", $"{report.SaveVersion} / {report.AssignmentVersion}", small: true);
            });
        });
    }

    private static void Fact(RowDescriptor row, string label, string value, bool bold = false, bool small = false,
        float weight = 1f)
    {
        row.RelativeItem(weight).Column(column =>
        {
            column.Item().Text(label).FontSize(small ? 7.5f : 8.5f).FontColor(Muted);
            var text = column.Item().Text(value).FontSize(small ? 7.5f : 9.5f);
            if (bold)
            {
                text.Bold();
            }
        });
    }

    private static string SeverityColor(InspectionSeverity? severity) => severity switch
    {
        InspectionSeverity.MinorDefect => Warning,
        InspectionSeverity.MajorDefect or InspectionSeverity.CriticalDisqualifier => Signal,
        _ => Muted
    };

    private static string Money(decimal amount) => $"MYR {amount.ToString("N2", CultureInfo.InvariantCulture)}";

    private static string Moment(DateTimeOffset value)
    {
        var local = TimeZoneInfo.ConvertTime(value, Malaysia);
        return $"{local.ToString("d MMM yyyy, h:mm", CultureInfo.InvariantCulture)} {(local.Hour < 12 ? "am" : "pm")}";
    }

    private static string Label(string value) => WordBoundary().Replace(value, "$1 $2");

    [GeneratedRegex("([a-z])([A-Z])")]
    private static partial Regex WordBoundary();
}
