using Kalora.Application.Common;

namespace Kalora.UnitTests.Common;

public class ResultTests
{
    [Fact]
    public void Success_exposes_value_and_has_no_error()
    {
        Result<int> result = 42;

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Throws<InvalidOperationException>(() => result.Error);
    }

    [Fact]
    public void Failure_exposes_error_and_has_no_value()
    {
        Result<int> result = Error.NotFound("salon.not_found", "slug");

        Assert.False(result.IsSuccess);
        Assert.Equal("salon.not_found", result.Error.Code);
        Assert.Equal("slug", result.Error.Parameter);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Non_generic_result_converts_from_error()
    {
        Result result = Error.Conflict("booking.slot_unavailable");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.True(Result.Success().IsSuccess);
    }
}
