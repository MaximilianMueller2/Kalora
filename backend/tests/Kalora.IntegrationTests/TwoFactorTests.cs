using System.Net;
using System.Net.Http.Json;
using Kalora.Application.Auth;

namespace Kalora.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class TwoFactorTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Two_factor_can_be_enabled_is_then_required_at_login_and_recovery_codes_work_once()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (session, _) = await host.RegisterOwnerAsync("maria@example.com");

        var setup = (await (await session.PostAsync("/api/auth/two-factor/setup")).Content.ReadFromJsonAsync<TwoFactorSetupResponse>())!;
        Assert.StartsWith("otpauth://totp/Kalora:maria%40example.com?secret=", setup.AuthenticatorUri);
        var enable = await session.PostAsync("/api/auth/two-factor/enable", new TwoFactorCodeRequest(Totp.Code(setup.SharedKey)));
        var recoveryCodes = (await enable.Content.ReadFromJsonAsync<RecoveryCodesResponse>())!.RecoveryCodes;

        Assert.Equal(10, recoveryCodes.Count);
        // Enabling changes the security stamp; the current session stays signed in.
        Assert.True((await session.MeAsync()).TwoFactorEnabled);

        await session.PostAsync("/api/auth/logout");
        var withoutCode = await session.PostAsync("/api/auth/login", new LoginRequest("maria@example.com", AuthTestHost.Password));
        var wrongCode = await session.PostAsync("/api/auth/login", new LoginRequest("maria@example.com", AuthTestHost.Password, TwoFactorCode: "000000"));
        var rightCode = await session.PostAsync("/api/auth/login", new LoginRequest("maria@example.com", AuthTestHost.Password, TwoFactorCode: Totp.Code(setup.SharedKey)));

        Assert.Equal("auth.two_factor_required", await withoutCode.ProblemCodeAsync());
        Assert.Equal("auth.invalid_two_factor_code", await wrongCode.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.OK, rightCode.StatusCode);

        await session.PostAsync("/api/auth/logout");
        var recovery = new LoginRequest("maria@example.com", AuthTestHost.Password, RecoveryCode: recoveryCodes[0]);
        Assert.Equal(HttpStatusCode.OK, (await session.PostAsync("/api/auth/login", recovery)).StatusCode);
        await session.PostAsync("/api/auth/logout");
        Assert.Equal("auth.invalid_two_factor_code", await (await session.PostAsync("/api/auth/login", recovery)).ProblemCodeAsync());
    }

    [Fact]
    public async Task The_second_factor_is_not_revealed_when_the_password_is_wrong()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (session, _) = await host.RegisterOwnerAsync("maria@example.com");
        await EnableAsync(session);
        await session.PostAsync("/api/auth/logout");

        var response = await session.PostAsync("/api/auth/login", new LoginRequest("maria@example.com", "wrong-password-here"));

        Assert.Equal("auth.invalid_credentials", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task Enabling_with_a_wrong_code_is_rejected_and_leaves_two_factor_off()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (session, _) = await host.RegisterOwnerAsync("maria@example.com");
        await session.PostAsync("/api/auth/two-factor/setup");

        var response = await session.PostAsync("/api/auth/two-factor/enable", new TwoFactorCodeRequest("000000"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False((await session.MeAsync()).TwoFactorEnabled);
    }

    [Fact]
    public async Task Disabling_needs_the_password_and_turns_the_login_requirement_off()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (session, _) = await host.RegisterOwnerAsync("maria@example.com");
        await EnableAsync(session);

        var wrongPassword = await session.PostAsync("/api/auth/two-factor/disable", new PasswordConfirmationRequest("wrong-password-here"));
        var disabled = await session.PostAsync("/api/auth/two-factor/disable", new PasswordConfirmationRequest(AuthTestHost.Password));
        await session.PostAsync("/api/auth/logout");
        var login = await session.PostAsync("/api/auth/login", new LoginRequest("maria@example.com", AuthTestHost.Password));

        Assert.Equal(HttpStatusCode.BadRequest, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, disabled.StatusCode);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Setup_is_refused_while_two_factor_is_already_enabled()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (session, _) = await host.RegisterOwnerAsync("maria@example.com");
        await EnableAsync(session);

        var response = await session.PostAsync("/api/auth/two-factor/setup");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Two_factor_endpoints_require_a_login()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);

        var response = await host.NewSession().PostAsync("/api/auth/two-factor/setup");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<string> EnableAsync(ApiSession session)
    {
        var setup = (await (await session.PostAsync("/api/auth/two-factor/setup")).Content.ReadFromJsonAsync<TwoFactorSetupResponse>())!;
        (await session.PostAsync("/api/auth/two-factor/enable", new TwoFactorCodeRequest(Totp.Code(setup.SharedKey)))).EnsureSuccessStatusCode();
        return setup.SharedKey;
    }
}
