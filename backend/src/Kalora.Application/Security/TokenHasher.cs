using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Kalora.Application.Security;

/// <summary>Random link tokens (32 bytes) of which only the SHA-256 hash is stored.</summary>
public static class TokenHasher
{
    public static string NewToken() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
