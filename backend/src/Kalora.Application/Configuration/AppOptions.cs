using Microsoft.Extensions.Options;

namespace Kalora.Application.Configuration;

public sealed class AppOptions
{
    public const string SectionName = "App";

    /// <summary>Public origin of the web app, used for links in e-mails (e.g. https://kalora.app).</summary>
    public string PublicBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Secret from which the management link of an appointment is derived (HMAC). Only the hash of the link is stored,
    /// the link itself can be recomputed for every later e-mail. Keep it secret and do not change it: a new secret
    /// invalidates all links already sent. At least <see cref="MinSecretLength"/> characters.
    /// </summary>
    public string ManageTokenSecret { get; set; } = string.Empty;

    public const int MinSecretLength = 32;
}

internal sealed class AppOptionsValidator : IValidateOptions<AppOptions>
{
    public ValidateOptionsResult Validate(string? name, AppOptions options)
    {
        var isValid = Uri.TryCreate(options.PublicBaseUrl, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https";

        var errors = new List<string>();
        if (!isValid)
        {
            errors.Add($"{AppOptions.SectionName}:{nameof(AppOptions.PublicBaseUrl)} must be an absolute http(s) URL.");
        }

        if (options.ManageTokenSecret.Length < AppOptions.MinSecretLength)
        {
            errors.Add($"{AppOptions.SectionName}:{nameof(AppOptions.ManageTokenSecret)} must have at least {AppOptions.MinSecretLength} characters.");
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
