using Kalora.Application.Common;
using Kalora.Application.Tenancy;

namespace Kalora.Api.Http;

/// <summary>
/// Decides which salon a request acts for. Public endpoints name it in the URL (/api/public/salons/{slug}/...);
/// all other requests take it from the signed-in user's claim. A SalonId from a request body is never used.
/// Public paths never use claims, so a signed-in salon cannot leak into another salon's public page.
/// </summary>
internal sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    private static readonly PathString PublicPrefix = new("/api/public");
    private static readonly PathString PublicSalonPrefix = new("/api/public/salons");

    public async Task InvokeAsync(HttpContext context, ITenantContextInitializer tenant, ISalonResolver resolver)
    {
        var path = context.Request.Path;
        if (path.StartsWithSegments(PublicSalonPrefix, out var remainder))
        {
            var slug = remainder.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToLowerInvariant();
            var resolved = slug is null
                ? Error.NotFound("salon.not_found", "slug")
                : await resolver.ResolveSlugAsync(slug, context.RequestAborted);
            if (!resolved.IsSuccess)
            {
                await WriteProblemAsync(context, resolved.Error);
                return;
            }

            tenant.Initialize(resolved.Value);
        }
        else if (!path.StartsWithSegments(PublicPrefix)
            && Guid.TryParse(context.User.FindFirst(TenantClaims.SalonId)?.Value, out var salonId))
        {
            tenant.Initialize(salonId);
        }

        await next(context);
    }

    private static async Task WriteProblemAsync(HttpContext context, Error error)
    {
        var problem = ProblemDetailsMapper.ToProblemDetails(error);
        context.Response.StatusCode = problem.Status!.Value;
        await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", context.RequestAborted);
    }
}
