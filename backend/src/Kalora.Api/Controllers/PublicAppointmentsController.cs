using System.Text;
using Kalora.Api.Http;
using Kalora.Application.Calendar;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Kalora.Api.Controllers;

/// <summary>What a customer can do with the management token of an appointment, without an account. The token is the only credential.</summary>
[Route("api/public/appointments/{token}")]
[AllowAnonymous]
public sealed class PublicAppointmentsController(IAppointmentCalendarService calendar) : ApiControllerBase
{
    private const string CalendarContentType = "text/calendar; charset=utf-8";

    [HttpGet("calendar.ics")]
    [EnableRateLimiting(PublicRateLimits.Read)]
    public async Task<IActionResult> Calendar(string token, CancellationToken cancellationToken)
    {
        var result = await calendar.GetFileAsync(token, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        // The file contains the management link, so no shared cache may keep it.
        Response.Headers.CacheControl = "no-store";
        return File(Encoding.UTF8.GetBytes(result.Value.Content), CalendarContentType, result.Value.FileName);
    }
}
