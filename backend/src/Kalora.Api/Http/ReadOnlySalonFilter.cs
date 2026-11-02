using Kalora.Application.Subscriptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Kalora.Api.Http;

/// <summary>
/// One place that keeps the salon area read-only when the salon's access is limited: any writing request of a signed-in salon user
/// is answered with <c>403 subscription.read_only</c>. Public endpoints (booking) decide for themselves, because customers are not signed in.
/// </summary>
internal sealed class ReadOnlySalonFilter(ISubscriptionGuard guard) : IAsyncActionFilter
{
    private static readonly PathString PublicPrefix = new("/api/public");

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        if (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || HttpMethods.IsOptions(request.Method)
            || request.Path.StartsWithSegments(PublicPrefix)
            || context.HttpContext.User.Identity?.IsAuthenticated != true
            || context.ActionDescriptor.EndpointMetadata.OfType<AllowWhenReadOnlyAttribute>().Any())
        {
            await next();
            return;
        }

        var writable = await guard.EnsureWritableAsync(context.HttpContext.RequestAborted);
        if (writable.IsSuccess)
        {
            await next();
            return;
        }

        var problem = ProblemDetailsMapper.ToProblemDetails(writable.Error);
        context.Result = new ObjectResult(problem) { StatusCode = problem.Status, ContentTypes = { "application/problem+json" } };
    }
}
