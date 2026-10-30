using System.Reflection;
using System.Text.Json;

namespace Kalora.Application.Email;

/// <summary>The words of one mail template in one language.</summary>
internal sealed class MailTexts
{
    public string Subject { get; set; } = string.Empty;

    public string Heading { get; set; } = string.Empty;

    public List<string> Intro { get; set; } = [];

    public string? Button { get; set; }

    /// <summary>Added when the payload has a response deadline.</summary>
    public string? Deadline { get; set; }

    /// <summary>Added when the payload has a reason.</summary>
    public string? ReasonLine { get; set; }

    public List<string> Outro { get; set; } = [];
}

internal sealed class LanguageTexts
{
    public string[] Weekdays { get; set; } = [];

    public string[] Months { get; set; } = [];

    /// <summary>Date and time with the tokens {weekday} {day} {month} {year} {hh} {mm} {h12} {ampm}.</summary>
    public string DateFormat { get; set; } = string.Empty;

    public Dictionary<string, string> Labels { get; set; } = [];

    public Dictionary<string, string> Status { get; set; } = [];

    public Dictionary<string, string> Layout { get; set; } = [];

    public Dictionary<string, MailTexts> Mails { get; set; } = [];
}

/// <summary>Texts per language from embedded resources; an unsupported language falls back to German.</summary>
internal static class EmailTexts
{
    public const string FallbackLanguage = "de";

    public static readonly string[] Languages = ["de", "en", "tr"];

    private static readonly Lazy<Dictionary<string, LanguageTexts>> Loaded = new(() =>
        Languages.ToDictionary(language => language, Load));

    public static LanguageTexts For(string language) =>
        Loaded.Value.TryGetValue(language, out var texts) ? texts : Loaded.Value[FallbackLanguage];

    public static string ResolveLanguage(string language) => Loaded.Value.ContainsKey(language) ? language : FallbackLanguage;

    private static LanguageTexts Load(string language)
    {
        var resource = $"Kalora.Application.Email.Texts.{language}.json";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Embedded resource {resource} not found.");
        return JsonSerializer.Deserialize<LanguageTexts>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException($"The mail texts for '{language}' are empty.");
    }
}
