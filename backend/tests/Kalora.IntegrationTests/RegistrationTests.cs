using System.Net;
using System.Net.Http.Json;
using Kalora.Application.Auth;
using Kalora.Application.Common;
using Kalora.Domain.Messaging;
using Kalora.Domain.Salons;
using Microsoft.EntityFrameworkCore;

namespace Kalora.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class RegistrationTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Registering_creates_the_owner_the_salon_and_a_trial_and_signs_in()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var session = host.NewSession();

        var response = await session.PostAsync("/api/auth/register", AuthTestHost.Registration("maria@example.com"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = (await response.Content.ReadFromJsonAsync<MeResponse>(TestJson.Options))!;
        Assert.Equal("maria@example.com", me.Email);
        Assert.Equal(SalonRole.Owner, me.Role);
        Assert.Equal("haarwerk-mueller", me.Salon!.Slug);
        Assert.Equal(SalonStatus.Trial, me.Salon.Status);
        Assert.Equal(host.Clock.GetUtcNow().AddDays(30), me.Salon.TrialEndsAtUtc);
        Assert.False(me.EmailConfirmed);

        // The response signed the owner in: the next request already knows the user and the salon.
        var current = await session.MeAsync();
        Assert.Equal(me.UserId, current.UserId);
        Assert.Equal(me.Salon.Id, current.Salon!.Id);
    }

    [Fact]
    public async Task The_login_cookie_is_http_only_secure_and_same_site_lax()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var session = host.NewSession();

        var response = await session.PostAsync("/api/auth/register", AuthTestHost.Registration("maria@example.com"));

        var cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("kalora.session=", StringComparison.Ordinal));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Registering_queues_a_confirmation_mail_and_does_not_store_the_password()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        await host.RegisterOwnerAsync("maria@example.com");

        var mail = Assert.Single(await host.OutboxAsync(EmailTypes.ConfirmEmail, "maria@example.com"));

        Assert.Equal(EmailStatus.Pending, mail.Status);
        Assert.Equal("de", mail.Language);
        Assert.DoesNotContain(AuthTestHost.Password, mail.PayloadJson);
        await using var db = TestDb.CreateContext(host.ConnectionString);
        var hash = await db.Users.Where(u => u.Email == "maria@example.com").Select(u => u.PasswordHash).SingleAsync();
        Assert.NotEqual(AuthTestHost.Password, hash);
    }

    [Fact]
    public async Task Salons_with_the_same_name_get_distinct_slugs_and_reserved_slugs_are_avoided()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);

        var (_, first) = await host.RegisterOwnerAsync("a@example.com", "Schnitt & Stil");
        var (_, second) = await host.RegisterOwnerAsync("b@example.com", "Schnitt & Stil");
        var (_, reserved) = await host.RegisterOwnerAsync("c@example.com", "Admin");

        Assert.Equal("schnitt-stil", first.Salon!.Slug);
        Assert.Equal("schnitt-stil-2", second.Salon!.Slug);
        Assert.Equal("admin-2", reserved.Salon!.Slug);
    }

    [Fact]
    public async Task A_second_registration_with_the_same_email_is_a_conflict_regardless_of_case()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        await host.RegisterOwnerAsync("maria@example.com");

        var response = await host.NewSession().PostAsync("/api/auth/register", AuthTestHost.Registration("MARIA@Example.com", "Anderer Salon"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("auth.email_taken", await response.ProblemCodeAsync());
    }

    [Theory]
    [InlineData("not-an-email", "Password-long-enough", true, "de", "validation.email_invalid")]
    [InlineData("a@example.com", "short", true, "de", "auth.password_too_short")]
    [InlineData("a@example.com", "Password-long-enough", false, "de", "auth.terms_not_accepted")]
    [InlineData("a@example.com", "Password-long-enough", true, "fr", "validation.language_unsupported")]
    public async Task Invalid_registrations_are_rejected_with_a_translatable_code(
        string email, string password, bool acceptTerms, string language, string expectedCode)
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var request = new RegisterRequest("Salon", SalonIndustry.Hair, "Maria", email, password, language, acceptTerms);

        var response = await host.NewSession().PostAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(expectedCode, await response.ProblemCodeAsync());
        await using var db = TestDb.CreateContext(host.ConnectionString);
        Assert.Empty(await db.Users.ToListAsync());
        Assert.Empty(await db.AllSalons().ToListAsync());
    }

    [Fact]
    public async Task Writing_requests_without_the_antiforgery_header_are_rejected()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var session = host.NewSession();

        var response = await session.Client.PostAsJsonAsync("/api/auth/register", AuthTestHost.Registration("maria@example.com"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("security.antiforgery_failed", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task The_antiforgery_endpoint_works_over_plain_http_and_behind_a_tls_proxy()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var plainHttp = host.NewSession(new Uri("http://localhost"));
        var behindProxy = host.NewSession(new Uri("http://localhost"));
        behindProxy.Client.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");

        var local = await plainHttp.GetAsync("/api/antiforgery");
        var proxied = await behindProxy.GetAsync("/api/antiforgery");

        Assert.Equal(HttpStatusCode.NoContent, local.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, proxied.StatusCode);
        Assert.DoesNotContain("secure", string.Join(';', local.Headers.GetValues("Set-Cookie")), StringComparison.OrdinalIgnoreCase);
        Assert.All(proxied.Headers.GetValues("Set-Cookie"), cookie => Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task A_user_can_belong_to_only_one_salon()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (_, me) = await host.RegisterOwnerAsync("maria@example.com");
        var (_, other) = await host.RegisterOwnerAsync("other@example.com", "Anderer Salon");

        await using var db = TestDb.CreateContext(host.ConnectionString);
        db.SalonMembers.Add(new SalonMember { SalonId = other.Salon!.Id, UserId = me.UserId, Role = SalonRole.Staff });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
