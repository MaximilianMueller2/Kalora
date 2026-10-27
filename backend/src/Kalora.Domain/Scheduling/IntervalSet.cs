namespace Kalora.Domain.Scheduling;

/// <summary>
/// Set operations on lists of intervals. Every result is normalized: sorted, free of empty intervals, and
/// intervals that overlap or touch are merged (9-13 and 13-18 become 9-18).
/// </summary>
public static class IntervalSet
{
    public static IReadOnlyList<Interval> Union(IEnumerable<Interval> intervals)
    {
        var sorted = intervals.Where(i => !i.IsEmpty).OrderBy(i => i.Start).ToList();
        var merged = new List<Interval>(sorted.Count);
        foreach (var interval in sorted)
        {
            if (merged.Count > 0 && interval.Start <= merged[^1].End)
            {
                var last = merged[^1];
                merged[^1] = new Interval(last.Start, interval.End > last.End ? interval.End : last.End);
            }
            else
            {
                merged.Add(interval);
            }
        }

        return merged;
    }

    public static IReadOnlyList<Interval> Intersect(IEnumerable<Interval> first, IEnumerable<Interval> second)
    {
        var a = Union(first);
        var b = Union(second);
        var result = new List<Interval>();
        int i = 0, j = 0;
        while (i < a.Count && j < b.Count)
        {
            var start = a[i].Start > b[j].Start ? a[i].Start : b[j].Start;
            var end = a[i].End < b[j].End ? a[i].End : b[j].End;
            if (start < end)
            {
                result.Add(new Interval(start, end));
            }

            // Move on in the list whose interval ends first.
            if (a[i].End < b[j].End)
            {
                i++;
            }
            else
            {
                j++;
            }
        }

        return result;
    }

    /// <summary>The time in <paramref name="from"/> that is not in <paramref name="remove"/>.</summary>
    public static IReadOnlyList<Interval> Subtract(IEnumerable<Interval> from, IEnumerable<Interval> remove)
    {
        var a = Union(from);
        var b = Union(remove);
        var result = new List<Interval>();
        var j = 0;
        foreach (var interval in a)
        {
            var cursor = interval.Start;
            while (j < b.Count && b[j].End <= cursor)
            {
                j++;
            }

            for (var k = j; k < b.Count && b[k].Start < interval.End; k++)
            {
                if (b[k].Start > cursor)
                {
                    result.Add(new Interval(cursor, b[k].Start));
                }

                if (b[k].End > cursor)
                {
                    cursor = b[k].End;
                }
            }

            if (cursor < interval.End)
            {
                result.Add(new Interval(cursor, interval.End));
            }
        }

        return result;
    }

    /// <summary>True if one interval of the normalized list <paramref name="set"/> contains <paramref name="span"/> completely.</summary>
    public static bool Covers(IReadOnlyList<Interval> set, Interval span)
    {
        // Binary search for the last interval that starts at or before the span.
        int low = 0, high = set.Count - 1, found = -1;
        while (low <= high)
        {
            var middle = (low + high) / 2;
            if (set[middle].Start <= span.Start)
            {
                found = middle;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return found >= 0 && set[found].Covers(span);
    }
}
