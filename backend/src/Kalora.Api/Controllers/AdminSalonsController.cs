using Kalora.Api.Http;
using Kalora.Application.Admin;
using Kalora.Application.Identity;
using Kalora.Domain.Salons;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kalora.Api.Controllers;

/// <summary>The platform admin's view on all salons: master data and subscription, no customer data.</summary>
[Route("api/admin/salons")]
[Authorize(Policy = AuthPolicies.PlatformAdmin)]
[AllowWhenReadOnly]
public sealed class AdminSalonsController(IAdminSalonService salons) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? q, [FromQuery] SalonStatus? status, CancellationToken cancellationToken) =>
        ToActionResult(await salons.ListAsync(q, status, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await salons.GetAsync(id, cancellationToken));

    [HttpPut("{id:guid}/plan")]
    public async Task<IActionResult> SetPlan(Guid id, SetPlanRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await salons.SetPlanAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/trial/extend")]
    public async Task<IActionResult> ExtendTrial(Guid id, ExtendTrialRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await salons.ExtendTrialAsync(id, request, cancellationToken));

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid id, SetStatusRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await salons.SetStatusAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/suspend")]
    public async Task<IActionResult> Suspend(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await salons.SuspendAsync(id, cancellationToken));

    [HttpPost("{id:guid}/unsuspend")]
    public async Task<IActionResult> Unsuspend(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await salons.UnsuspendAsync(id, cancellationToken));

    [HttpPut("{id:guid}/note")]
    public async Task<IActionResult> SetNote(Guid id, SetNoteRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await salons.SetNoteAsync(id, request, cancellationToken));
}
