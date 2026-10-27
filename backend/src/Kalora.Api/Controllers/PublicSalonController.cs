using Kalora.Api.Http;
using Kalora.Application.PublicBooking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Kalora.Api.Controllers;

/// <summary>
/// The public booking page of a salon: no login, no customer data in any answer. The salon is the one named by
/// the slug in the URL (resolved by the tenant middleware); the slug parameter itself is not used here.
/// </summary>
[Route("api/public/salons/{slug}")]
[AllowAnonymous]
public sealed class PublicSalonController(IPublicSalonService salon, IBookingService booking) : ApiControllerBase
{
    [HttpGet]
    [EnableRateLimiting(PublicRateLimits.Read)]
    public async Task<IActionResult> Profile(string slug, CancellationToken cancellationToken) =>
        ToActionResult(await salon.GetProfileAsync(cancellationToken));

    [HttpGet("services")]
    [EnableRateLimiting(PublicRateLimits.Read)]
    public async Task<IActionResult> Services(string slug, CancellationToken cancellationToken) =>
        ToActionResult(await salon.GetServicesAsync(cancellationToken));

    [HttpGet("staff")]
    [EnableRateLimiting(PublicRateLimits.Read)]
    public async Task<IActionResult> Staff(string slug, [FromQuery] Guid serviceId, CancellationToken cancellationToken) =>
        ToActionResult(await salon.GetStaffAsync(serviceId, cancellationToken));

    [HttpGet("availability/days")]
    [EnableRateLimiting(PublicRateLimits.Availability)]
    public async Task<IActionResult> Days(string slug, [FromQuery] Guid serviceId, [FromQuery] Guid? staffId, [FromQuery] string month, CancellationToken cancellationToken) =>
        ToActionResult(await salon.GetDaysAsync(serviceId, staffId, month, cancellationToken));

    [HttpGet("availability/slots")]
    [EnableRateLimiting(PublicRateLimits.Availability)]
    public async Task<IActionResult> Slots(string slug, [FromQuery] Guid serviceId, [FromQuery] Guid? staffId, [FromQuery] DateOnly date, CancellationToken cancellationToken) =>
        ToActionResult(await salon.GetSlotsAsync(serviceId, staffId, date, cancellationToken));

    /// <summary>Anonymous and without ambient credentials, so no antiforgery token is needed (like accepting an invitation).</summary>
    [HttpPost("appointment-requests")]
    [IgnoreAntiforgeryToken]
    [EnableRateLimiting(PublicRateLimits.Booking)]
    public async Task<IActionResult> RequestAppointment(string slug, CreateAppointmentRequest request, CancellationToken cancellationToken)
    {
        var result = await booking.RequestAsync(request, cancellationToken);
        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, result.Value) : ToActionResult(result);
    }
}
