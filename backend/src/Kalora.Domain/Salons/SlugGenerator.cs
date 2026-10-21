using System.Globalization;
using System.Text;

namespace Kalora.Domain.Salons;

/// <summary>Builds URL slugs ("kalora.app/&lt;slug&gt;") from salon names.</summary>
public static class SlugGenerator
{
    public const int MaxLength = 40;

    // First path segments that the web app itself uses; a salon must never shadow them.
    private static readonly HashSet<string> Reserved =
    [
        "admin", "agb", "api", "app", "assets", "auth", "dashboard", "datenschutz", "health", "impressum",
        "invitation", "login", "logout", "m", "openapi", "privacy", "register", "scalar", "settings", "static", "terms", "www",
    ];

    public static bool IsReserved(string slug) => Reserved.Contains(slug);

    public static string Slugify(string name)
    {
        var builder = new StringBuilder();
        foreach (var character in Transliterate(name.Trim().ToLowerInvariant()).Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var isAllowed = character is >= 'a' and <= 'z' or >= '0' and <= '9';
            if (isAllowed)
            {
                builder.Append(character);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        var slug = builder.ToString().Trim('-');
        if (slug.Length > MaxLength)
        {
            slug = slug[..MaxLength].TrimEnd('-');
        }

        return slug.Length >= 3 ? slug : $"{slug}-salon".Trim('-');
    }

    /// <summary>The slug itself first, then numbered variants (name-2, name-3, ...) that still fit the length limit.</summary>
    public static IEnumerable<string> Candidates(string slug)
    {
        yield return slug;
        for (var number = 2; ; number++)
        {
            var suffix = $"-{number}";
            yield return $"{slug[..Math.Min(slug.Length, MaxLength - suffix.Length)].TrimEnd('-')}{suffix}";
        }
    }

    private static string Transliterate(string text) => text
        .Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("ß", "ss")
        .Replace("ç", "c").Replace("ş", "s").Replace("ğ", "g").Replace("ı", "i").Replace("̇", string.Empty);
}
