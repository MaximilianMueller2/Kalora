using Kalora.Application.Common;

namespace Kalora.Application.Auth;

/// <summary>Two-factor authentication with an authenticator app (TOTP) for the signed-in user.</summary>
public interface ITwoFactorService
{
    Task<Result<TwoFactorSetupResponse>> BeginSetupAsync();

    Task<Result<RecoveryCodesResponse>> EnableAsync(TwoFactorCodeRequest request);

    Task<Result> DisableAsync(PasswordConfirmationRequest request);

    Task<Result<RecoveryCodesResponse>> RegenerateRecoveryCodesAsync(PasswordConfirmationRequest request);
}
