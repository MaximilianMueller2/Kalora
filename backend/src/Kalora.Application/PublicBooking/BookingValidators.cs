using FluentValidation;
using Kalora.Application.Common;
using Kalora.Application.Salons;

namespace Kalora.Application.PublicBooking;

internal sealed class CreateAppointmentRequestValidator : AbstractValidator<CreateAppointmentRequest>
{
    public CreateAppointmentRequestValidator()
    {
        RuleFor(r => r.ServiceId).NotEmpty().WithErrorCode("validation.required");
        RuleFor(r => r.StartUtc).NotEqual(default(DateTimeOffset)).WithErrorCode("validation.required");
        RuleFor(r => r.Name).NotEmpty().WithErrorCode("validation.required")
            .MaximumLength(100).WithErrorCode("validation.too_long");
        RuleFor(r => r.Email).ValidEmail();
        RuleFor(r => r.Phone)
            .Must(phone => string.IsNullOrWhiteSpace(phone) || Rules.Phone().IsMatch(phone.Trim()))
            .WithErrorCode("settings.phone_invalid");
        RuleFor(r => r.Note).MaximumLength(1000).WithErrorCode("validation.too_long");
        RuleFor(r => r.Language)
            .Must(language => string.IsNullOrEmpty(language) || ValidationExtensions.SupportedLanguages.Contains(language))
            .WithErrorCode("validation.language_unsupported");
        RuleFor(r => r.Website).MaximumLength(500).WithErrorCode("validation.too_long");
    }
}
