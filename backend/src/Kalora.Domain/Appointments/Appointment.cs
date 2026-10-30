using Kalora.Domain.Common;

namespace Kalora.Domain.Appointments;

public class Appointment : Entity, ISalonOwned
{
    public Guid SalonId { get; set; }

    public Guid StaffId { get; set; }

    public Guid ServiceId { get; set; }

    public Guid CustomerId { get; set; }

    /// <summary>Start and end of the service itself, without buffers.</summary>
    public DateTimeOffset StartUtc { get; set; }

    public DateTimeOffset EndUtc { get; set; }

    /// <summary>
    /// Range occupied in the calendar: service plus buffer before and after. The database derives the
    /// <c>blocked_range</c> column from it and forbids overlaps per staff member for active appointments.
    /// </summary>
    public DateTimeOffset BlockedStartUtc { get; set; }

    public DateTimeOffset BlockedEndUtc { get; set; }

    public AppointmentStatus Status { get; set; } = AppointmentStatus.Requested;

    public AppointmentSource Source { get; set; }

    /// <summary>Language of the booking; used for all e-mails about it.</summary>
    public string Language { get; set; } = "de";

    // Copy of the service data at booking time, so later price changes do not alter past appointments.
    public string ServiceName { get; set; } = string.Empty;

    public int PriceCents { get; set; }

    public bool PriceIsFrom { get; set; }

    public string Currency { get; set; } = "EUR";

    public int DurationMinutes { get; set; }

    public string? CustomerNote { get; set; }

    public string? InternalNote { get; set; }

    /// <summary>
    /// Version of the calendar entry (iCalendar SEQUENCE): raised whenever the time changes, so calendars that
    /// already hold the appointment replace it instead of adding a second one.
    /// </summary>
    public int CalendarSequence { get; set; }

    /// <summary>SHA-256 of the management link token; the token itself is never stored.</summary>
    public string? ManageTokenHash { get; set; }

    /// <summary>Until when the salon may answer a request; after that it expires.</summary>
    public DateTimeOffset? ExpiresAtUtc { get; set; }

    public DateTimeOffset? ConfirmedAtUtc { get; set; }

    public DateTimeOffset? DeclinedAtUtc { get; set; }

    public DateTimeOffset? ExpiredAtUtc { get; set; }

    public DateTimeOffset? CancelledAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public DateTimeOffset? NoShowAtUtc { get; set; }

    /// <summary>When the reminder mail was queued; set once, so the scheduler never sends it twice.</summary>
    public DateTimeOffset? ReminderSentAtUtc { get; set; }

    /// <summary>Moves to <paramref name="to"/> if the status rules allow it and records when. Returns false and changes nothing otherwise.</summary>
    public bool TryMoveTo(AppointmentStatus to, DateTimeOffset now)
    {
        if (!AppointmentStatusRules.CanMove(Status, to))
        {
            return false;
        }

        Status = to;
        switch (to)
        {
            case AppointmentStatus.Confirmed:
                ConfirmedAtUtc = now;
                ExpiresAtUtc = null;
                break;
            case AppointmentStatus.Declined:
                DeclinedAtUtc = now;
                break;
            case AppointmentStatus.Expired:
                ExpiredAtUtc = now;
                break;
            case AppointmentStatus.Cancelled:
                CancelledAtUtc = now;
                break;
            case AppointmentStatus.Completed:
                CompletedAtUtc = now;
                break;
            case AppointmentStatus.NoShow:
                NoShowAtUtc = now;
                break;
        }

        return true;
    }
}
