namespace Kalora.Domain.Scheduling;

/// <summary>
/// Wall-clock time in a salon's time zone to UTC instants and back. Daylight saving changes are handled
/// explicitly: a local time that does not exist (the hour skipped in spring) is pushed forward, one that
/// happens twice (the hour repeated in autumn) refers to both instants where the caller asks for all of them.
/// </summary>
internal static class ZoneClock
{
    public static DateOnly LocalDate(DateTimeOffset instant, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);

    /// <summary>Start of a block of local time: the earlier instant if the time happens twice.</summary>
    public static DateTimeOffset BlockStart(DateOnly date, TimeOnly time, TimeZoneInfo zone) =>
        Resolve(date, time, zone, useLaterInstant: false);

    /// <summary>End of a block of local time: the later instant if the time happens twice, so the block covers the whole wall-clock span.</summary>
    public static DateTimeOffset BlockEnd(DateOnly date, TimeOnly time, TimeZoneInfo zone) =>
        Resolve(date, time, zone, useLaterInstant: true);

    /// <summary>Every instant at which the clocks show this local time: none in the skipped hour, two in the repeated one.</summary>
    public static IEnumerable<DateTimeOffset> Instants(DateOnly date, TimeOnly time, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local))
        {
            yield break;
        }

        if (zone.IsAmbiguousTime(local))
        {
            foreach (var offset in zone.GetAmbiguousTimeOffsets(local).OrderByDescending(o => o))
            {
                yield return new DateTimeOffset(local, offset).ToUniversalTime();
            }

            yield break;
        }

        yield return new DateTimeOffset(local, zone.GetUtcOffset(local)).ToUniversalTime();
    }

    private static DateTimeOffset Resolve(DateOnly date, TimeOnly time, TimeZoneInfo zone, bool useLaterInstant)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local))
        {
            // 02:30 on the day the clocks jump from 02:00 to 03:00 is read with the offset from before the jump: 03:30.
            return new DateTimeOffset(local, zone.GetUtcOffset(local.AddDays(-1))).ToUniversalTime();
        }

        if (zone.IsAmbiguousTime(local))
        {
            var offsets = zone.GetAmbiguousTimeOffsets(local);
            // The larger offset is daylight saving time, i.e. the earlier instant.
            var offset = useLaterInstant ? offsets.Min() : offsets.Max();
            return new DateTimeOffset(local, offset).ToUniversalTime();
        }

        return new DateTimeOffset(local, zone.GetUtcOffset(local)).ToUniversalTime();
    }
}
