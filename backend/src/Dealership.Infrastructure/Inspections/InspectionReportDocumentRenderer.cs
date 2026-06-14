using System.Globalization;
using System.Text.RegularExpressions;
using Dealership.Application.Inspections;
using Dealership.Domain.Inspections;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using static Dealership.Infrastructure.PdfDocumentLayout;

namespace Dealership.Infrastructure.Inspections;

// Layout system: one left edge, a four-column grid, a five-step spacing scale and a six-step type scale.
public sealed partial class InspectionReportDocumentRenderer : IInspectionReportDocumentRenderer
{
    private const int PhotoColumns = 4;
    private const float PhotoHeight = 88;

    public byte[] Render(InspectionReport report, IReadOnlyDictionary<Guid, byte[]> evidence)
    {
        return RenderPage(page =>
        {
            Configure(page, $"Vehicle Inspection & Condition Report · Revision {report.Revision}");
            page.Header().Element(container => Masthead(container, "Vehicle Inspection & Condition Report",
                $"{report.Vehicle.RegistrationPlate} · Revision {report.Revision}"));
            page.Content().PaddingHorizontal(PageMargin, Unit.Millimetre).PaddingTop(SectionGap).Column(column =>
            {
                column.Spacing(Block);
                column.Item().Element(container => Overview(container, report));
                column.Item().Element(container => Findings(container, report, evidence));
                column.Item().Element(container => SignOff(container, report));
                column.Item().Element(container => OtherPoints(container, report));
            });
        });
    }

    private static void Overview(IContainer container, InspectionReport report)
    {
        var vehicle = report.Vehicle;
        var findings = report.Checklist.Count(point => point.Kind == InspectionEntryKind.Finding);
        var notFitted = report.Checklist.Count(point => point.Kind == InspectionEntryKind.NotFitted);
        var passed = report.Checklist.Count - findings - notFitted;
        var rejected = report.Assessment.IsRejected;
        container.BorderBottom(Hairline).BorderColor(Line).PaddingBottom(SectionGap).Column(column =>
        {
            column.Spacing(SectionGap);
            column.Item().Row(row =>
            {
                row.RelativeItem().AlignBottom().Text($"{vehicle.ManufactureYear} {vehicle.Make} {vehicle.Model} {vehicle.Trim}")
                    .FontSize(Title).Bold();
                row.AutoItem().Column(result =>
                {
                    result.Item().AlignRight().Text("Overall result").FontSize(Small).FontColor(Muted);
                    result.Item().AlignRight().Text(rejected ? "Rejected" : $"Grade {report.Assessment.Grade}")
                        .FontSize(Display).Bold().FontColor(rejected ? Signal : Graphite);
                });
            });
            column.Item().Row(row =>
            {
                Fact(row, "Registration", vehicle.RegistrationPlate, bold: true);
                Fact(row, "VIN", vehicle.Vin);
                Fact(row, "Recorded mileage", $"{vehicle.MileageKilometres.ToString("N0", CultureInfo.InvariantCulture)} km");
                Fact(row, "Checked in", Moment(vehicle.CheckedInAt));
            });
            column.Item().Row(row =>
            {
                Fact(row, "Findings", findings.ToString(CultureInfo.InvariantCulture), color: findings > 0 ? Warning : Graphite, emphasis: true);
                Fact(row, "Original repair estimate", Money(report.Assessment.EstimatedRepairCost), emphasis: true);
                Fact(row, "Checklist", $"{passed} passed · {notFitted} not fitted", span: 2, emphasis: true);
            });
        });
    }

    private static void Findings(IContainer container, InspectionReport report, IReadOnlyDictionary<Guid, byte[]> evidence)
    {
        var findings = report.Checklist.Where(point => point.Kind == InspectionEntryKind.Finding)
            .OrderByDescending(point => point.Severity).ThenBy(point => point.Code, StringComparer.Ordinal).ToArray();
        container.Column(column =>
        {
            column.Item().Element(title => SectionTitle(title, "Findings"));
            if (findings.Length == 0)
            {
                column.Item().PaddingTop(SectionGap).Text("No findings were recorded. Every examined point passed.").FontColor(Muted);
            }

            for (var index = 0; index < findings.Length; index++)
            {
                var point = findings[index];
                var entry = column.Item().ShowEntire();
                if (index > 0)
                {
                    entry = entry.BorderTop(Hairline).BorderColor(Line);
                }

                entry.PaddingTop(SectionGap).PaddingBottom(index == findings.Length - 1 ? 0 : SectionGap)
                    .Element(item => Finding(item, point, evidence));
            }
        });
    }

    private static void Finding(IContainer container, InspectionReportPoint point, IReadOnlyDictionary<Guid, byte[]> evidence)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Text(point.Description).FontSize(Strong).SemiBold();
                row.AutoItem().PaddingLeft(Block).Text(Money(point.EstimatedRepairCost)).FontSize(Strong).SemiBold();
            });
            column.Item().PaddingTop(Tight).Text(text =>
            {
                text.DefaultTextStyle(style => style.FontSize(Small).FontColor(Muted));
                text.Span($"{point.Code} · {Label(point.Category.ToString())} · ");
                text.Span(Label(point.Severity.ToString()!)).SemiBold().FontColor(SeverityColor(point.Severity));
            });
            if (!string.IsNullOrWhiteSpace(point.Notes))
            {
                column.Item().PaddingTop(Related).Text(point.Notes);
            }

            if (point.Photos.Count == 0)
            {
                column.Item().PaddingTop(Related).Text("No photo attached").FontSize(Small).FontColor(Muted).Italic();
                return;
            }

            column.Item().PaddingTop(SectionGap).Row(row =>
            {
                for (var slot = 0; slot < PhotoColumns; slot++)
                {
                    var cell = row.RelativeItem().PaddingRight(slot == PhotoColumns - 1 ? 0 : SectionGap);
                    if (slot < point.Photos.Count)
                    {
                        cell.Element(item => Photo(item, point.Photos[slot], slot + 1, evidence));
                    }
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
                column.Item().Height(PhotoHeight).AlignLeft().Image(bytes).FitArea();
            }
            else
            {
                column.Item().Height(PhotoHeight).Background(SoftLine).AlignCenter().AlignMiddle()
                    .Text("Photo unavailable").FontSize(Small).FontColor(Muted);
            }

            column.Item().PaddingTop(Tight).Text($"Photo {number}").FontSize(Micro).FontColor(Muted);
        });
    }

    private static void OtherPoints(IContainer container, InspectionReport report)
    {
        container.Column(column =>
        {
            column.Item().Element(title => SectionTitle(title, "Other checklist points"));
            column.Item().PaddingTop(SectionGap).Text("Examined with no finding recorded. Points marked not fitted do not apply to this vehicle.")
                .FontColor(Muted);
            foreach (var category in report.Checklist.Select(point => point.Category).Distinct())
            {
                var points = report.Checklist.Where(point => point.Category == category &&
                    point.Kind != InspectionEntryKind.Finding).ToArray();
                if (points.Length > 0)
                {
                    column.Item().PaddingTop(SectionGap).Element(item => Category(item, category, points));
                }
            }
        });
    }

    private static void Category(IContainer container, InspectionCategory category, InspectionReportPoint[] points)
    {
        container.Column(column =>
        {
            column.Item().BorderBottom(Hairline).BorderColor(Line).PaddingBottom(Tight).Row(row =>
            {
                row.RelativeItem().Text(Label(category.ToString())).FontSize(Body).Bold();
                row.AutoItem().Text($"{points.Length} points").FontSize(Small).FontColor(Muted);
            });
            column.Item().PaddingTop(Tight).Element(item => PointTable(item, points));
        });
    }

    // Column-major halves share the grid's centre line and let long categories break between rows.
    private static void PointTable(IContainer container, InspectionReportPoint[] points)
    {
        var half = (points.Length + 1) / 2;
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(36);
                columns.RelativeColumn();
                columns.ConstantColumn(36);
                columns.RelativeColumn();
            });
            for (var index = 0; index < half; index++)
            {
                PointCells(table, points[index], gutter: true);
                if (index + half < points.Length)
                {
                    PointCells(table, points[index + half], gutter: false);
                }
                else
                {
                    table.Cell().Text(string.Empty);
                    table.Cell().Text(string.Empty);
                }
            }
        });
    }

    private static void PointCells(TableDescriptor table, InspectionReportPoint point, bool gutter)
    {
        table.Cell().PaddingVertical(0.5f).Text(point.Code).FontSize(Micro).FontColor(Muted);
        table.Cell().PaddingVertical(0.5f).PaddingRight(gutter ? Block : 0).Text(text =>
        {
            text.DefaultTextStyle(style => style.FontSize(Small));
            text.Span(point.Description);
            if (point.Kind == InspectionEntryKind.NotFitted)
            {
                text.Span(" · Not fitted").FontColor(Muted).Italic();
            }
        });
    }

    private static void SignOff(IContainer container, InspectionReport report)
    {
        container.ShowEntire().Column(column =>
        {
            column.Item().Element(title => SectionTitle(title, "Sign-off"));
            column.Item().PaddingTop(SectionGap).Row(row =>
            {
                Fact(row, "Signed by", report.TechnicianIdentity, span: 2);
                Fact(row, "Signed", Moment(report.SignedAt));
                Fact(row, "Revision", report.Revision.ToString(CultureInfo.InvariantCulture));
            });
            column.Item().PaddingTop(SectionGap).Text("This is the retained signed revision. Linked repair work does not change its original findings, estimates or grade.")
                .FontSize(Small).FontColor(Muted);
            column.Item().PaddingTop(Block).BorderTop(Hairline).BorderColor(Line).PaddingTop(SectionGap).Row(row =>
            {
                Fact(row, "Inspection", report.InspectionId.ToString(), span: 2, record: true);
                Fact(row, "Revision receipt", report.OperationId.ToString(), span: 2, record: true);
            });
            column.Item().PaddingTop(Related).Row(row =>
            {
                Fact(row, "Intake appointment", report.Vehicle.IntakeAppointmentId.ToString(), span: 2, record: true);
                Fact(row, "Saved / assignment versions", $"{report.SaveVersion} / {report.AssignmentVersion}", record: true);
                Fact(row, "Checklist catalog", report.CatalogVersion, record: true);
            });
        });
    }

    private static string SeverityColor(InspectionSeverity? severity) => severity switch
    {
        InspectionSeverity.MinorDefect => Warning,
        InspectionSeverity.MajorDefect or InspectionSeverity.CriticalDisqualifier => Signal,
        _ => Muted
    };

    private static string Label(string value) => WordBoundary().Replace(value, "$1 $2");

    [GeneratedRegex("([a-z])([A-Z])")]
    private static partial Regex WordBoundary();
}
