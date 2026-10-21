using Microsoft.Extensions.Options;

namespace Kalora.Application.Configuration;

public sealed class AppOptions
{
    public const string SectionName = "App";

    /// <summary>Public origin of the web app, used for links in e-mails (e.g. https://kalora.app).</summary>
    public string PublicBaseUrl { get; set; } = string.Empty;
}

internal sealed class AppOptionsValidator : IValidateOptions<AppOptions>
{
    public ValidateOptionsResult Validate(string? name, AppOptions options)
    {
        var isValid = Uri.TryCreate(options.PublicBaseUrl, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https";

        return isValid
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail($"{AppOptions.SectionName}:{nameof(AppOptions.PublicBaseUrl)} must be an absolute http(s) URL.");
    }
}
