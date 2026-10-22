using FluentValidation;
using Kalora.Application.Common;

namespace Kalora.Application.Catalog;

internal static class CatalogRules
{
    public const int MaxPriceCents = 1_000_000;
    public const int MaxDurationMinutes = 720;
    public const int MaxBufferMinutes = 240;
    public const int Step = 5;

    public static bool IsValidDuration(int minutes) => minutes is > 0 and <= MaxDurationMinutes && minutes % Step == 0;

    public static bool IsValidBuffer(int minutes) => minutes is >= 0 and <= MaxBufferMinutes && minutes % Step == 0;
}

internal sealed class SaveCategoryRequestValidator : AbstractValidator<SaveCategoryRequest>
{
    public SaveCategoryRequestValidator() =>
        RuleFor(r => r.Name).NotEmpty().WithErrorCode("validation.required")
            .MaximumLength(100).WithErrorCode("validation.too_long");
}

internal sealed class SaveServiceRequestValidator : AbstractValidator<SaveServiceRequest>
{
    public SaveServiceRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().WithErrorCode("validation.required")
            .MaximumLength(200).WithErrorCode("validation.too_long");
        RuleFor(r => r.Description).MaximumLength(2000).WithErrorCode("validation.too_long");
        RuleFor(r => r.PriceCents).InclusiveBetween(0, CatalogRules.MaxPriceCents).WithErrorCode("service.price_invalid");
        RuleFor(r => r.DurationMinutes).Must(CatalogRules.IsValidDuration).WithErrorCode("service.duration_invalid");
        RuleFor(r => r.BufferBeforeMinutes).Must(CatalogRules.IsValidBuffer).WithErrorCode("service.buffer_invalid");
        RuleFor(r => r.BufferAfterMinutes).Must(CatalogRules.IsValidBuffer).WithErrorCode("service.buffer_invalid");
    }
}

internal sealed class ApplyTemplateRequestValidator : AbstractValidator<ApplyTemplateRequest>
{
    public ApplyTemplateRequestValidator() =>
        RuleFor(r => r.Industry).Must(industry => industry is null || Enum.IsDefined(industry.Value)).WithErrorCode("validation.invalid");
}
