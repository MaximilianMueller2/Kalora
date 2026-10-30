using System.Net;
using System.Text;

namespace Kalora.Application.Email;

/// <summary>
/// Turns <see cref="MailContent"/> into a plain, responsive HTML mail (single column, one main button, inline styles
/// because mail clients ignore style sheets) and the text alternative. Every value is HTML-encoded here, never earlier.
/// </summary>
internal static class EmailLayout
{
    private const string Accent = "#6a1bd0";

    public static RenderedEmail Build(MailContent content) =>
        new(content.Subject, Html(content), Text(content));

    private static string Html(MailContent c)
    {
        var e = (string value) => WebUtility.HtmlEncode(value);
        var html = new StringBuilder();
        html.Append("<!doctype html>\n<html lang=\"").Append(e(c.Language)).Append("\">\n<head>\n<meta charset=\"utf-8\">\n")
            .Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n")
            .Append("<meta name=\"color-scheme\" content=\"light\">\n<title>").Append(e(c.Subject)).Append("</title>\n</head>\n")
            .Append("<body style=\"margin:0;padding:0;background:#f6f4fa;\">\n")
            .Append("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"background:#f6f4fa;\"><tr><td align=\"center\" style=\"padding:24px 12px;\">\n")
            .Append("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"max-width:560px;background:#ffffff;border-radius:12px;font-family:Arial,Helvetica,sans-serif;color:#1d1b20;\">\n")
            .Append("<tr><td style=\"padding:24px 28px 8px;font-size:15px;font-weight:bold;color:").Append(Accent).Append(";\">").Append(e(c.Brand)).Append("</td></tr>\n")
            .Append("<tr><td style=\"padding:8px 28px 0;\"><h1 style=\"margin:0;font-size:22px;line-height:28px;\">").Append(e(c.Heading)).Append("</h1></td></tr>\n");

        foreach (var paragraph in c.Paragraphs)
        {
            html.Append("<tr><td style=\"padding:12px 28px 0;font-size:16px;line-height:24px;\">").Append(e(paragraph)).Append("</td></tr>\n");
        }

        if (c.Facts.Count > 0)
        {
            html.Append("<tr><td style=\"padding:16px 28px 0;\"><table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"background:#f6f4fa;border-radius:8px;\">\n");
            foreach (var (label, value) in c.Facts)
            {
                html.Append("<tr><td valign=\"top\" style=\"padding:8px 12px;font-size:14px;color:#5f5b66;width:34%;\">").Append(e(label))
                    .Append("</td><td valign=\"top\" style=\"padding:8px 12px;font-size:15px;font-weight:bold;\">").Append(e(value)).Append("</td></tr>\n");
            }

            html.Append("</table></td></tr>\n");
        }

        if (c.ButtonLabel is not null && SafeUrl(c.ButtonUrl) is { } buttonUrl)
        {
            html.Append("<tr><td style=\"padding:24px 28px 0;\"><a class=\"button\" href=\"").Append(e(buttonUrl))
                .Append("\" style=\"display:inline-block;padding:14px 24px;background:").Append(Accent)
                .Append(";color:#ffffff;text-decoration:none;font-size:16px;font-weight:bold;border-radius:24px;\">").Append(e(c.ButtonLabel)).Append("</a></td></tr>\n")
                .Append("<tr><td style=\"padding:12px 28px 0;font-size:12px;line-height:18px;color:#5f5b66;word-break:break-all;\">").Append(e(c.ButtonFallbackLabel))
                .Append("<br>").Append(e(buttonUrl)).Append("</td></tr>\n");
        }

        foreach (var line in c.Outro)
        {
            html.Append("<tr><td style=\"padding:16px 28px 0;font-size:14px;line-height:21px;color:#5f5b66;\">").Append(e(line)).Append("</td></tr>\n");
        }

        html.Append("<tr><td style=\"padding:24px 28px 24px;\"><div style=\"border-top:1px solid #e6e1ec;padding-top:16px;font-size:13px;line-height:20px;color:#5f5b66;\">\n");
        if (c.ManageLabel is not null && SafeUrl(c.ManageUrl) is { } manageUrl)
        {
            html.Append(e(c.ManageLabel)).Append("<br><a href=\"").Append(e(manageUrl)).Append("\" style=\"color:").Append(Accent).Append(";word-break:break-all;\">")
                .Append(e(manageUrl)).Append("</a><br><br>\n");
        }

        html.Append(string.Join("<br>\n", c.FooterLines.Select(e))).Append("\n</div></td></tr>\n</table>\n</td></tr></table>\n</body>\n</html>\n");
        return html.ToString();
    }

    private static string Text(MailContent c)
    {
        var text = new StringBuilder();
        text.AppendLine(c.Brand).AppendLine().AppendLine(c.Heading).AppendLine();
        foreach (var paragraph in c.Paragraphs)
        {
            text.AppendLine(paragraph).AppendLine();
        }

        foreach (var (label, value) in c.Facts)
        {
            text.Append(label).Append(": ").AppendLine(value);
        }

        if (c.Facts.Count > 0)
        {
            text.AppendLine();
        }

        if (c.ButtonLabel is not null && SafeUrl(c.ButtonUrl) is { } buttonUrl)
        {
            text.Append(c.ButtonLabel).AppendLine(":").AppendLine(buttonUrl).AppendLine();
        }

        foreach (var line in c.Outro)
        {
            text.AppendLine(line).AppendLine();
        }

        text.AppendLine("--");
        if (c.ManageLabel is not null && SafeUrl(c.ManageUrl) is { } manageUrl)
        {
            text.AppendLine(c.ManageLabel).AppendLine(manageUrl).AppendLine();
        }

        foreach (var line in c.FooterLines)
        {
            text.AppendLine(line);
        }

        return text.ToString();
    }

    /// <summary>Only web links go into a mail; anything else (javascript:, data:) is dropped.</summary>
    private static string? SafeUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" ? url : null;
}
