namespace Kalora.Application.Identity;

internal static class EmailAddresses
{
    public static string Normalize(string email) => email.Trim().ToLowerInvariant();
}
