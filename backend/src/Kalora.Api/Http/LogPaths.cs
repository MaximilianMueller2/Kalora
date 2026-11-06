namespace Kalora.Api.Http;

/// <summary>
/// The request log names the path of each request. Some paths carry a secret (the management link of an appointment, the token of an
/// invitation), and a log file is not the place for those: they are replaced by <c>{token}</c>.
/// </summary>
public static class LogPaths
{
    public const string TokenMask = "{token}";

    private static readonly string[] TokenPrefixes = ["/api/public/appointments/", "/api/public/invitations/"];

    public static string Redact(string path)
    {
        foreach (var prefix in TokenPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var rest = path[prefix.Length..];
                var slash = rest.IndexOf('/', StringComparison.Ordinal);
                return path[..prefix.Length] + TokenMask + (slash < 0 ? string.Empty : rest[slash..]);
            }
        }

        return path;
    }
}
