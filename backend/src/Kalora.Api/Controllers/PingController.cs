using Kalora.Api.Http;
using Kalora.Application.Ping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kalora.Api.Controllers;

[Route("api/ping")]
[AllowAnonymous]
public sealed class PingController(IPingService pingService) : ApiControllerBase
{
    [HttpGet]
    public IActionResult Get() => ToActionResult(pingService.Ping());
}
