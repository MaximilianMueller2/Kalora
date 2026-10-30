using FluentValidation;
using Kalora.Application.Common;

namespace Kalora.Application.Calendar;

internal sealed class CreateManualAppointmentRequestValidator : AbstractValidator<CreateManualAppointmentRequest>
{
    public CreateManualAppointmentRequestValidator()
    {
        RuleFor(r => r.ServiceId).NotEmpty().WithErrorCode("validation.required");
        RuleFor(r => r.StaffId).NotEmpty().WithErrorCode("validation.required");
        RuleFor(r => r.StartUtc).NotEqual(default(DateTimeOffset)).WithErrorCode("validation.required");
        RuleFor(r => r.CustomerName).NotEmpty().When(r => r.CustomerId is null).WithErrorCode("validation.required");
        RuleFor(r => r.CustomerName).MaximumLength(100).WithErrorCode("validation.too_long");
        RuleFor(r => r.CustomerEmail!).ValidEmail().When(r => !string.IsNullOrWhiteSpace(r.CustomerEmail));
        RuleFor(r => r.CustomerPhone).MaximumLength(40).WithErrorCode("validation.too_long");
        RuleFor(r => r.Note).MaximumLength(2000).WithErrorCode("validation.too_long");
    }
}

internal sealed class MoveAppointmentRequestValidator : AbstractValidator<MoveAppointmentRequest>
{
    public MoveAppointmentRequestValidator()
    {
        RuleFor(r => r.StartUtc).NotEqual(default(DateTimeOffset)).WithErrorCode("validation.required");
        RuleFor(r => r.Reason).MaximumLength(500).WithErrorCode("validation.too_long");
    }
}

internal sealed class CreateBlockRequestValidator : AbstractValidator<CreateBlockRequest>
{
    public const int MaxDays = 14;

    public CreateBlockRequestValidator()
    {
        RuleFor(r => r.StaffId).NotEmpty().WithErrorCode("validation.required");
        RuleFor(r => r.EndUtc).GreaterThan(r => r.StartUtc).WithErrorCode("calendar.block_range_invalid");
        RuleFor(r => r).Must(r => r.EndUtc - r.StartUtc <= TimeSpan.FromDays(MaxDays)).WithErrorCode("calendar.block_range_invalid").OverridePropertyName(nameof(CreateBlockRequest.EndUtc));
        RuleFor(r => r.Label).MaximumLength(100).WithErrorCode("validation.too_long");
    }
}
