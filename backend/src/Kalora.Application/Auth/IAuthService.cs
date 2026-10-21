using Kalora.Application.Common;

namespace Kalora.Application.Auth;

public interface IAuthService
{
    Task<Result<MeResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    Task<Result> LogoutAsync();

    Task<Result<MeResponse>> GetMeAsync(CancellationToken cancellationToken = default);

    Task<Result> ConfirmEmailAsync(ConfirmEmailRequest request);

    /// <summary>Always succeeds, so the response never reveals whether an e-mail address has an account.</summary>
    Task<Result> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default);

    Task<Result> ResetPasswordAsync(ResetPasswordRequest request);
}
