using System.Globalization;
using System.Net;
using System.Text;

namespace Dealership.Infrastructure.Mail;

// One branded layout for every transactional email. Inline styles only, because mail clients ignore stylesheets.
public static class MailTemplate
{
    private const string Brand = "ApexAuto";
    private const string Accent = "#c8102e";
    private const string Ink = "#1f2328";
    private const string Muted = "#5f6670";
    private const string Rule = "#e3e6ea";

    public static string Render(MailLayout layout)
    {
        var html = new StringBuilder();
        html.Append("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\">")
            .Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">")
            .Append(CultureInfo.InvariantCulture, $"<title>{Encode(layout.Title)}</title></head>")
            .Append("<body style=\"margin:0;padding:0;background:#f4f5f7;font-family:Arial,Helvetica,sans-serif;color:")
            .Append(Ink).Append(";\">")
            .Append("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"background:#f4f5f7;\"><tr><td align=\"center\" style=\"padding:24px 12px;\">")
            .Append("<table role=\"presentation\" width=\"600\" cellspacing=\"0\" cellpadding=\"0\" style=\"width:100%;max-width:600px;background:#ffffff;border-radius:8px;overflow:hidden;\">");
        AppendHeader(html);
        html.Append("<tr><td style=\"padding:32px 36px 8px 36px;\">");
        html.Append(CultureInfo.InvariantCulture, $"<h1 style=\"margin:0 0 16px 0;font-size:22px;line-height:1.3;color:{Ink};\">{Encode(layout.Title)}</h1>");
        foreach (var paragraph in layout.Paragraphs)
        {
            html.Append(CultureInfo.InvariantCulture, $"<p style=\"margin:0 0 16px 0;font-size:15px;line-height:1.6;color:{Ink};\">{Encode(paragraph)}</p>");
        }

        AppendDetails(html, layout.Details);
        AppendAction(html, layout.ActionLabel, layout.ActionUrl);
        foreach (var note in layout.Notes)
        {
            html.Append(CultureInfo.InvariantCulture, $"<p style=\"margin:20px 0 0 0;font-size:13px;line-height:1.6;color:{Muted};\">{Encode(note)}</p>");
        }

        html.Append("</td></tr>");
        AppendFooter(html);
        html.Append("</table></td></tr></table></body></html>");
        return html.ToString();
    }

    private static void AppendHeader(StringBuilder html)
    {
        html.Append(CultureInfo.InvariantCulture, $"<tr><td style=\"background:{Ink};padding:20px 36px;\">")
            .Append(CultureInfo.InvariantCulture, $"<span style=\"font-size:20px;font-weight:700;letter-spacing:0.5px;color:#ffffff;\">{Brand}</span>")
            .Append(CultureInfo.InvariantCulture, $"<span style=\"display:inline-block;width:8px;height:8px;margin-left:6px;border-radius:4px;background:{Accent};\"></span>")
            .Append("</td></tr>");
    }

    private static void AppendDetails(StringBuilder html, IReadOnlyList<MailDetail> details)
    {
        if (details.Count == 0)
        {
            return;
        }

        html.Append(CultureInfo.InvariantCulture, $"<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"margin:8px 0 8px 0;border-collapse:collapse;\">");
        foreach (var detail in details)
        {
            html.Append(CultureInfo.InvariantCulture, $"<tr><td width=\"40%\" style=\"padding:10px 0;border-bottom:1px solid {Rule};font-size:13px;color:{Muted};vertical-align:top;\">{Encode(detail.Label)}</td>")
                .Append(CultureInfo.InvariantCulture, $"<td style=\"padding:10px 0;border-bottom:1px solid {Rule};font-size:14px;color:{Ink};font-weight:600;vertical-align:top;\">{Encode(detail.Value)}</td></tr>");
        }

        html.Append("</table>");
    }

    private static void AppendAction(StringBuilder html, string? label, string? url)
    {
        if (label is null || url is null)
        {
            return;
        }

        html.Append("<table role=\"presentation\" cellspacing=\"0\" cellpadding=\"0\" style=\"margin:24px 0 8px 0;\"><tr><td style=\"border-radius:6px;background:")
            .Append(Accent).Append(";\">")
            .Append(CultureInfo.InvariantCulture, $"<a href=\"{Encode(url)}\" style=\"display:inline-block;padding:14px 26px;font-size:15px;font-weight:700;color:#ffffff;text-decoration:none;\">{Encode(label)}</a>")
            .Append("</td></tr></table>")
            .Append(CultureInfo.InvariantCulture, $"<p style=\"margin:12px 0 0 0;font-size:12px;line-height:1.5;color:{Muted};word-break:break-all;\">If the button does not open, copy this link into your browser:<br>{Encode(url)}</p>");
    }

    private static void AppendFooter(StringBuilder html)
    {
        html.Append(CultureInfo.InvariantCulture, $"<tr><td style=\"padding:24px 36px 32px 36px;border-top:1px solid {Rule};font-size:12px;line-height:1.6;color:{Muted};\">")
            .Append(CultureInfo.InvariantCulture, $"{Brand} Malaysia. This is an automated message; please do not reply to it.")
            .Append("</td></tr>");
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
    string? ActionUrl = null);
