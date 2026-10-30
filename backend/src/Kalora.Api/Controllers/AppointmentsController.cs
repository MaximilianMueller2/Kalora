using Kalora.Api.Http;
using Kalora.Application.Appointments;
using Kalora.Application.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kalora.Api.Controllers;

/// <summary>The salon's appointments. Which ones a person may see and change is decided by the service.</summary>
[Route("api/appointments")]
[Authorize(Policy = AuthPolicies.SalonMember)]
public sealed class AppointmentsController(IAppointmentService appointments) : ApiControllerBase
{
    [HttpGet("requests")]
    public async Task<IActionResult> OpenRequests(CancellationToken cancellationToken) =>
        ToActionResult(await appointments.ListOpenRequestsAsync(cancellationToken));

    [HttpGet("requests/count")]
    public async Task<IActionResult> OpenRequestCount(CancellationToken cancellationToken) =>
        ToActionResult(await appointments.CountOpenRequestsAsync(cancellationToken));

    [HttpGet("awaiting-outcome")]
    public async Task<IActionResult> AwaitingOutcome(CancellationToken cancellationToken) =>
        ToActionResult(await appointments.ListAwaitingOutcomeAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await appointments.GetAsync(id, cancellationToken));

    [HttpPost("{id:guid}/confirm")]
    public async Task<IActionResult> Confirm(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await appointments.ConfirmAsync(id, cancellationToken));

    [HttpPost("{id:guid}/decline")]
    public async Task<IActionResult> Decline(Guid id, ReasonRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await appointments.DeclineAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, ReasonRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await appointments.CancelAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/attended")]
    public async Task<IActionResult> Attended(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await appointments.MarkAttendedAsync(id, cancellationToken));

    [HttpPost("{id:guid}/no-show")]
    public async Task<IActionResult> NoShow(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await appointments.MarkNoShowAsync(id, cancellationToken));
}
