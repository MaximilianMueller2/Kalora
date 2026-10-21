using FluentValidation;
using Kalora.Application.Common;

namespace Kalora.Application.Auth;

internal sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(r => r.SalonName).NotEmpty().WithErrorCode("validation.required")
            .MaximumLength(200).WithErrorCode("validation.too_long");
        RuleFor(r => r.Industry).IsInEnum().WithErrorCode("validation.invalid");
        RuleFor(r => r.Name).NotEmpty().WithErrorCode("validation.required")
            .MaximumLength(200).WithErrorCode("validation.too_long");
        RuleFor(r => r.Email).ValidEmail();
        RuleFor(r => r.Password).ValidPassword();
        RuleFor(r => r.Language).SupportedLanguage();
        RuleFor(r => r.AcceptTerms).Equal(true).WithErrorCode("auth.terms_not_accepted");
    }
}

internal sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(r => r.Email).NotEmpty().WithErrorCode("validation.required");
        RuleFor(r => r.Password).NotEmpty().WithErrorCode("validation.required");
    }
}

internal sealed class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator() => RuleFor(r => r.Email).ValidEmail();
}

internal sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(r => r.Email).ValidEmail();
        RuleFor(r => r.Token).NotEmpty().WithErrorCode("auth.invalid_token");
        RuleFor(r => r.NewPassword).ValidPassword();
    }
}

internal sealed class CreateInvitationRequestValidator : AbstractValidator<CreateInvitationRequest>
{
    public CreateInvitationRequestValidator()
    {
        RuleFor(r => r.Email).ValidEmail();
        RuleFor(r => r.Role).Must(role => role is Domain.Salons.SalonRole.Manager or Domain.Salons.SalonRole.Staff)
            .WithErrorCode("invitation.role_invalid");
    }
}

internal sealed class AcceptInvitationRequestValidator : AbstractValidator<AcceptInvitationRequest>
{
    public AcceptInvitationRequestValidator()
    {
        RuleFor(r => r.Password).ValidPassword();
        RuleFor(r => r.Language).SupportedLanguage();
    }
}
