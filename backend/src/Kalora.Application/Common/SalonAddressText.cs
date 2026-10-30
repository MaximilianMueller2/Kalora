using Kalora.Domain.Salons;

namespace Kalora.Application.Common;

internal static class SalonAddressText
{
    /// <summary>"Street 1, 10115 Berlin" on one line for mails and calendar entries; parts that are empty are left out.</summary>
    public static string Format(Salon salon)
    {
        var city = string.Join(' ', new[] { salon.PostalCode, salon.City }.Where(part => !string.IsNullOrWhiteSpace(part)));
        return string.Join(", ", new[] { salon.Street, city }.Where(part => !string.IsNullOrWhiteSpace(part)));
    }
}
