using System.Text.Json;
using Kalora.Api.Http;

namespace Kalora.UnitTests.Http;

public class TimeOnlyJsonConverterTests
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new TimeOnlyJsonConverter() } };

    [Fact]
    public void Writes_hours_and_minutes() =>
        Assert.Equal("\"09:05\"", JsonSerializer.Serialize(new TimeOnly(9, 5, 30), Options));

    [Theory]
    [InlineData("\"09:05\"")]
    [InlineData("\"09:05:00\"")]
    public void Reads_both_formats(string json) =>
        Assert.Equal(new TimeOnly(9, 5), JsonSerializer.Deserialize<TimeOnly>(json, Options));

    [Theory]
    [InlineData("\"25:00\"")]
    [InlineData("\"9\"")]
    [InlineData("\"noon\"")]
    [InlineData("null")]
    public void Rejects_everything_else(string json) =>
        Assert.ThrowsAny<JsonException>(() => JsonSerializer.Deserialize<TimeOnly>(json, Options));
}
