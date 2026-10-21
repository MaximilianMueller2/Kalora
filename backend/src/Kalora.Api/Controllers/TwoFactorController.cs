using Kalora.Api.Http;
using Kalora.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kalora.Api.Controllers;

[Route("api/auth/two-factor")]
[Authorize]
public sealed class TwoFactorController(ITwoFactorService twoFactor) : ApiControllerBase
{
    [HttpPost("setup")]
    public async Task<IActionResult> Setup() => ToActionResult(await twoFactor.BeginSetupAsync());

    [HttpPost("enable")]
    public async Task<IActionResult> Enable(TwoFactorCodeRequest request) => ToActionResult(await twoFactor.EnableAsync(request));

    [HttpPost("disable")]
    public async Task<IActionResult> Disable(PasswordConfirmationRequest request) =>
        ToActionResult(await twoFactor.DisableAsync(request));

    [HttpPost("recovery-codes")]
    public async Task<IActionResult> RecoveryCodes(PasswordConfirmationRequest request) =>
        ToActionResult(await twoFactor.RegenerateRecoveryCodesAsync(request));
}
