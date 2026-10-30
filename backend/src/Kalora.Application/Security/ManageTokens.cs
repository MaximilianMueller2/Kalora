using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Kalora.Application.Configuration;
using Microsoft.Extensions.Options;

namespace Kalora.Application.Security;

/// <summary>The secret link of an appointment, for the customer's own page.</summary>
public interface IManageTokens
{
    /// <summary>
    /// The token for an appointment: derived from its ID and the server secret, so it is the same every time and never
    /// has to be stored. Store only <see cref="TokenHasher.Hash"/> of it to find the appointment again.
    /// </summary>
    string Create(Guid appointmentId);
}

internal sealed class ManageTokens(IOptions<AppOptions> options) : IManageTokens
{
    public string Create(Guid appointmentId)
    {
        var key = Encoding.UTF8.GetBytes(options.Value.ManageTokenSecret);
        var data = Encoding.UTF8.GetBytes($"appointment-manage:{appointmentId:N}");
        return Base64Url.EncodeToString(HMACSHA256.HashData(key, data));
    }
}
