using Kalora.Api.Auth;
using Microsoft.AspNetCore.Antiforgery;

namespace Kalora.Api.Http;

internal static class AntiforgeryEndpoint
{
    /// <summary>
    /// Hands the frontend its request token as a script-readable cookie (Angular echoes it in the X-XSRF-TOKEN header).
    /// Tokens are bound to the user, so the frontend fetches a fresh one after login and logout.
    /// </summary>
    public static void MapAntiforgeryEndpoint(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet("/api/antiforgery", (HttpContext context, IAntiforgery antiforgery) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(context);
            context.Response.Cookies.Append(AuthenticationSetup.AntiforgeryRequestCookie, tokens.RequestToken!, new CookieOptions
            {
                HttpOnly = false,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Path = "/",
            });
            return Results.NoContent();
        });
}
