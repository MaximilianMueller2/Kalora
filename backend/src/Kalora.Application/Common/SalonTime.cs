namespace Kalora.Application.Common;

/// <summary>Conversions between UTC instants and calendar days in a salon's own time zone.</summary>
internal static class SalonTime
{
    public static DateOnly LocalDate(DateTimeOffset utc, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utc, zone).DateTime);

    /// <summary>Midnight at the start of <paramref name="date"/> as a UTC instant (some zones skip midnight on DST days).</summary>
    public static DateTimeOffset StartOfDayUtc(DateOnly date, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local))
        {
            local = local.AddHours(1);
        }

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone), TimeSpan.Zero);
    }
}
