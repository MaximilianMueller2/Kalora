using Kalora.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace Kalora.Api.Http;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult ToActionResult<T>(Result<T> result) =>
        result.IsSuccess ? Ok(result.Value) : Problem(result.Error);

    protected IActionResult ToActionResult(Result result) =>
        result.IsSuccess ? NoContent() : Problem(result.Error);

    private ObjectResult Problem(Error error)
    {
        var problem = ProblemDetailsMapper.ToProblemDetails(error);
        return new ObjectResult(problem)
        {
            StatusCode = problem.Status,
            ContentTypes = { "application/problem+json" },
        };
    }
}
