using Kalora.Application.Configuration;
using Kalora.Application.Security;
using Microsoft.Extensions.Options;

namespace Kalora.UnitTests.Email;

public class ManageTokensTests
{
    private static ManageTokens Tokens(string secret = "secret-one-0123456789-0123456789-0123") =>
        new(Options.Create(new AppOptions { PublicBaseUrl = "https://kalora.app", ManageTokenSecret = secret }));

    [Fact]
    public void The_same_appointment_always_gets_the_same_token()
    {
        var id = Guid.NewGuid();

        Assert.Equal(Tokens().Create(id), Tokens().Create(id));
    }

    [Fact]
    public void Different_appointments_get_different_tokens()
    {
        Assert.NotEqual(Tokens().Create(Guid.NewGuid()), Tokens().Create(Guid.NewGuid()));
    }

    [Fact]
    public void A_different_secret_gives_a_different_token()
    {
        var id = Guid.NewGuid();

        Assert.NotEqual(Tokens("secret-one-0123456789-0123456789-0123").Create(id), Tokens("secret-two-0123456789-0123456789-0123").Create(id));
    }

    [Fact]
    public void A_token_is_url_safe_and_long_enough_to_be_unguessable()
    {
        var token = Tokens().Create(Guid.CreateVersion7());

        Assert.Equal(43, token.Length); // 32 bytes, Base64Url without padding
        Assert.Matches("^[A-Za-z0-9_-]+$", token);
    }

    [Fact]
    public void The_token_does_not_contain_the_appointment_id()
    {
        var id = Guid.NewGuid();

        var token = Tokens().Create(id);

        Assert.DoesNotContain(id.ToString("N"), token, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(TokenHasher.Hash(token), token);
    }
}
