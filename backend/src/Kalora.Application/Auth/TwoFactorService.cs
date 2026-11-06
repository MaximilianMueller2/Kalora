using Kalora.Application.Common;
using Kalora.Application.Dashboard;
using Kalora.Application.Identity;
using Microsoft.AspNetCore.Identity;

namespace Kalora.Application.Auth;

internal sealed class TwoFactorService(UserManager<AppUser> users, ICurrentUser currentUser, ISessionService session, IQrCodeService qr) : ITwoFactorService
{
    private const string Issuer = "Kalora";
    private const int RecoveryCodeCount = 10;
    private const int MaxCodeLength = 20;
    private const int MaxPasswordLength = 128;

    public async Task<Result<TwoFactorSetupResponse>> BeginSetupAsync()
    {
        var user = await GetUserAsync();
        if (user is null)
        {
            return Error.Unauthorized();
        }

        if (user.TwoFactorEnabled)
        {
            return Error.Conflict("auth.two_factor_already_enabled");
        }

        var key = await users.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key))
        {
            await users.ResetAuthenticatorKeyAsync(user);
            key = await users.GetAuthenticatorKeyAsync(user);
        }

        var uri = $"otpauth://totp/{Uri.EscapeDataString(Issuer)}:{Uri.EscapeDataString(user.Email ?? string.Empty)}"
            + $"?secret={key}&issuer={Uri.EscapeDataString(Issuer)}&digits=6";
        return new TwoFactorSetupResponse(key!, uri, Convert.ToBase64String(qr.CreatePng(uri)));
    }

    public async Task<Result<RecoveryCodesResponse>> EnableAsync(TwoFactorCodeRequest request)
    {
        var user = await GetUserAsync();
        if (user is null)
        {
            return Error.Unauthorized();
        }

        var code = request.Code.Replace(" ", string.Empty).Replace("-", string.Empty);
        if (code.Length > MaxCodeLength || !await users.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, code))
        {
            return Error.Validation("auth.invalid_two_factor_code", "code");
        }

        await users.SetTwoFactorEnabledAsync(user, true);
        var codes = await users.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount);
        await session.RefreshAsync(user);
        return new RecoveryCodesResponse(codes!.ToList());
    }

    public async Task<Result> DisableAsync(PasswordConfirmationRequest request)
    {
        var user = await GetUserAsync();
        if (user is null)
        {
            return Error.Unauthorized();
        }

        var password = await CheckPasswordAsync(user, request.Password);
        if (!password.IsSuccess)
        {
            return password.Error;
        }

        await users.SetTwoFactorEnabledAsync(user, false);
        await users.ResetAuthenticatorKeyAsync(user);
        await session.RefreshAsync(user);
        return Result.Success();
    }

    public async Task<Result<RecoveryCodesResponse>> RegenerateRecoveryCodesAsync(PasswordConfirmationRequest request)
    {
        var user = await GetUserAsync();
        if (user is null)
        {
            return Error.Unauthorized();
        }

        if (!user.TwoFactorEnabled)
        {
            return Error.Conflict("auth.two_factor_not_enabled");
        }

        var password = await CheckPasswordAsync(user, request.Password);
        if (!password.IsSuccess)
        {
            return password.Error;
        }

        var codes = await users.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount);
        return new RecoveryCodesResponse(codes!.ToList());
    }

    /// <summary>
    /// Checks the password that confirms a security change. Wrong ones count like wrong passwords at sign-in, so a stolen session cannot
    /// be used to guess the password: after five the account is locked, here as there.
    /// </summary>
    private async Task<Result> CheckPasswordAsync(AppUser user, string password)
    {
        if (await users.IsLockedOutAsync(user))
        {
            return Error.Unauthorized("auth.locked_out");
        }

        if (password.Length <= MaxPasswordLength && await users.CheckPasswordAsync(user, password))
        {
            await users.ResetAccessFailedCountAsync(user);
            return Result.Success();
        }

        await users.AccessFailedAsync(user);
        return await users.IsLockedOutAsync(user) ? Error.Unauthorized("auth.locked_out") : Error.Validation("auth.invalid_credentials", "password");
    }

    private async Task<AppUser?> GetUserAsync() =>
        currentUser.UserId is { } id ? await users.FindByIdAsync(id.ToString()) : null;
}
