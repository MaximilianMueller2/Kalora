using Kalora.Application.Identity;
using Microsoft.AspNetCore.Identity;

namespace Kalora.Api.Auth;

internal sealed class SessionService(SignInManager<AppUser> signInManager) : ISessionService
{
    public Task SignInAsync(AppUser user, bool persistent) => signInManager.SignInAsync(user, persistent);

    public Task RefreshAsync(AppUser user) => signInManager.RefreshSignInAsync(user);

    public Task SignOutAsync() => signInManager.SignOutAsync();
}
