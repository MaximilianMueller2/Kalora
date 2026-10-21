namespace Kalora.Application.Identity;

/// <summary>Creates and ends the login session (cookie). Implemented in Api, where the HTTP context lives.</summary>
public interface ISessionService
{
    Task SignInAsync(AppUser user, bool persistent);

    /// <summary>Re-issues the cookie after a change that updates the security stamp (e.g. enabling 2FA), keeping the user signed in.</summary>
    Task RefreshAsync(AppUser user);

    Task SignOutAsync();
}
