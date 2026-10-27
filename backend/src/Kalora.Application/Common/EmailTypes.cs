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
}
