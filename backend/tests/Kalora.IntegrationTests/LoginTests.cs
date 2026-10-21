using System.Net;
using System.Net.Http.Json;
using Kalora.Application.Auth;
using Kalora.Application.Common;

namespace Kalora.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class LoginTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Login_with_the_right_password_signs_in()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        await host.RegisterOwnerAsync("maria@example.com");
        var session = host.NewSession();

        var response = await session.PostAsync("/api/auth/login", new LoginRequest("Maria@Example.com", AuthTestHost.Password));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("maria@example.com", (await session.MeAsync()).Email);
    }

    [Theory]
    [InlineData("maria@example.com", "wrong-password-here")]
    [InlineData("nobody@example.com", "correct-horse-battery")]
    public async Task Wrong_credentials_give_the_same_answer_for_unknown_users_and_wrong_passwords(string email, string password)
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        await host.RegisterOwnerAsync("maria@example.com");
        var session = host.NewSession();

        var response = await session.PostAsync("/api/auth/login", new LoginRequest(email, password));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("auth.invalid_credentials", await response.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, (await session.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task The_account_is_locked_after_five_failed_attempts_even_for_the_right_password()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        await host.RegisterOwnerAsync("maria@example.com");
        var session = host.NewSession();

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            var failed = await session.PostAsync("/api/auth/login", new LoginRequest("maria@example.com", "wrong-password-here"));
            Assert.Equal("auth.invalid_credentials", await failed.ProblemCodeAsync());
        }

        var fifth = await session.PostAsync("/api/auth/login", new LoginRequest("maria@example.com", "wrong-password-here"));
        var correct = await session.PostAsync("/api/auth/login", new LoginRequest("maria@example.com", AuthTestHost.Password));

        Assert.Equal("auth.locked_out", await fifth.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, correct.StatusCode);
        Assert.Equal("auth.locked_out", await correct.ProblemCodeAsync());
    }

    [Fact]
    public async Task Logout_ends_the_session()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (session, _) = await host.RegisterOwnerAsync("maria@example.com");

        var logout = await session.PostAsync("/api/auth/logout");

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await session.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task Anonymous_requests_to_protected_endpoints_get_401_not_a_redirect()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);

        var response = await host.NewSession().GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

[Collection(DatabaseCollection.Name)]
public class AccountRecoveryTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Confirming_the_email_with_the_mailed_token_marks_it_confirmed()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (session, me) = await host.RegisterOwnerAsync("maria@example.com");
        var token = await host.LinkParameterAsync(EmailTypes.ConfirmEmail, "maria@example.com", "ConfirmUrl", "token");

        var response = await session.PostAsync("/api/auth/confirm-email", new ConfirmEmailRequest(me.UserId, token));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True((await session.MeAsync()).EmailConfirmed);
    }

    [Fact]
    public async Task A_wrong_confirmation_token_is_rejected()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (session, me) = await host.RegisterOwnerAsync("maria@example.com");

        var response = await session.PostAsync("/api/auth/confirm-email", new ConfirmEmailRequest(me.UserId, "bogus"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("auth.invalid_token", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task A_password_can_be_reset_once_with_the_mailed_token()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        await host.RegisterOwnerAsync("maria@example.com");
        var anonymous = host.NewSession();

        Assert.Equal(HttpStatusCode.NoContent, (await anonymous.PostAsync("/api/auth/forgot-password", new ForgotPasswordRequest("maria@example.com"))).StatusCode);
        var token = await host.LinkParameterAsync(EmailTypes.ResetPassword, "maria@example.com", "ResetUrl", "token");
        const string newPassword = "a-brand-new-password";

        var reset = await anonymous.PostAsync("/api/auth/reset-password", new ResetPasswordRequest("maria@example.com", token, newPassword));
        var reuse = await anonymous.PostAsync("/api/auth/reset-password", new ResetPasswordRequest("maria@example.com", token, "yet-another-password"));
        var oldLogin = await anonymous.PostAsync("/api/auth/login", new LoginRequest("maria@example.com", AuthTestHost.Password));
        var newLogin = await anonymous.PostAsync("/api/auth/login", new LoginRequest("maria@example.com", newPassword));

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal("auth.invalid_token", await reuse.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    [Fact]
    public async Task Forgot_password_does_not_reveal_whether_an_account_exists()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        await host.RegisterOwnerAsync("maria@example.com");

        var response = await host.NewSession().PostAsync("/api/auth/forgot-password", new ForgotPasswordRequest("nobody@example.com"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await host.OutboxAsync(EmailTypes.ResetPassword, "nobody@example.com"));
    }

    [Fact]
    public async Task A_reset_password_must_meet_the_minimum_length()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        await host.RegisterOwnerAsync("maria@example.com");
        var anonymous = host.NewSession();
        await anonymous.PostAsync("/api/auth/forgot-password", new ForgotPasswordRequest("maria@example.com"));
        var token = await host.LinkParameterAsync(EmailTypes.ResetPassword, "maria@example.com", "ResetUrl", "token");

        var response = await anonymous.PostAsync("/api/auth/reset-password", new ResetPasswordRequest("maria@example.com", token, "short"));

        Assert.Equal("auth.password_too_short", await response.ProblemCodeAsync());
    }
}
