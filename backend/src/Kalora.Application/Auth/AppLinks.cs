namespace Kalora.Application.Auth;

/// <summary>Links into the web app that e-mails contain. The routes are implemented by the frontend.</summary>
internal static class AppLinks
{
    public static string ConfirmEmail(string baseUrl, Guid userId, string token) =>
        $"{Trim(baseUrl)}/auth/confirm-email?userId={userId}&token={Uri.EscapeDataString(token)}";

    public static string ResetPassword(string baseUrl, string email, string token) =>
        $"{Trim(baseUrl)}/auth/reset-password?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";

    public static string Invitation(string baseUrl, string token) =>
        $"{Trim(baseUrl)}/auth/invitation/{Uri.EscapeDataString(token)}";

    /// <summary>The customer's own page for an appointment; the token is the only credential.</summary>
    public static string ManageAppointment(string baseUrl, string salonSlug, string token) =>
        $"{Trim(baseUrl)}/{Uri.EscapeDataString(salonSlug)}/appointment/{Uri.EscapeDataString(token)}";

    /// <summary>The salon's inbox of requests.</summary>
    public static string Requests(string baseUrl) => $"{Trim(baseUrl)}/app/requests";

    /// <summary>Direct link to one request in the salon's inbox.</summary>
    public static string Request(string baseUrl, Guid appointmentId) => $"{Trim(baseUrl)}/app/requests/{appointmentId}";

    /// <summary>The salon's public booking page.</summary>
    public static string Booking(string baseUrl, string salonSlug) => $"{Trim(baseUrl)}/{Uri.EscapeDataString(salonSlug)}";

    private static string Trim(string baseUrl) => baseUrl.TrimEnd('/');
}
