namespace Kalora.Domain.Scheduling;

/// <summary>A half-open span of time [<see cref="Start"/>, <see cref="End"/>) between two UTC instants.</summary>
public readonly record struct Interval
{
    public Interval(DateTimeOffset start, DateTimeOffset end)
    {
        if (end < start)
        {
            throw new ArgumentException("An interval must not end before it starts.", nameof(end));
        }

        Start = start.ToUniversalTime();
        End = end.ToUniversalTime();
    }

    public DateTimeOffset Start { get; }

    public DateTimeOffset End { get; }

    public TimeSpan Length => End - Start;

    public bool IsEmpty => End <= Start;

    /// <summary>True if <paramref name="other"/> lies completely inside this interval (touching the edges is fine).</summary>
    public bool Covers(Interval other) => other.Start >= Start && other.End <= End;

    /// <summary>True if both share some time; intervals that merely touch do not overlap.</summary>
    public bool Overlaps(Interval other) => Start < other.End && other.Start < End;
}
