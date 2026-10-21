using FluentValidation;
using Kalora.Application.Common;
using Kalora.Application.Configuration;
using Kalora.Application.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kalora.Application.Auth;

internal sealed class AuthService(
    UserManager<AppUser> users,
    IAppDbContext db,
    ISessionService session,
    ICurrentUser currentUser,
    IEmailOutbox outbox,
    MeResponseFactory meFactory,
    IValidator<LoginRequest> loginValidator,
    IValidator<ForgotPasswordRequest> forgotValidator,
    IValidator<ResetPasswordRequest> resetValidator,
    IOptions<AppOptions> appOptions) : IAuthService
{
    public async Task<Result<MeResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var validation = loginValidator.Validate(request);
        if (!validation.IsValid)
        {
            return validation.ToError();
        }

        var user = await users.FindByEmailAsync(EmailAddresses.Normalize(request.Email));
        if (user is null)
        {
            return InvalidCredentials;
        }

        if (await users.IsLockedOutAsync(user))
        {
            return LockedOut;
        }

        if (!await users.CheckPasswordAsync(user, request.Password))
        {
            return await RegisterFailureAsync(user, InvalidCredentials);
        }

        // The second factor is only asked for once the password is right, so it is not revealed to anyone guessing.
        if (user.TwoFactorEnabled)
        {
            var second = await VerifySecondFactorAsync(user, request);
            if (second is not null)
            {
                return second.Type == ErrorType.Unauthorized && second.Code == "auth.two_factor_required"
                    ? second
                    : await RegisterFailureAsync(user, second);
            }
        }

        await users.ResetAccessFailedCountAsync(user);
        await session.SignInAsync(user, request.RememberMe);
        return await meFactory.BuildAsync(user, cancellationToken);
    }

    public async Task<Result> LogoutAsync()
    {
        await session.SignOutAsync();
        return Result.Success();
    }

    public async Task<Result<MeResponse>> GetMeAsync(CancellationToken cancellationToken = default)
    {
        var user = currentUser.UserId is { } id ? await users.FindByIdAsync(id.ToString()) : null;
        return user is null ? Error.Unauthorized() : await meFactory.BuildAsync(user, cancellationToken);
    }

    public async Task<Result> ConfirmEmailAsync(ConfirmEmailRequest request)
    {
        var user = await users.FindByIdAsync(request.UserId.ToString());
        if (user is null || string.IsNullOrWhiteSpace(request.Token))
        {
            return InvalidToken;
        }

        var confirmed = await users.ConfirmEmailAsync(user, request.Token);
        return confirmed.Succeeded ? Result.Success() : InvalidToken;
    }

    public async Task<Result> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var validation = forgotValidator.Validate(request);
        if (!validation.IsValid)
        {
            return validation.ToError();
        }

        var email = EmailAddresses.Normalize(request.Email);
        var user = await users.FindByEmailAsync(email);
        var salonId = user is null
            ? null
            : await db.AllTenants<Domain.Salons.SalonMember>()
                .Where(m => m.UserId == user.Id)
                .Select(m => (Guid?)m.SalonId)
                .FirstOrDefaultAsync(cancellationToken);

        // Accounts without a salon (platform admins) cannot receive queued mail yet: the outbox is salon-owned.
        if (user is not null && salonId is { } id)
        {
            var token = await users.GeneratePasswordResetTokenAsync(user);
            outbox.Enqueue(id, email, EmailTypes.ResetPassword, user.PreferredLanguage, new
            {
                user.DisplayName,
                ResetUrl = AppLinks.ResetPassword(appOptions.Value.PublicBaseUrl, email, token),
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }

    public async Task<Result> ResetPasswordAsync(ResetPasswordRequest request)
    {
        var validation = resetValidator.Validate(request);
        if (!validation.IsValid)
        {
            return validation.ToError();
        }

        var user = await users.FindByEmailAsync(EmailAddresses.Normalize(request.Email));
        if (user is null)
        {
            return InvalidToken;
        }

        var reset = await users.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!reset.Succeeded)
        {
            return reset.ToError();
        }

        await users.SetLockoutEndDateAsync(user, null);
        await users.ResetAccessFailedCountAsync(user);
        return Result.Success();
    }

    /// <summary>Returns null when the second factor is valid, otherwise the error to report.</summary>
    private async Task<Error?> VerifySecondFactorAsync(AppUser user, LoginRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.TwoFactorCode))
        {
            var code = request.TwoFactorCode.Replace(" ", string.Empty).Replace("-", string.Empty);
            var valid = await users.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, code);
            return valid ? null : InvalidTwoFactorCode;
        }

        if (!string.IsNullOrWhiteSpace(request.RecoveryCode))
        {
            var redeemed = await users.RedeemTwoFactorRecoveryCodeAsync(user, request.RecoveryCode.Trim());
            return redeemed.Succeeded ? null : InvalidTwoFactorCode;
        }

        return Error.Unauthorized("auth.two_factor_required");
    }

    private async Task<Error> RegisterFailureAsync(AppUser user, Error error)
    {
        await users.AccessFailedAsync(user);
        return await users.IsLockedOutAsync(user) ? LockedOut : error;
    }

    private static Error InvalidCredentials => Error.Unauthorized("auth.invalid_credentials");

    private static Error LockedOut => Error.Unauthorized("auth.locked_out");

    private static Error InvalidTwoFactorCode => Error.Unauthorized("auth.invalid_two_factor_code");

    private static Error InvalidToken => Error.Validation("auth.invalid_token");
}
