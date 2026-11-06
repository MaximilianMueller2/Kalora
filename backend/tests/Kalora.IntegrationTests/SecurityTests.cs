using System.Net;
using System.Net.Http.Json;
using Kalora.Application.Auth;
using Kalora.Application.Identity;
using Kalora.Domain.Salons;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Kalora.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class SecurityTests(PostgresFixture postgres)
{
    // ---------------------------------------------------------------- headers

    [Theory]
    [InlineData("/api/ping")]
    [InlineData("/api/me")]
    [InlineData("/api/does-not-exist")]
    [InlineData("/health")]
    public async Task Every_response_tells_the_browser_what_it_may_not_do(string path)
    {
        await using var host = await AuthTestHost.StartAsync(postgres);

        var response = await host.NewSession().GetAsync(path);

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Contains("camera=()", response.Headers.GetValues("Permissions-Policy").Single());
        Assert.Contains("microphone=()", response.Headers.GetValues("Permissions-Policy").Single());
        Assert.Equal("same-origin", response.Headers.GetValues("Cross-Origin-Resource-Policy").Single());
        Assert.Equal("default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
    }

    [Theory]
    [InlineData("/api/ping")]
    [InlineData("/api/me")]
    [InlineData("/api/auth/login")]
    public async Task Nothing_behind_the_api_is_kept_by_a_browser_or_a_cache(string path)
    {
        await using var host = await AuthTestHost.StartAsync(postgres);

        var response = await host.NewSession().GetAsync(path);

        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Over_https_on_a_real_host_name_browsers_are_told_to_stay_on_https()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);

        var response = await host.NewSession(new Uri("https://kalora.test")).GetAsync("/api/ping");

        Assert.Equal("max-age=31536000; includeSubDomains", response.Headers.GetValues("Strict-Transport-Security").Single());
    }

    // ---------------------------------------------------------------- CORS

    [Fact]
    public async Task No_other_site_is_allowed_to_call_the_api_from_a_browser()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var session = host.NewSession();

        var simple = new HttpRequestMessage(HttpMethod.Get, "/api/ping");
        simple.Headers.Add("Origin", "https://evil.example");
        var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/auth/login");
        preflight.Headers.Add("Origin", "https://evil.example");
        preflight.Headers.Add("Access-Control-Request-Method", "POST");
        preflight.Headers.Add("Access-Control-Request-Headers", "content-type,x-xsrf-token");

        foreach (var request in new[] { simple, preflight })
        {
            var response = await session.Client.SendAsync(request);

            Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"), $"{request.Method} {request.RequestUri}");
            Assert.False(response.Headers.Contains("Access-Control-Allow-Methods"));
            Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
        }
    }

    // ---------------------------------------------------------------- who may call what

    /// <summary>The endpoints anyone may call without signing in. Adding one is a decision: it goes here and into docs/SECURITY.md.</summary>
    private static readonly string[] AnonymousEndpoints =
    [
        "GET /api/antiforgery",
        "GET /api/ping",
        "GET /health",
        "POST /api/auth/confirm-email",
        "POST /api/auth/forgot-password",
        "POST /api/auth/login",
        "POST /api/auth/logout",
        "POST /api/auth/register",
        "POST /api/auth/reset-password",
        "GET /api/public/appointments/{token}",
        "GET /api/public/appointments/{token}/calendar.ics",
        "POST /api/public/appointments/{token}/cancel",
        "GET /api/public/invitations/{token}",
        "POST /api/public/invitations/{token}/accept",
        "GET /api/public/salons/{slug}",
        "GET /api/public/salons/{slug}/availability/days",
        "GET /api/public/salons/{slug}/availability/slots",
        "POST /api/public/salons/{slug}/appointment-requests",
        "GET /api/public/salons/{slug}/services",
        "GET /api/public/salons/{slug}/staff",
    ];

    private static readonly string[] KnownPolicies =
        [AuthPolicies.SalonMember, AuthPolicies.SalonManager, AuthPolicies.SalonOwner, AuthPolicies.PlatformAdmin];

    private sealed record Endpoint(string Key, bool Anonymous, bool Writes, bool RateLimited, string[] Policies);

    private static IReadOnlyList<Endpoint> Endpoints(IServiceProvider services) => services.GetRequiredService<EndpointDataSource>().Endpoints
        .OfType<RouteEndpoint>()
        .Where(e => e.Metadata.GetMetadata<ControllerActionDescriptor>() is not null || e.RoutePattern.RawText is "/api/antiforgery" or "/health")
        .SelectMany(e =>
        {
            var methods = e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["GET"];
            return methods.Select(method => new Endpoint(
                $"{method} /{e.RoutePattern.RawText!.TrimStart('/')}",
                e.Metadata.GetMetadata<IAllowAnonymous>() is not null,
                method is "POST" or "PUT" or "PATCH" or "DELETE",
                e.Metadata.GetMetadata<EnableRateLimitingAttribute>() is not null,
                e.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(a => a.Policy ?? "(signed in)").ToArray()));
        })
        .ToList();

    [Fact]
    public async Task Every_endpoint_says_who_may_call_it_and_the_anonymous_ones_are_the_known_ones()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);

        var endpoints = Endpoints(host.Services);

        Assert.True(endpoints.Count > 80, "the endpoint list could not be read");
        var undecided = endpoints.Where(e => !e.Anonymous && e.Policies.Length == 0).Select(e => e.Key).ToList();
        Assert.Empty(undecided);
        Assert.Equal(AnonymousEndpoints.Order(), endpoints.Where(e => e.Anonymous).Select(e => e.Key).Order());
    }

    [Fact]
    public async Task An_endpoint_that_says_nothing_is_refused_rather_than_open()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var provider = host.Services.GetRequiredService<IAuthorizationPolicyProvider>();

        var fallback = await provider.GetFallbackPolicyAsync();

        Assert.NotNull(fallback);
        Assert.Contains(fallback.Requirements, requirement => requirement is Microsoft.AspNetCore.Authorization.Infrastructure.DenyAnonymousAuthorizationRequirement);
    }

    [Fact]
    public async Task Only_known_policies_protect_the_other_endpoints()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);

        var used = Endpoints(host.Services).Where(e => !e.Anonymous).SelectMany(e => e.Policies).Distinct().ToList();

        Assert.All(used, policy => Assert.Contains(policy, KnownPolicies.Append("(signed in)")));
        Assert.Contains(AuthPolicies.PlatformAdmin, used);
    }

    [Fact]
    public async Task Every_anonymous_endpoint_that_changes_something_has_its_own_rate_limit()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);

        var unlimited = Endpoints(host.Services).Where(e => e.Anonymous && e.Writes && !e.RateLimited).Select(e => e.Key).ToList();

        // Signing out needs no limit: it only ends the caller's own session.
        Assert.Equal(["POST /api/auth/logout"], unlimited);
    }

    [Fact]
    public async Task A_signed_in_write_without_or_with_a_wrong_antiforgery_token_is_refused()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, _) = await host.RegisterOwnerAsync("maria@example.com");
        await owner.RefreshAntiforgeryAsync();
        var body = new Kalora.Application.Staff.CreateStaffRequest("Anna", "#7C5CFC", true, false, false);

        owner.Client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        var missing = await owner.Client.PostAsJsonAsync("/api/staff", body);
        owner.Client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", "not-a-token");
        var wrong = await owner.Client.PostAsJsonAsync("/api/staff", body);

        Assert.Equal((HttpStatusCode.BadRequest, "security.antiforgery_failed"), (missing.StatusCode, await missing.ProblemCodeAsync()));
        Assert.Equal((HttpStatusCode.BadRequest, "security.antiforgery_failed"), (wrong.StatusCode, await wrong.ProblemCodeAsync()));
        Assert.Empty(await owner.GetJsonAsync<List<Kalora.Application.Staff.StaffDto>>("/api/staff"));
    }

    // ---------------------------------------------------------------- rate limits

    private static Dictionary<string, string> Limit(string policy, int permits) => new()
    {
        [$"RateLimiting:{policy}:PermitLimit"] = permits.ToString(),
        [$"RateLimiting:{policy}:WindowSeconds"] = "600",
    };

    [Fact]
    public async Task Too_many_sign_in_attempts_from_one_address_are_stopped_with_a_hint_when_to_try_again()
    {
        await using var host = await AuthTestHost.StartAsync(postgres, Limit("Login", 3));
        var session = host.NewSession();

        var answers = new List<HttpStatusCode>();
        HttpResponseMessage last = null!;
        for (var i = 0; i < 5; i++)
        {
            last = await session.PostAsync("/api/auth/login", new LoginRequest($"nobody{i}@example.com", "wrong-password-here"));
            answers.Add(last.StatusCode);
        }

        Assert.Equal([HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests, HttpStatusCode.TooManyRequests], answers);
        Assert.Equal("rate_limited", await last.ProblemCodeAsync());
        Assert.True(last.Headers.RetryAfter?.Delta > TimeSpan.Zero);
    }

    [Fact]
    public async Task Registration_is_limited_per_address()
    {
        await using var host = await AuthTestHost.StartAsync(postgres, Limit("Register", 2));

        var first = await host.NewSession().PostAsync("/api/auth/register", AuthTestHost.Registration("one@example.com", "Eins"));
        var second = await host.NewSession().PostAsync("/api/auth/register", AuthTestHost.Registration("two@example.com", "Zwei"));
        var third = await host.NewSession().PostAsync("/api/auth/register", AuthTestHost.Registration("three@example.com", "Drei"));

        Assert.True(first.IsSuccessStatusCode);
        Assert.True(second.IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
    }

    [Theory]
    [InlineData("/api/auth/forgot-password")]
    [InlineData("/api/auth/reset-password")]
    [InlineData("/api/auth/confirm-email")]
    public async Task Password_reset_and_confirmation_are_limited_together_so_they_cannot_send_floods_of_mails_or_test_tokens(string path)
    {
        await using var host = await AuthTestHost.StartAsync(postgres, Limit("Recovery", 2));
        var session = host.NewSession();
        object Body(int i) => path switch
        {
            "/api/auth/forgot-password" => new ForgotPasswordRequest($"who{i}@example.com"),
            "/api/auth/reset-password" => new ResetPasswordRequest($"who{i}@example.com", "token", "correct-horse-battery"),
            _ => new ConfirmEmailRequest(Guid.NewGuid(), "token"),
        };

        var codes = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
        {
            codes.Add((await session.PostAsync(path, Body(i))).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, codes[2]);
        Assert.DoesNotContain(HttpStatusCode.TooManyRequests, codes.Take(2));
    }

    [Fact]
    public async Task The_pages_behind_an_invitation_link_are_limited_too()
    {
        await using var host = await AuthTestHost.StartAsync(postgres, Limit("Manage", 2));
        var session = host.NewSession();

        var codes = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
        {
            codes.Add((await session.GetAsync($"/api/public/invitations/guess{i}")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, codes[2]);
    }

    [Fact]
    public async Task Everything_else_has_a_generous_flood_limit_but_the_health_check_has_none()
    {
        await using var host = await AuthTestHost.StartAsync(postgres, Limit("General", 5));
        var session = host.NewSession();

        var codes = new List<HttpStatusCode>();
        for (var i = 0; i < 7; i++)
        {
            codes.Add((await session.GetAsync("/api/ping")).StatusCode);
        }

        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests, HttpStatusCode.TooManyRequests], codes);
        Assert.Equal(HttpStatusCode.OK, (await session.GetAsync("/health")).StatusCode);
    }

    // ---------------------------------------------------------------- locking accounts

    [Fact]
    public async Task Wrong_passwords_to_confirm_a_security_change_lock_the_account_like_wrong_passwords_at_sign_in()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, _) = await host.RegisterOwnerAsync("maria@example.com");

        var codes = new List<string?>();
        for (var i = 0; i < 6; i++)
        {
            codes.Add(await (await owner.PostAsync("/api/auth/two-factor/disable", new PasswordConfirmationRequest("not-the-password"))).ProblemCodeAsync());
        }

        Assert.Equal(["auth.invalid_credentials", "auth.invalid_credentials", "auth.invalid_credentials", "auth.invalid_credentials", "auth.locked_out", "auth.locked_out"], codes);
        // Even the right password is not accepted while the account is locked, here and at sign-in.
        Assert.Equal("auth.locked_out", await (await owner.PostAsync("/api/auth/two-factor/disable", new PasswordConfirmationRequest(AuthTestHost.Password))).ProblemCodeAsync());
        Assert.Equal("auth.locked_out", await (await host.NewSession().PostAsync("/api/auth/login", new LoginRequest("maria@example.com", AuthTestHost.Password))).ProblemCodeAsync());
    }

    [Fact]
    public async Task A_right_password_clears_the_count_of_wrong_ones()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, _) = await host.RegisterOwnerAsync("maria@example.com");

        for (var round = 0; round < 3; round++)
        {
            for (var i = 0; i < 3; i++)
            {
                Assert.Equal("auth.invalid_credentials", await (await owner.PostAsync("/api/auth/two-factor/disable", new PasswordConfirmationRequest("not-the-password"))).ProblemCodeAsync());
            }

            Assert.True((await owner.PostAsync("/api/auth/two-factor/disable", new PasswordConfirmationRequest(AuthTestHost.Password))).IsSuccessStatusCode);
        }
    }

    // ---------------------------------------------------------------- inputs

    [Fact]
    public async Task Absurdly_long_inputs_are_refused_before_any_work_is_done()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, _) = await host.RegisterOwnerAsync("maria@example.com");
        var huge = new string('x', 100_000);

        var login = await host.NewSession().PostAsync("/api/auth/login", new LoginRequest("maria@example.com", huge));
        var longEmail = await host.NewSession().PostAsync("/api/auth/login", new LoginRequest(new string('a', 400) + "@example.com", "x"));
        var reset = await host.NewSession().PostAsync("/api/auth/reset-password", new ResetPasswordRequest("maria@example.com", huge, "correct-horse-battery"));
        var confirm = await host.NewSession().PostAsync("/api/auth/confirm-email", new ConfirmEmailRequest(Guid.NewGuid(), huge));
        var code = await owner.PostAsync("/api/auth/two-factor/enable", new TwoFactorCodeRequest(huge));
        var search = await owner.GetAsync($"/api/customers?q={new string('q', 101)}");

        Assert.Equal((HttpStatusCode.BadRequest, "validation.too_long"), (login.StatusCode, await login.ProblemCodeAsync()));
        Assert.Equal("validation.too_long", await longEmail.ProblemCodeAsync());
        Assert.Equal("auth.invalid_token", await reset.ProblemCodeAsync());
        Assert.Equal("auth.invalid_token", await confirm.ProblemCodeAsync());
        Assert.Equal("auth.invalid_two_factor_code", await code.ProblemCodeAsync());
        Assert.Equal((HttpStatusCode.BadRequest, "validation.too_long"), (search.StatusCode, await search.ProblemCodeAsync()));
    }

    [Fact]
    public async Task Guessing_ids_or_tokens_confirms_nothing()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (_, me) = await host.RegisterOwnerAsync("maria@example.com");
        var (otherOwner, _) = await host.RegisterOwnerAsync("other@example.com", "Anderer Salon");
        var staff = await host.AddStaffAsync(me.Salon!.Id, "Anna");

        // Another salon's owner asks for a person of this salon: not found, not forbidden, so nothing is confirmed.
        Assert.Equal(HttpStatusCode.NotFound, (await otherOwner.GetAsync($"/api/staff/{staff.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.NewSession().GetAsync("/api/public/appointments/not-a-real-token")).StatusCode);
    }

    // ---------------------------------------------------------------- what staff may see

    [Fact]
    public async Task A_staff_member_finds_only_customers_with_an_appointment_of_their_own_in_the_customer_search()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, me) = await host.RegisterOwnerAsync("maria@example.com");
        var tom = await host.InviteAndAcceptAsync(owner, me.Salon!.Id, "Tom", "tom@example.com", SalonRole.Staff);
        var salon = new SalonSeeder(host.ConnectionString, me.Salon.Id);
        var cut = await salon.AddServiceAsync();
        var toms = await salon.WithDbAsync(db => Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(db.StaffMembers, s => s.DisplayName == "Tom"));
        var anna = await salon.AddStaffAsync("Anna", 1, offers: cut);
        await salon.AddAppointmentAsync(cut, toms, new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero), customerName: "Tims Kunde");
        await salon.AddAppointmentAsync(cut, anna, new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero), customerName: "Annas Kunde");

        async Task<string[]> Find(ApiSession session) =>
            (await session.GetJsonAsync<List<Kalora.Application.Calendar.CustomerHitDto>>("/api/calendar/customers?q=kunde")).Select(h => h.Name).Order().ToArray();

        Assert.Equal(["Annas Kunde", "Tims Kunde"], await Find(owner));
        Assert.Equal(["Tims Kunde"], await Find(tom));
        Assert.Equal(HttpStatusCode.Forbidden, (await tom.GetAsync("/api/customers")).StatusCode);
        var tooLong = await tom.GetAsync($"/api/calendar/customers?q={new string('k', 101)}");
        Assert.Equal((HttpStatusCode.BadRequest, "validation.too_long"), (tooLong.StatusCode, await tooLong.ProblemCodeAsync()));
    }
}
