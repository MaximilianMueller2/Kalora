namespace Kalora.Api.Http;

/// <summary>
/// Headers that tell browsers what an API response may do. The API only ever answers with JSON, files and problem details, so
/// the policy is the strictest one: nothing may be loaded or framed. The web app itself is served by the reverse proxy with its own
/// policy (see docs/SECURITY.md and deploy/security-headers.conf).
/// </summary>
internal sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public const string ApiContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";

    private const string PermissionsPolicy = "accelerometer=(), camera=(), geolocation=(), gyroscope=(), microphone=(), payment=(), usb=()";

    private static readonly PathString[] DocumentationPaths = [new("/scalar"), new("/openapi")];

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] = PermissionsPolicy;
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            headers["Cross-Origin-Resource-Policy"] = "same-origin";

            // The developer documentation (Development only) loads its own scripts and styles.
            if (!DocumentationPaths.Any(path => context.Request.Path.StartsWithSegments(path)))
            {
                headers.ContentSecurityPolicy = ApiContentSecurityPolicy;
            }

            // Nothing behind /api may be kept by a browser or a shared cache: it is either personal or changes all the time.
            if (context.Request.Path.StartsWithSegments("/api") && !headers.ContainsKey("Cache-Control"))
            {
                headers.CacheControl = "no-store";
            }

            return Task.CompletedTask;
        });

        return next(context);
    }
}
