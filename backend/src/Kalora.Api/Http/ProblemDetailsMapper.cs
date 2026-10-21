using Kalora.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace Kalora.Api.Http;

public static class ProblemDetailsMapper
{
    public static ProblemDetails ToProblemDetails(Error error)
    {
        var problem = new ProblemDetails
        {
            Status = ToStatusCode(error.Type),
            Title = error.Type.ToString(),
        };
        problem.Extensions["code"] = error.Code;
        if (error.Parameter is not null)
        {
            problem.Extensions["parameter"] = error.Parameter;
        }

        return problem;
    }

    public static int ToStatusCode(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Unprocessable => StatusCodes.Status422UnprocessableEntity,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };
}
