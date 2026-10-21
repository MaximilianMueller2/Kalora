using Kalora.Api.Http;
using Kalora.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kalora.Api.Controllers;

[Route("api/me")]
[Authorize]
public sealed class MeController(IAuthService auth) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        ToActionResult(await auth.GetMeAsync(cancellationToken));
}
