using Kalora.Application.Security;

namespace Kalora.UnitTests.Security;

public class TokenHasherTests
{
    [Fact]
    public void Tokens_are_random_url_safe_and_long_enough()
    {
        var tokens = Enumerable.Range(0, 50).Select(_ => TokenHasher.NewToken()).ToList();

        Assert.Equal(50, tokens.Distinct().Count());
        Assert.All(tokens, token =>
        {
            Assert.Equal(43, token.Length); // 32 bytes, base64url without padding
            Assert.Matches("^[A-Za-z0-9_-]+$", token);
        });
    }

    [Fact]
    public void The_hash_is_stable_hex_and_differs_from_the_token()
    {
        var hash = TokenHasher.Hash("abc");

        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", hash);
        Assert.Equal(hash, TokenHasher.Hash("abc"));
        Assert.NotEqual(hash, TokenHasher.Hash("abd"));
    }
}
