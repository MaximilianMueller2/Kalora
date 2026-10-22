using System.Reflection;
using System.Text.Json;
using Kalora.Domain.Salons;

namespace Kalora.Application.Catalog;

public sealed record TemplateService(
    Dictionary<string, string> Name,
    int PriceCents,
    bool PriceIsFrom,
    int DurationMinutes,
    int BufferBeforeMinutes,
    int BufferAfterMinutes);

public sealed record TemplateCategory(Dictionary<string, string> Name, List<TemplateService> Services);

/// <summary>Typical categories and services per industry, with names in all supported languages.</summary>
public static class ServiceTemplates
{
    private const string Resource = "Kalora.Application.Catalog.service-templates.json";
    private const string FallbackLanguage = "de";

    private static readonly Lazy<Dictionary<SalonIndustry, List<TemplateCategory>>> Templates = new(Load);

    public static IReadOnlyList<TemplateCategory> For(SalonIndustry industry) =>
        Templates.Value.TryGetValue(industry, out var categories) ? categories : [];

    /// <summary>The name in the given language, or German if that translation is missing.</summary>
    public static string Localize(Dictionary<string, string> names, string language) =>
        names.TryGetValue(language, out var name) ? name : names[FallbackLanguage];

    private static Dictionary<SalonIndustry, List<TemplateCategory>> Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(Resource)
            ?? throw new InvalidOperationException($"Embedded resource {Resource} not found.");
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        return JsonSerializer.Deserialize<Dictionary<SalonIndustry, List<TemplateCategory>>>(stream, options)
            ?? throw new InvalidOperationException("The service templates are empty.");
    }
}
