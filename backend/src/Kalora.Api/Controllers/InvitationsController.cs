using Kalora.Api.Http;
using Kalora.Application.Auth;
using Kalora.Application.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kalora.Api.Controllers;

[Route("api/invitations")]
[Authorize(Policy = AuthPolicies.SalonManager)]
public sealed class InvitationsController(IInvitationService invitations) : ApiControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateInvitationRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await invitations.CreateAsync(request, cancellationToken));
}

/// <summary>Opened from the e-mail link by someone without an account; the token identifies the invitation.</summary>
[Route("api/public/invitations/{token}")]
public sealed class PublicInvitationsController(IInvitationService invitations) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Preview(string token, CancellationToken cancellationToken) =>
        ToActionResult(await invitations.PreviewAsync(token, cancellationToken));

    [HttpPost("accept")]
    public async Task<IActionResult> Accept(string token, AcceptInvitationRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await invitations.AcceptAsync(token, request, cancellationToken));
}
