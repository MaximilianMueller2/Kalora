using FluentValidation;
using FluentValidation.Results;

namespace Kalora.Application.Common;

internal static class ValidationExtensions
{
    public const int MinPasswordLength = 10;

    public static readonly string[] SupportedLanguages = ["de", "en", "tr"];

    /// <summary>The first failure becomes the <see cref="Error"/>: its code is the translation key, its property the field.</summary>
    public static Error ToError(this ValidationResult result)
    {
        var failure = result.Errors[0];
        return Error.Validation(failure.ErrorCode, ToCamelCase(failure.PropertyName));
    }

    public static IRuleBuilderOptions<T, string> ValidEmail<T>(this IRuleBuilder<T, string> rule) => rule
        .NotEmpty().WithErrorCode("validation.required")
        .MaximumLength(320).WithErrorCode("validation.too_long")
        .EmailAddress().WithErrorCode("validation.email_invalid");

    public static IRuleBuilderOptions<T, string> ValidPassword<T>(this IRuleBuilder<T, string> rule) => rule
        .NotEmpty().WithErrorCode("validation.required")
        .MinimumLength(MinPasswordLength).WithErrorCode("auth.password_too_short")
        .MaximumLength(128).WithErrorCode("validation.too_long");

    public static IRuleBuilderOptions<T, string> SupportedLanguage<T>(this IRuleBuilder<T, string> rule) => rule
        .Must(language => SupportedLanguages.Contains(language)).WithErrorCode("validation.language_unsupported");

    private static string ToCamelCase(string name) =>
        name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name[1..];
}
