using Kalora.Api.Http;
using Kalora.Application.Identity;
using Kalora.Application.Onboarding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kalora.Api.Controllers;

/// <summary>Progress of the setup assistant. Owners and managers only.</summary>
[Route("api/onboarding")]
[Authorize(Policy = AuthPolicies.SalonManager)]
public sealed class OnboardingController(IOnboardingService onboarding) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        ToActionResult(await onboarding.GetStatusAsync(cancellationToken));
}
