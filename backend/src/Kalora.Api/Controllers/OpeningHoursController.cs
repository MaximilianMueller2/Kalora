using Kalora.Api.Http;
using Kalora.Application.Identity;
using Kalora.Application.Salons;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kalora.Api.Controllers;

[Route("api/settings")]
[Authorize(Policy = AuthPolicies.SalonManager)]
public sealed class OpeningHoursController(IOpeningHoursService openingHours) : ApiControllerBase
{
    [HttpGet("opening-hours")]
    public async Task<IActionResult> GetPlan(CancellationToken cancellationToken) =>
        ToActionResult(await openingHours.GetPlanAsync(cancellationToken));

    [HttpPut("opening-hours")]
    public async Task<IActionResult> ReplacePlan(OpeningHoursPlan plan, CancellationToken cancellationToken) =>
        ToActionResult(await openingHours.ReplacePlanAsync(plan, cancellationToken));

    [HttpGet("closures")]
    public async Task<IActionResult> ListClosures(CancellationToken cancellationToken) =>
        ToActionResult(await openingHours.ListClosuresAsync(cancellationToken));

    [HttpPost("closures")]
    public async Task<IActionResult> CreateClosures(CreateClosureRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await openingHours.CreateClosuresAsync(request, cancellationToken));

    [HttpDelete("closures/{id:guid}")]
    public async Task<IActionResult> DeleteClosure(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await openingHours.DeleteClosureAsync(id, cancellationToken));
}
