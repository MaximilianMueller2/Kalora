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

    private static string Trim(string baseUrl) => baseUrl.TrimEnd('/');
}
