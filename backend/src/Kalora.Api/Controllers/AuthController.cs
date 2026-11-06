using Kalora.Api.Http;
using Kalora.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Kalora.Api.Controllers;

[Route("api/auth")]
[AllowWhenReadOnly]
[AllowAnonymous]
public sealed class AuthController(IRegistrationService registration, IAuthService auth) : ApiControllerBase
{
    [HttpPost("register")]
    [EnableRateLimiting(PublicRateLimits.Register)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await registration.RegisterOwnerAsync(request, cancellationToken));

    [HttpPost("login")]
    [EnableRateLimiting(PublicRateLimits.Login)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await auth.LoginAsync(request, cancellationToken));

    [HttpPost("logout")]
    public async Task<IActionResult> Logout() => ToActionResult(await auth.LogoutAsync());

    [HttpPost("confirm-email")]
    [EnableRateLimiting(PublicRateLimits.Recovery)]
    public async Task<IActionResult> ConfirmEmail(ConfirmEmailRequest request) =>
        ToActionResult(await auth.ConfirmEmailAsync(request));

    [HttpPost("forgot-password")]
    [EnableRateLimiting(PublicRateLimits.Recovery)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await auth.ForgotPasswordAsync(request, cancellationToken));

    [HttpPost("reset-password")]
    [EnableRateLimiting(PublicRateLimits.Recovery)]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request) =>
        ToActionResult(await auth.ResetPasswordAsync(request));
}
