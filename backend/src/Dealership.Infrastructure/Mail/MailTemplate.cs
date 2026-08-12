using System.Globalization;
using System.Net;
using System.Text;

namespace Dealership.Infrastructure.Mail;

// One branded layout for every transactional email. Table-based, inline styles only, because mail clients ignore
// stylesheets and most drop flexbox and CSS variables. Colours and type mirror the site tokens in globals.css.
public static class MailTemplate
{
    private const string Graphite = "#0e1b2a";
    private const string Canvas = "#f2f5f8";
    private const string Paper = "#ffffff";
    private const string Signal = "#d92518";
    private const string Line = "#dce3eb";
    private const string Muted = "#485766";
    private const string Subtle = "#5c6a79";
    private const string Sans = "Arial, 'Helvetica Neue', Helvetica, sans-serif";
    private const string Display = "'Arial Narrow', 'Helvetica Neue Condensed', Arial, sans-serif";

    public static string Render(MailLayout layout)
    {
        var html = new StringBuilder();
        html.Append("<!doctype html><html lang=\"en\" xmlns=\"http://www.w3.org/1999/xhtml\"><head>");
        html.Append("<meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        html.Append("<meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\">");
        html.Append("<meta name=\"color-scheme\" content=\"light\"><meta name=\"supported-color-schemes\" content=\"light\">");
        html.Append(string.Concat("<title>", Encode(layout.Title), "</title>"));
        html.Append("<style>@media only screen and (max-width:620px){");
        html.Append(".mail-card{padding:32px 24px !important;}");
        html.Append(".mail-title{font-size:24px !important;}");
        html.Append(".mail-gutter{padding:24px 12px !important;}");
        html.Append(".mail-detail-label{width:auto !important;display:block !important;padding:14px 0 2px 0 !important;border-bottom:0 !important;}");
        html.Append(".mail-detail-value{display:block !important;padding:0 0 14px 0 !important;}");
        html.Append("}</style></head>");
        html.Append(string.Concat("<body style=\"margin:0;padding:0;background:", Canvas, ";font-family:", Sans,
            ";color:", Graphite, ";-webkit-text-size-adjust:100%;\">"));
        html.Append(Preheader(layout.Preheader));
        html.Append(string.Concat(
            "<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" border=\"0\" style=\"background:",
            Canvas, ";\"><tr><td class=\"mail-gutter\" align=\"center\" style=\"padding:40px 16px;\">"));
        html.Append(
            "<table role=\"presentation\" width=\"600\" cellspacing=\"0\" cellpadding=\"0\" border=\"0\" style=\"width:100%;max-width:600px;\">");
        html.Append(Header());
        html.Append(string.Concat("<tr><td class=\"mail-card\" bgcolor=\"", Paper, "\" style=\"background:", Paper,
            ";padding:48px 48px 40px 48px;\">"));
        html.Append(string.Concat(
            "<h1 class=\"mail-title\" style=\"margin:0 0 20px 0;font-family:", Display,
            ";font-size:30px;line-height:1.2;font-weight:700;color:", Graphite, ";\">",
            Encode(layout.Title), "</h1>"));
        foreach (var paragraph in layout.Paragraphs)
        {
            html.Append(Paragraph(paragraph, Graphite, 17));
        }

        html.Append(Details(layout.Details));
        html.Append(Action(layout.ActionLabel, layout.ActionUrl));
        foreach (var note in layout.Notes)
        {
            html.Append(Paragraph(note, Muted, 14));
        }

        html.Append("</td></tr>");
        html.Append(Footer());
        html.Append("</table></td></tr></table></body></html>");
        return html.ToString();
    }

    private static string Preheader(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return string.Concat(
            "<div style=\"display:none;max-height:0;overflow:hidden;mso-hide:all;font-size:1px;line-height:1px;color:",
            Canvas, ";\">", Encode(text), "</div>");
    }

    private static string Header()
    {
        return string.Concat(
            "<tr><td bgcolor=\"", Graphite, "\" style=\"background:", Graphite, ";padding:26px 40px;\">",
            "<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" border=\"0\"><tr>",
            "<td style=\"font-family:", Display, ";font-size:24px;font-weight:700;color:", Paper,
            ";letter-spacing:0.5px;\">ApexAuto</td>",
            "<td align=\"right\" style=\"font-family:", Sans, ";font-size:12px;color:#b7c4d2;\">Malaysia</td>",
            "</tr></table></td></tr>",
            "<tr><td bgcolor=\"", Signal, "\" height=\"4\" style=\"background:", Signal,
            ";font-size:0;line-height:0;height:4px;\">&nbsp;</td></tr>");
    }

    private static string Paragraph(string text, string colour, int size)
    {
        return string.Concat(
            "<p style=\"margin:0 0 18px 0;font-family:", Sans, ";font-size:", size.ToString(CultureInfo.InvariantCulture),
            "px;line-height:1.6;color:", colour, ";\">", Encode(text), "</p>");
    }

    private static string Details(IReadOnlyList<MailDetail> details)
    {
        if (details.Count == 0)
        {
            return string.Empty;
        }

        var rows = new StringBuilder();
        foreach (var detail in details)
        {
            rows.Append(string.Concat(
                "<tr><td class=\"mail-detail-label\" width=\"36%\" valign=\"top\" style=\"padding:18px 24px 18px 0;border-bottom:1px solid ",
                Line, ";font-family:", Sans, ";font-size:13px;line-height:1.5;font-weight:400;color:", Muted, ";\">",
                Encode(detail.Label),
                "</td><td class=\"mail-detail-value\" valign=\"top\" style=\"padding:18px 0;border-bottom:1px solid ",
                Line, ";font-family:", Sans, ";font-size:16px;line-height:1.5;font-weight:700;color:", Graphite, ";\">",
                Encode(detail.Value), "</td></tr>"));
        }

        return string.Concat(
            "<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" border=\"0\" style=\"margin:12px 0 32px 0;border-top:1px solid ",
            Line, ";\">", rows.ToString(), "</table>");
    }

    private static string Action(string? label, string? url)
    {
        if (label is null || url is null)
        {
            return string.Empty;
        }

        var href = Encode(url);
        return string.Concat(
            "<table role=\"presentation\" cellspacing=\"0\" cellpadding=\"0\" border=\"0\" style=\"margin:8px 0 0 0;\"><tr>",
            "<td bgcolor=\"", Signal, "\" style=\"background:", Signal, ";border-radius:4px;\">",
            "<a href=\"", href, "\" target=\"_blank\" style=\"display:inline-block;padding:15px 30px;font-family:", Sans,
            ";font-size:15px;font-weight:700;line-height:1;color:", Paper, ";text-decoration:none;border-radius:4px;\">",
            Encode(label), "</a></td></tr></table>",
            "<p style=\"margin:28px 0 0 0;font-family:", Sans, ";font-size:12px;line-height:1.6;color:", Subtle,
            ";word-break:break-all;\">If the button does not work, open this link: <a href=\"", href,
            "\" style=\"color:", Muted, ";\">", href, "</a></p>");
    }

    private static string Footer()
    {
        return string.Concat(
            "<tr><td style=\"padding:24px 40px 32px 40px;font-family:", Sans, ";font-size:12px;line-height:1.6;color:",
            Subtle, ";\">",
            "<p style=\"margin:0 0 8px 0;\">ApexAuto Malaysia. Vehicles inspected, priced and recorded in one place.</p>",
            "<p style=\"margin:0;\">This is an automated message. Please do not reply to it.</p>",
            "</td></tr>");
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}

public sealed record MailDetail(string Label, string Value);

public sealed record MailLayout(
    string Title,
    IReadOnlyList<string> Paragraphs,
    IReadOnlyList<MailDetail> Details,
    IReadOnlyList<string> Notes,
    string? ActionLabel = null,
    string? ActionUrl = null,
    string? Preheader = null);
