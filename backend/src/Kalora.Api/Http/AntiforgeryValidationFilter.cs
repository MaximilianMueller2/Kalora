using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Kalora.Api.Http;

/// <summary>
/// Requires a valid antiforgery token on every writing request (POST, PUT, PATCH, DELETE) unless the endpoint opts out
/// with <see cref="IgnoreAntiforgeryTokenAttribute"/> (anonymous endpoints that rely on no ambient credentials).
/// </summary>
internal sealed class AntiforgeryValidationFilter(IAntiforgery antiforgery) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (HttpMethods.IsGet(context.HttpContext.Request.Method)
            || HttpMethods.IsHead(context.HttpContext.Request.Method)
            || HttpMethods.IsOptions(context.HttpContext.Request.Method)
            || HttpMethods.IsTrace(context.HttpContext.Request.Method)
            || context.Filters.OfType<IgnoreAntiforgeryTokenAttribute>().Any())
        {
            return;
        }

        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            var problem = new ProblemDetails { Status = StatusCodes.Status400BadRequest, Title = "Bad Request" };
            problem.Extensions["code"] = "security.antiforgery_failed";
            context.Result = new ObjectResult(problem) { StatusCode = problem.Status, ContentTypes = { "application/problem+json" } };
        }
    }
}
