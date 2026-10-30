namespace Kalora.Application.Common;

/// <summary>Template keys of queued e-mails.</summary>
public static class EmailTypes
{
    public const string ConfirmEmail = "auth.confirm_email";
    public const string ResetPassword = "auth.reset_password";
    public const string StaffInvitation = "auth.staff_invitation";

    /// <summary>To the customer: the request arrived and waits for the salon.</summary>
    public const string BookingRequestReceived = "booking.request_received";

    /// <summary>To the customer: the appointment is confirmed (automatically on booking, or later by the salon).</summary>
    public const string BookingConfirmed = "booking.confirmed";

    /// <summary>To the salon: a new request or appointment came in through the booking page.</summary>
    public const string BookingNewForSalon = "booking.new_for_salon";

    /// <summary>To the customer: the salon declined the request, optionally with a reason.</summary>
    public const string BookingDeclined = "booking.declined";

    /// <summary>To the customer: the salon did not answer in time.</summary>
    public const string BookingExpired = "booking.expired";

    /// <summary>To the customer: confirmation of their own cancellation.</summary>
    public const string BookingCancelledByCustomer = "booking.cancelled_by_customer";

    /// <summary>To the customer: the salon called the appointment off.</summary>
    public const string BookingCancelledBySalon = "booking.cancelled_by_salon";
    public const string BookingRescheduled = "booking.rescheduled";

    /// <summary>To the customer: shortly before the appointment.</summary>
    public const string BookingReminder = "booking.reminder";

    /// <summary>To the salon: a customer cancelled.</summary>
    public const string BookingCancellationForSalon = "booking.cancellation_for_salon";
}
