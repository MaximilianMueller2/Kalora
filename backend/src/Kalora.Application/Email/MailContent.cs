namespace Kalora.Application.Email;

/// <summary>Everything one mail says, before it is turned into HTML and text.</summary>
/// <param name="Brand">Name in the header: the salon, or "Kalora" for account mails.</param>
/// <param name="Facts">Label and value rows (service, person, time).</param>
/// <param name="ButtonLabel">The one main button; with <paramref name="ButtonUrl"/>.</param>
/// <param name="ManageLabel">Plain link at the bottom of customer mails to the appointment.</param>
/// <param name="Calendar">Only the confirmation has it. These links are plain text links, so the mail keeps its one main button.</param>
/// <param name="FooterLines">Salon name, address and phone for customer mails, or a note that the mail is automatic.</param>
public sealed record MailContent(
    string Language,
    string Brand,
    string Subject,
    string Heading,
    IReadOnlyList<string> Paragraphs,
    IReadOnlyList<(string Label, string Value)> Facts,
    IReadOnlyList<string> Outro,
    string? ButtonLabel,
    string? ButtonUrl,
    string ButtonFallbackLabel,
    string? ManageLabel,
    string? ManageUrl,
    IReadOnlyList<string> FooterLines,
    CalendarBlock? Calendar = null);

/// <summary>Quiet links below the main button: the same appointment as an "add event" link of web calendars, and a note about the attached file.</summary>
public sealed record CalendarBlock(string Heading, IReadOnlyList<(string Label, string Url)> Links, string Note);

public sealed record RenderedEmail(string Subject, string Html, string Text);

public interface IEmailRenderer
{
    /// <summary>Renders a queued mail from its template key, language and JSON payload. Throws for an unknown key or a payload that cannot be read.</summary>
    RenderedEmail Render(string type, string language, string? payloadJson);
}
