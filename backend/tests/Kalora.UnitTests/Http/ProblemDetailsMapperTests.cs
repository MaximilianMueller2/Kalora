using Kalora.Api.Http;
using Kalora.Application.Common;

namespace Kalora.UnitTests.Http;

public class ProblemDetailsMapperTests
{
    [Theory]
    [InlineData(ErrorType.Validation, 400)]
    [InlineData(ErrorType.Unauthorized, 401)]
    [InlineData(ErrorType.Forbidden, 403)]
    [InlineData(ErrorType.NotFound, 404)]
    [InlineData(ErrorType.Conflict, 409)]
    [InlineData(ErrorType.Unprocessable, 422)]
    public void Maps_error_type_to_status_code(ErrorType type, int expectedStatus)
    {
        var problem = ProblemDetailsMapper.ToProblemDetails(new Error(type, "some.code"));

        Assert.Equal(expectedStatus, problem.Status);
        Assert.Equal("some.code", problem.Extensions["code"]);
        Assert.False(problem.Extensions.ContainsKey("parameter"));
    }

    [Fact]
    public void Includes_parameter_when_present()
    {
        var problem = ProblemDetailsMapper.ToProblemDetails(Error.Validation("field.required", "email"));

        Assert.Equal("email", problem.Extensions["parameter"]);
    }
}
