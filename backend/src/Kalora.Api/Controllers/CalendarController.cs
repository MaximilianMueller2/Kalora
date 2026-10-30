using Kalora.Api.Http;
using Kalora.Application.Calendar;
using Kalora.Application.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kalora.Api.Controllers;

/// <summary>The team calendar. Which columns a person may see and change is decided by the service.</summary>
[Route("api/calendar")]
[Authorize(Policy = AuthPolicies.SalonMember)]
public sealed class CalendarController(ICalendarService calendar) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] Guid[]? staffIds, CancellationToken cancellationToken) =>
        ToActionResult(await calendar.GetAsync(new CalendarQuery(from, to, staffIds), cancellationToken));

    [HttpGet("customers")]
    public async Task<IActionResult> SearchCustomers([FromQuery] string? q, CancellationToken cancellationToken) =>
        ToActionResult(await calendar.SearchCustomersAsync(q ?? string.Empty, cancellationToken));

    [HttpPost("appointments")]
    public async Task<IActionResult> CreateAppointment(CreateManualAppointmentRequest request, CancellationToken cancellationToken)
    {
        var result = await calendar.CreateAppointmentAsync(request, cancellationToken);
        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, result.Value) : ToActionResult(result);
    }

    [HttpPost("appointments/{id:guid}/move")]
    public async Task<IActionResult> Move(Guid id, MoveAppointmentRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await calendar.MoveAppointmentAsync(id, request, cancellationToken));

    [HttpPost("blocks")]
    public async Task<IActionResult> CreateBlock(CreateBlockRequest request, CancellationToken cancellationToken)
    {
        var result = await calendar.CreateBlockAsync(request, cancellationToken);
        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, result.Value) : ToActionResult(result);
    }

    [HttpDelete("blocks/{id:guid}")]
    public async Task<IActionResult> DeleteBlock(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await calendar.DeleteBlockAsync(id, cancellationToken));
}
