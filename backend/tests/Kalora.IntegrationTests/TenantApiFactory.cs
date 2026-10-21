using System.Security.Claims;
using System.Text.Encodings.Web;
using Kalora.Application.Tenancy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kalora.IntegrationTests;

/// <summary>
/// The real API against a real database. Requests are authenticated by a test scheme: the header
/// <see cref="SalonHeader"/> carries the SalonId claim (no header = anonymous). Test controllers stand in for CRUD endpoints.
/// </summary>
public sealed class TenantApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    public const string SalonHeader = "X-Test-Salon";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", connectionString);
        builder.ConfigureServices(services =>
        {
            services.AddControllers().AddApplicationPart(typeof(TestStaffController).Assembly);
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }

    public HttpClient CreateClientFor(Guid? salonId)
    {
        var client = CreateClient();
        if (salonId is { } id)
        {
            client.DefaultRequestHeaders.Add(SalonHeader, id.ToString());
        }

        return client;
    }
}

internal sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(TenantApiFactory.SalonHeader, out var salonId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity([new Claim(TenantClaims.SalonId, salonId.ToString())], SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
