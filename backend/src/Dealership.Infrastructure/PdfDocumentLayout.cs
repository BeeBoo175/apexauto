using System.Globalization;
using Dealership.Domain.Common;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Dealership.Infrastructure;

internal static class PdfDocumentLayout
{
    public const string Graphite = "#0e1b2a";
    public const string Muted = "#485766";
    public const string Line = "#becbd8";
    public const string SoftLine = "#dce3eb";
    public const string Warning = "#8a4a00";
    public const string Signal = "#b81c10";
    public const string BrandRed = "#d92518";

    public const float PageMargin = 14;
    public const float Tight = 4;
    public const float Related = 8;
    public const float SectionGap = 12;
    public const float Block = 20;

    public const float Micro = 7.5f;
    public const float Small = 8.5f;
    public const float Body = 9.5f;
    public const float Strong = 11;
    public const float Title = 15;
    public const float Display = 22;

    public const float Hairline = 0.75f;
    public const float Heavy = 1.5f;

    private static readonly Color WatermarkApex = Color.FromARGB(26, 14, 27, 42);
    private static readonly Color WatermarkAuto = Color.FromARGB(34, 217, 37, 24);

    static PdfDocumentLayout()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] RenderPage(Action<PageDescriptor> compose) =>
        Document.Create(document => document.Page(compose)).GeneratePdf();

    public static void Configure(PageDescriptor page, string footer)
    {
        page.Size(PageSizes.A4);
        page.MarginHorizontal(0);
        page.MarginTop(0);
        page.MarginBottom(PageMargin, Unit.Millimetre);
        page.DefaultTextStyle(style => style.FontSize(Body).FontColor(Graphite));
        page.Background().AlignCenter().AlignMiddle().Rotate(-40).Text(text =>
        {
            text.DefaultTextStyle(style => style.FontSize(120).Bold());
            text.Span("Apex").FontColor(WatermarkApex);
            text.Span("Auto").FontColor(WatermarkAuto);
        });
        page.Footer().PaddingHorizontal(PageMargin, Unit.Millimetre).PaddingTop(Related).Row(row =>
        {
            row.RelativeItem().Text($"ApexAuto · {footer}")
                .FontSize(Small).FontColor(Muted);
            row.AutoItem().Text(text =>
            {
                text.DefaultTextStyle(style => style.FontSize(Small).FontColor(Muted));
                text.Span("Page ");
                text.CurrentPageNumber();
                text.Span(" of ");
                text.TotalPages();
            });
        });
    }

    public static void Masthead(IContainer container, string title, string reference)
    {
        container.Background(Graphite).PaddingHorizontal(PageMargin, Unit.Millimetre).PaddingVertical(SectionGap).Row(row =>
        {
            row.RelativeItem().AlignMiddle().Text(text =>
            {
                text.Span("Apex").FontSize(Title).Bold().FontColor(Colors.White);
                text.Span("Auto").FontSize(Title).Bold().FontColor(BrandRed);
            });
            row.AutoItem().Column(column =>
            {
                column.Item().AlignRight().Text(title)
                    .FontSize(Body).SemiBold().FontColor(Colors.White);
                column.Item().AlignRight().Text(reference)
                    .FontSize(Small).FontColor(Line);
            });
        });
    }

    public static void SectionTitle(IContainer container, string title)
    {
        container.BorderBottom(Heavy).BorderColor(Graphite).PaddingBottom(Tight)
            .Text(title.ToUpperInvariant()).FontSize(Body).Bold().LetterSpacing(0.08f);
    }

    // Every fact row sits on the same four equal columns; span widens a cell without moving its left edge.
    public static void Fact(RowDescriptor row, string label, string value, float span = 1, bool bold = false,
        bool emphasis = false, bool record = false, string color = Graphite)
    {
        var size = Body;
        if (record)
        {
            size = Micro;
        }
        else if (emphasis)
        {
            size = Strong;
        }

        row.RelativeItem(span).PaddingRight(SectionGap).Column(column =>
        {
            column.Item().Text(label).FontSize(record ? Micro : Small).FontColor(Muted);
            var text = column.Item().PaddingTop(Tight / 2).Text(value)
                .FontSize(size).FontColor(color);
            if (bold || emphasis)
            {
                text.SemiBold();
            }
        });
    }

    public static string Money(decimal amount) => $"MYR {amount.ToString("N2", CultureInfo.InvariantCulture)}";

    public static string Moment(DateTimeOffset value)
    {
        var local = TimeZoneInfo.ConvertTime(value, DealershipLocale.Malaysia);
        return $"{local.ToString("d MMM yyyy, h:mm", CultureInfo.InvariantCulture)} {(local.Hour < 12 ? "am" : "pm")}";
    }

}
