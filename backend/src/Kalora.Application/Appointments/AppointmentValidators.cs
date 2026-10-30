using FluentValidation;

namespace Kalora.Application.Appointments;

internal sealed class ReasonRequestValidator : AbstractValidator<ReasonRequest>
{
    public const int MaxLength = 500;

    public ReasonRequestValidator() =>
        RuleFor(r => r.Reason).MaximumLength(MaxLength).WithErrorCode("validation.too_long");
}
