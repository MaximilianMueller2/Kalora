using Kalora.Application.Common;

namespace Kalora.Application.Calendar;

/// <param name="Content">The text of the .ics file.</param>
public sealed record CalendarFile(string FileName, string Content);

/// <summary>The calendar file of one appointment for the customer who holds its management token.</summary>
public interface IAppointmentCalendarService
{
    /// <summary>
    /// The appointment as <c>PUBLISH</c> when it is confirmed (or took place), as <c>CANCEL</c> when it was cancelled.
    /// An unknown token and a salon that is switched off look the same: <c>appointment.not_found</c>. A request that
    /// is still open, declined or expired is <c>appointment.calendar_unavailable</c>.
    /// </summary>
    Task<Result<CalendarFile>> GetFileAsync(string token, CancellationToken cancellationToken = default);
}
