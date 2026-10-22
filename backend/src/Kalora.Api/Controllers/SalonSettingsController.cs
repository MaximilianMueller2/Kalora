using Kalora.Api.Http;
using Kalora.Application.Identity;
using Kalora.Application.Salons;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kalora.Api.Controllers;

[Route("api/settings")]
[Authorize(Policy = AuthPolicies.SalonManager)]
public sealed class SalonSettingsController(ISalonSettingsService settings) : ApiControllerBase
{
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken) =>
        ToActionResult(await settings.GetProfileAsync(cancellationToken));

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(SalonProfile profile, CancellationToken cancellationToken) =>
        ToActionResult(await settings.UpdateProfileAsync(profile, cancellationToken));

    [HttpGet("booking-rules")]
    public async Task<IActionResult> GetBookingRules(CancellationToken cancellationToken) =>
        ToActionResult(await settings.GetBookingRulesAsync(cancellationToken));

    [HttpPut("booking-rules")]
    public async Task<IActionResult> UpdateBookingRules(BookingRules rules, CancellationToken cancellationToken) =>
        ToActionResult(await settings.UpdateBookingRulesAsync(rules, cancellationToken));

    [HttpGet("slug-suggestion")]
    public async Task<IActionResult> SuggestSlug([FromQuery] string name, CancellationToken cancellationToken) =>
        ToActionResult(await settings.SuggestSlugAsync(name, cancellationToken));
}
