using System.Net;
using System.Net.Http.Json;
using Kalora.Application.Ping;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Kalora.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class PingEndpointTests(PostgresFixture postgres)
{
    // MVC requests need the data protection keys (stored in the database), so these tests use a real one.
    private async Task<AuthApiFactory> StartAsync() => new(await postgres.CreateMigratedDatabaseAsync());

    [Fact]
    public async Task Ping_returns_ok_and_utc_time()
    {
        await using var factory = await StartAsync();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/ping");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PingResponse>();
        Assert.Equal("ok", body!.Status);
        Assert.Equal(TimeSpan.Zero, body.TimeUtc.Offset);
    }

    [Fact]
    public async Task Health_is_healthy()
    {
        await using var factory = await StartAsync();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Unhandled_exception_becomes_problem_details_without_leaking_details()
    {
        await using var factory = await StartAsync();
        var client = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddScoped<IPingService, ThrowingPingService>()))
            .CreateClient();

        var response = await client.GetAsync("/api/ping");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.Contains("server.unexpected", raw);
        Assert.DoesNotContain("secret failure detail", raw);
    }

    [Fact]
    public void Invalid_configuration_stops_the_host_on_startup()
    {
        using var factory = new ApiFactory();
        var invalid = factory.WithWebHostBuilder(builder => builder.UseSetting("App:PublicBaseUrl", "not-a-url"));

        Assert.Throws<OptionsValidationException>(() => invalid.CreateClient());
    }

    private sealed class ThrowingPingService : IPingService
    {
        public Kalora.Application.Common.Result<PingResponse> Ping() =>
            throw new InvalidOperationException("secret failure detail");
    }
}
