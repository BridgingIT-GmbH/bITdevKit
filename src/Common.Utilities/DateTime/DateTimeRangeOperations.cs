// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;
using System.Globalization;

/// <summary>
/// Provides relational and set operations for date and time ranges.
/// </summary>
/// <remarks><example><code>var overlap = first.Intersection(second);</code></example></remarks>
public static class DateTimeRangeOperations
{
    /// <summary>Determines whether the first range ends before or exactly at the second range start.</summary>
    /// <param name="source">The first range.</param>
    /// <param name="other">The second range.</param>
    /// <returns><c>true</c> when the first range is before the second range.</returns>
    /// <remarks><example><code>var before = first.IsBefore(second);</code></example></remarks>
    [DebuggerStepThrough]
    public static bool IsBefore(this DateTimeRange source, DateTimeRange other)
        => source.EndExclusive.HasValue && other.StartInclusive.HasValue && source.EndExclusive.Value <= other.StartInclusive.Value;

    /// <summary>Determines whether the first range starts after or exactly at the second range end.</summary>
    /// <param name="source">The first range.</param>
    /// <param name="other">The second range.</param>
    /// <returns><c>true</c> when the first range is after the second range.</returns>
    /// <remarks><example><code>var after = first.IsAfter(second);</code></example></remarks>
    [DebuggerStepThrough]
    public static bool IsAfter(this DateTimeRange source, DateTimeRange other) => other.IsBefore(source);

    /// <summary>Determines whether two ranges touch at a boundary without overlapping.</summary>
    /// <param name="source">The first range.</param>
    /// <param name="other">The second range.</param>
    /// <returns><c>true</c> when the ranges are adjacent.</returns>
    /// <remarks><example><code>var adjacent = first.Touches(second);</code></example></remarks>
    [DebuggerStepThrough]
    public static bool Touches(this DateTimeRange source, DateTimeRange other)
        => (source.EndExclusive.HasValue && other.StartInclusive.HasValue && source.EndExclusive.Value == other.StartInclusive.Value) ||
            (other.EndExclusive.HasValue && source.StartInclusive.HasValue && other.EndExclusive.Value == source.StartInclusive.Value);

    /// <summary>Determines whether two ranges are adjacent.</summary>
    /// <param name="source">The first range.</param>
    /// <param name="other">The second range.</param>
    /// <returns><c>true</c> when the ranges touch but do not overlap.</returns>
    /// <remarks><example><code>var adjacent = first.IsAdjacentTo(second);</code></example></remarks>
    [DebuggerStepThrough]
    public static bool IsAdjacentTo(this DateTimeRange source, DateTimeRange other) => source.Touches(other);

    /// <summary>Determines whether the source range starts before another range.</summary>
    /// <param name="source">The source range.</param>
    /// <param name="other">The other range.</param>
    /// <returns><c>true</c> when the source starts before the other range.</returns>
    /// <remarks><example><code>var startsBefore = first.StartsBefore(second);</code></example></remarks>
    [DebuggerStepThrough]
    public static bool StartsBefore(this DateTimeRange source, DateTimeRange other) => CompareStarts(source.StartInclusive, other.StartInclusive) < 0;

    /// <summary>Determines whether the source range ends after another range.</summary>
    /// <param name="source">The source range.</param>
    /// <param name="other">The other range.</param>
    /// <returns><c>true</c> when the source ends after the other range.</returns>
    /// <remarks><example><code>var endsAfter = first.EndsAfter(second);</code></example></remarks>
    [DebuggerStepThrough]
    public static bool EndsAfter(this DateTimeRange source, DateTimeRange other) => CompareEnds(source.EndExclusive, other.EndExclusive) > 0;

    /// <summary>Determines whether the source range fully contains another range.</summary>
    /// <param name="source">The containing range.</param>
    /// <param name="other">The range to test.</param>
    /// <returns><c>true</c> when the source contains the other range.</returns>
    /// <remarks><example><code>var contains = outer.Contains(inner);</code></example></remarks>
    [DebuggerStepThrough]
    public static bool Contains(this DateTimeRange source, DateTimeRange other)
        => CompareStarts(source.StartInclusive, other.StartInclusive) <= 0 && CompareEnds(source.EndExclusive, other.EndExclusive) >= 0;

    /// <summary>Returns the half-open intersection of two ranges.</summary>
    /// <param name="source">The first range.</param>
    /// <param name="other">The second range.</param>
    /// <returns>The intersection, or <c>null</c> when the ranges do not overlap.</returns>
    /// <remarks><example><code>var intersection = first.Intersection(second);</code></example></remarks>
    [DebuggerStepThrough]
    public static DateTimeRange? Intersection(this DateTimeRange source, DateTimeRange other)
    {
        var start = MaxStart(source.StartInclusive, other.StartInclusive);
        var end = MinEnd(source.EndExclusive, other.EndExclusive);

        return HasPositiveLength(start, end) ? new DateTimeRange(start, end) : null;
    }

    /// <summary>Attempts to return the half-open intersection of two ranges.</summary>
    /// <param name="source">The first range.</param>
    /// <param name="other">The second range.</param>
    /// <param name="result">The intersection when one exists.</param>
    /// <returns><c>true</c> when an intersection exists.</returns>
    /// <remarks><example><code>if (first.TryIntersection(second, out var result)) { }</code></example></remarks>
    [DebuggerStepThrough]
    public static bool TryIntersection(this DateTimeRange source, DateTimeRange other, out DateTimeRange result)
    {
        var intersection = source.Intersection(other);
        result = intersection.GetValueOrDefault();
        return intersection.HasValue;
    }

    /// <summary>Returns the union of two overlapping or adjacent ranges.</summary>
    /// <param name="source">The first range.</param>
    /// <param name="other">The second range.</param>
    /// <returns>The union, or <c>null</c> when the ranges are disjoint.</returns>
    /// <remarks><example><code>var union = first.Union(second);</code></example></remarks>
    [DebuggerStepThrough]
    public static DateTimeRange? Union(this DateTimeRange source, DateTimeRange other)
    {
        if (!source.Overlaps(other) && !source.Touches(other))
        {
            return null;
        }

        var start = MinStart(source.StartInclusive, other.StartInclusive);
        var end = MaxEnd(source.EndExclusive, other.EndExclusive);
        return !start.HasValue && !end.HasValue ? null : new DateTimeRange(start, end);
    }

    /// <summary>Attempts to merge two overlapping or adjacent ranges.</summary>
    /// <param name="source">The first range.</param>
    /// <param name="other">The second range.</param>
    /// <param name="result">The merged range when merge succeeds.</param>
    /// <returns><c>true</c> when the ranges overlap or touch.</returns>
    /// <remarks><example><code>if (first.TryMerge(second, out var merged)) { }</code></example></remarks>
    [DebuggerStepThrough]
    public static bool TryMerge(this DateTimeRange source, DateTimeRange other, out DateTimeRange result)
    {
        var union = source.Union(other);
        result = union.GetValueOrDefault();
        return union.HasValue;
    }

    /// <summary>Returns the finite gap between two disjoint ranges.</summary>
    /// <param name="source">The first range.</param>
    /// <param name="other">The second range.</param>
    /// <returns>The gap, or <c>null</c> when ranges overlap, touch, or have no finite gap.</returns>
    /// <remarks><example><code>var gap = first.Gap(second);</code></example></remarks>
    [DebuggerStepThrough]
    public static DateTimeRange? Gap(this DateTimeRange source, DateTimeRange other)
    {
        if (source.Overlaps(other) || source.Touches(other))
        {
            return null;
        }

        var ordered = source.CompareTo(other) <= 0 ? (First: source, Second: other) : (First: other, Second: source);
        return ordered.First.EndExclusive.HasValue && ordered.Second.StartInclusive.HasValue &&
            ordered.First.EndExclusive.Value < ordered.Second.StartInclusive.Value
            ? new DateTimeRange(ordered.First.EndExclusive.Value, ordered.Second.StartInclusive.Value)
            : null;
    }

    /// <summary>Sorts and merges overlapping or adjacent ranges.</summary>
    /// <param name="source">The ranges to normalize.</param>
    /// <returns>A normalized list of ranges.</returns>
    /// <remarks><example><code>var normalized = ranges.Normalize();</code></example></remarks>
    [DebuggerStepThrough]
    public static IReadOnlyList<DateTimeRange> Normalize(this IEnumerable<DateTimeRange> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var ordered = source.Order().ToArray();
        if (ordered.Length == 0)
        {
            return [];
        }

        var result = new List<DateTimeRange>();
        var current = ordered[0];
        foreach (var range in ordered.Skip(1))
        {
            if (current.TryMerge(range, out var merged))
            {
                current = merged;
                continue;
            }

            result.Add(current);
            current = range;
        }

        result.Add(current);
        return result;
    }

    /// <summary>Splits a finite date/time range into day-bounded ranges.</summary>
    /// <param name="source">The finite source range.</param>
    /// <returns>Day-bounded ranges clipped to the source range.</returns>
    /// <remarks><example><code>var days = range.SplitByDay();</code></example></remarks>
    /// <exception cref="InvalidOperationException">Thrown when the range has an open boundary.</exception>
    [DebuggerStepThrough]
    public static IEnumerable<DateTimeRange> SplitByDay(this DateTimeRange source)
    {
        EnsureFinite(source);

        var cursor = source.StartInclusive.Value;
        while (cursor < source.EndExclusive.Value)
        {
            var next = cursor.StartOfDay().AddDays(1);
            if (next > source.EndExclusive.Value)
            {
                next = source.EndExclusive.Value;
            }

            yield return new DateTimeRange(cursor, next);
            cursor = next;
        }
    }

    /// <summary>Splits a finite date/time range into month-bounded ranges.</summary>
    /// <param name="source">The finite source range.</param>
    /// <returns>Month-bounded ranges clipped to the source range.</returns>
    /// <remarks><example><code>var months = range.SplitByMonth();</code></example></remarks>
    /// <exception cref="InvalidOperationException">Thrown when the range has an open boundary.</exception>
    [DebuggerStepThrough]
    public static IEnumerable<DateTimeRange> SplitByMonth(this DateTimeRange source)
    {
        EnsureFinite(source);

        var cursor = source.StartInclusive.Value;
        while (cursor < source.EndExclusive.Value)
        {
            var monthStart = cursor.StartOfMonth();
            var next = monthStart == cursor ? cursor.AddMonths(1) : monthStart.AddMonths(1);
            if (next > source.EndExclusive.Value)
            {
                next = source.EndExclusive.Value;
            }

            yield return new DateTimeRange(cursor, next);
            cursor = next;
        }
    }

    /// <summary>Converts a local wall-clock date/time range to a date/time-offset range using time-zone rules.</summary>
    /// <param name="source">The local wall-clock range.</param>
    /// <param name="timeZone">The time zone.</param>
    /// <param name="invalidTimePolicy">The invalid-time policy.</param>
    /// <param name="ambiguousTimePolicy">The ambiguous-time policy.</param>
    /// <returns>The converted date/time-offset range.</returns>
    /// <remarks><example><code>var range = localRange.ToDateTimeOffsetRange(timeZone);</code></example></remarks>
    [DebuggerStepThrough]
    public static DateTimeOffsetRange ToDateTimeOffsetRange(
        this DateTimeRange source,
        TimeZoneInfo timeZone,
        InvalidTimePolicy invalidTimePolicy = InvalidTimePolicy.Throw,
        AmbiguousTimePolicy ambiguousTimePolicy = AmbiguousTimePolicy.Throw)
    {
        ArgumentNullException.ThrowIfNull(timeZone);

        return new DateTimeOffsetRange(
            source.StartInclusive.HasValue ? source.StartInclusive.Value.ToDateTimeOffset(timeZone, invalidTimePolicy, ambiguousTimePolicy) : null,
            source.EndExclusive.HasValue ? source.EndExclusive.Value.ToDateTimeOffset(timeZone, invalidTimePolicy, ambiguousTimePolicy) : null);
    }

    /// <summary>Formats a date/time range as an ISO interval string.</summary>
    /// <param name="source">The source range.</param>
    /// <returns>The ISO interval string.</returns>
    /// <remarks><example><code>var text = range.ToIsoRangeString();</code></example></remarks>
    [DebuggerStepThrough]
    public static string ToIsoRangeString(this DateTimeRange source)
        => $"{FormatDateTimeBoundary(source.StartInclusive)}/{FormatDateTimeBoundary(source.EndExclusive)}";

    /// <summary>Attempts to parse an ISO date/time interval string.</summary>
    /// <param name="source">The source interval text.</param>
    /// <param name="result">The parsed range when parsing succeeds.</param>
    /// <returns><c>true</c> when parsing succeeds.</returns>
    /// <remarks><example><code>if ("2026-01-01T00:00:00Z/".TryParseDateTimeRange(out var range)) { }</code></example></remarks>
    [DebuggerStepThrough]
    public static bool TryParseDateTimeRange(this string source, out DateTimeRange result)
    {
        result = default;
        if (!TrySplitRange(source, out var startText, out var endText))
        {
            return false;
        }

        if (!TryParseDateTimeBoundary(startText, out var start) || !TryParseDateTimeBoundary(endText, out var end))
        {
            return false;
        }

        if (!start.HasValue && !end.HasValue)
        {
            return false;
        }

        try
        {
            result = new DateTimeRange(start, end);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>Determines whether the first range ends before or exactly at the second range start.</summary>
    [DebuggerStepThrough]
    public static bool IsBefore(this DateTimeOffsetRange source, DateTimeOffsetRange other)
        => source.EndExclusive.HasValue && other.StartInclusive.HasValue && source.EndExclusive.Value <= other.StartInclusive.Value;

    /// <summary>Determines whether the first range starts after or exactly at the second range end.</summary>
    [DebuggerStepThrough]
    public static bool IsAfter(this DateTimeOffsetRange source, DateTimeOffsetRange other) => other.IsBefore(source);

    /// <summary>Determines whether two ranges touch at a boundary without overlapping.</summary>
    [DebuggerStepThrough]
    public static bool Touches(this DateTimeOffsetRange source, DateTimeOffsetRange other)
        => (source.EndExclusive.HasValue && other.StartInclusive.HasValue && source.EndExclusive.Value == other.StartInclusive.Value) ||
            (other.EndExclusive.HasValue && source.StartInclusive.HasValue && other.EndExclusive.Value == source.StartInclusive.Value);

    /// <summary>Determines whether two ranges are adjacent.</summary>
    [DebuggerStepThrough]
    public static bool IsAdjacentTo(this DateTimeOffsetRange source, DateTimeOffsetRange other) => source.Touches(other);

    /// <summary>Determines whether the source range starts before another range.</summary>
    [DebuggerStepThrough]
    public static bool StartsBefore(this DateTimeOffsetRange source, DateTimeOffsetRange other) => CompareStarts(source.StartInclusive, other.StartInclusive) < 0;

    /// <summary>Determines whether the source range ends after another range.</summary>
    [DebuggerStepThrough]
    public static bool EndsAfter(this DateTimeOffsetRange source, DateTimeOffsetRange other) => CompareEnds(source.EndExclusive, other.EndExclusive) > 0;

    /// <summary>Determines whether the source range fully contains another range.</summary>
    [DebuggerStepThrough]
    public static bool Contains(this DateTimeOffsetRange source, DateTimeOffsetRange other)
        => CompareStarts(source.StartInclusive, other.StartInclusive) <= 0 && CompareEnds(source.EndExclusive, other.EndExclusive) >= 0;

    /// <summary>Returns the half-open intersection of two ranges.</summary>
    [DebuggerStepThrough]
    public static DateTimeOffsetRange? Intersection(this DateTimeOffsetRange source, DateTimeOffsetRange other)
    {
        var start = MaxStart(source.StartInclusive, other.StartInclusive);
        var end = MinEnd(source.EndExclusive, other.EndExclusive);

        return HasPositiveLength(start, end) ? new DateTimeOffsetRange(start, end) : null;
    }

    /// <summary>Attempts to return the half-open intersection of two ranges.</summary>
    [DebuggerStepThrough]
    public static bool TryIntersection(this DateTimeOffsetRange source, DateTimeOffsetRange other, out DateTimeOffsetRange result)
    {
        var intersection = source.Intersection(other);
        result = intersection.GetValueOrDefault();
        return intersection.HasValue;
    }

    /// <summary>Returns the union of two overlapping or adjacent ranges.</summary>
    [DebuggerStepThrough]
    public static DateTimeOffsetRange? Union(this DateTimeOffsetRange source, DateTimeOffsetRange other)
    {
        if (!source.Overlaps(other) && !source.Touches(other))
        {
            return null;
        }

        var start = MinStart(source.StartInclusive, other.StartInclusive);
        var end = MaxEnd(source.EndExclusive, other.EndExclusive);
        return !start.HasValue && !end.HasValue ? null : new DateTimeOffsetRange(start, end);
    }

    /// <summary>Attempts to merge two overlapping or adjacent ranges.</summary>
    [DebuggerStepThrough]
    public static bool TryMerge(this DateTimeOffsetRange source, DateTimeOffsetRange other, out DateTimeOffsetRange result)
    {
        var union = source.Union(other);
        result = union.GetValueOrDefault();
        return union.HasValue;
    }

    /// <summary>Returns the finite gap between two disjoint ranges.</summary>
    [DebuggerStepThrough]
    public static DateTimeOffsetRange? Gap(this DateTimeOffsetRange source, DateTimeOffsetRange other)
    {
        if (source.Overlaps(other) || source.Touches(other))
        {
            return null;
        }

        var ordered = source.CompareTo(other) <= 0 ? (First: source, Second: other) : (First: other, Second: source);
        return ordered.First.EndExclusive.HasValue && ordered.Second.StartInclusive.HasValue &&
            ordered.First.EndExclusive.Value < ordered.Second.StartInclusive.Value
            ? new DateTimeOffsetRange(ordered.First.EndExclusive.Value, ordered.Second.StartInclusive.Value)
            : null;
    }

    /// <summary>Sorts and merges overlapping or adjacent ranges.</summary>
    [DebuggerStepThrough]
    public static IReadOnlyList<DateTimeOffsetRange> Normalize(this IEnumerable<DateTimeOffsetRange> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var ordered = source.Order().ToArray();
        if (ordered.Length == 0)
        {
            return [];
        }

        var result = new List<DateTimeOffsetRange>();
        var current = ordered[0];
        foreach (var range in ordered.Skip(1))
        {
            if (current.TryMerge(range, out var merged))
            {
                current = merged;
                continue;
            }

            result.Add(current);
            current = range;
        }

        result.Add(current);
        return result;
    }

    /// <summary>Converts a date/time-offset range to another time zone while preserving instants.</summary>
    /// <param name="source">The source range.</param>
    /// <param name="timeZone">The target time zone.</param>
    /// <returns>The range represented in the target time zone.</returns>
    /// <remarks><example><code>var berlin = range.ToTimeZone(timeZone);</code></example></remarks>
    [DebuggerStepThrough]
    public static DateTimeOffsetRange ToTimeZone(this DateTimeOffsetRange source, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);

        return new DateTimeOffsetRange(
            source.StartInclusive.HasValue ? TimeZoneInfo.ConvertTime(source.StartInclusive.Value, timeZone) : null,
            source.EndExclusive.HasValue ? TimeZoneInfo.ConvertTime(source.EndExclusive.Value, timeZone) : null);
    }

    /// <summary>Formats a date/time-offset range as an ISO interval string.</summary>
    /// <param name="source">The source range.</param>
    /// <returns>The ISO interval string.</returns>
    /// <remarks><example><code>var text = range.ToIsoRangeString();</code></example></remarks>
    [DebuggerStepThrough]
    public static string ToIsoRangeString(this DateTimeOffsetRange source)
        => $"{FormatDateTimeOffsetBoundary(source.StartInclusive)}/{FormatDateTimeOffsetBoundary(source.EndExclusive)}";

    /// <summary>Attempts to parse an ISO date/time-offset interval string.</summary>
    /// <param name="source">The source interval text.</param>
    /// <param name="result">The parsed range when parsing succeeds.</param>
    /// <returns><c>true</c> when parsing succeeds.</returns>
    /// <remarks><example><code>if ("2026-01-01T00:00:00+01:00/".TryParseDateTimeOffsetRange(out var range)) { }</code></example></remarks>
    [DebuggerStepThrough]
    public static bool TryParseDateTimeOffsetRange(this string source, out DateTimeOffsetRange result)
    {
        result = default;
        if (!TrySplitRange(source, out var startText, out var endText))
        {
            return false;
        }

        if (!TryParseDateTimeOffsetBoundary(startText, out var start) || !TryParseDateTimeOffsetBoundary(endText, out var end))
        {
            return false;
        }

        if (!start.HasValue && !end.HasValue)
        {
            return false;
        }

        try
        {
            result = new DateTimeOffsetRange(start, end);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>Determines whether the first range ends before or exactly at the second range start.</summary>
    [DebuggerStepThrough]
    public static bool IsBefore(this DateOnlyRange source, DateOnlyRange other)
        => source.EndExclusive.HasValue && other.StartInclusive.HasValue && source.EndExclusive.Value <= other.StartInclusive.Value;

    /// <summary>Determines whether the first range starts after or exactly at the second range end.</summary>
    [DebuggerStepThrough]
    public static bool IsAfter(this DateOnlyRange source, DateOnlyRange other) => other.IsBefore(source);

    /// <summary>Determines whether two ranges touch at a boundary without overlapping.</summary>
    [DebuggerStepThrough]
    public static bool Touches(this DateOnlyRange source, DateOnlyRange other)
        => (source.EndExclusive.HasValue && other.StartInclusive.HasValue && source.EndExclusive.Value == other.StartInclusive.Value) ||
            (other.EndExclusive.HasValue && source.StartInclusive.HasValue && other.EndExclusive.Value == source.StartInclusive.Value);

    /// <summary>Determines whether two ranges are adjacent.</summary>
    [DebuggerStepThrough]
    public static bool IsAdjacentTo(this DateOnlyRange source, DateOnlyRange other) => source.Touches(other);

    /// <summary>Determines whether the source range starts before another range.</summary>
    [DebuggerStepThrough]
    public static bool StartsBefore(this DateOnlyRange source, DateOnlyRange other) => CompareStarts(source.StartInclusive, other.StartInclusive) < 0;

    /// <summary>Determines whether the source range ends after another range.</summary>
    [DebuggerStepThrough]
    public static bool EndsAfter(this DateOnlyRange source, DateOnlyRange other) => CompareEnds(source.EndExclusive, other.EndExclusive) > 0;

    /// <summary>Determines whether the source range fully contains another range.</summary>
    [DebuggerStepThrough]
    public static bool Contains(this DateOnlyRange source, DateOnlyRange other)
        => CompareStarts(source.StartInclusive, other.StartInclusive) <= 0 && CompareEnds(source.EndExclusive, other.EndExclusive) >= 0;

    /// <summary>Returns the half-open intersection of two ranges.</summary>
    [DebuggerStepThrough]
    public static DateOnlyRange? Intersection(this DateOnlyRange source, DateOnlyRange other)
    {
        var start = MaxStart(source.StartInclusive, other.StartInclusive);
        var end = MinEnd(source.EndExclusive, other.EndExclusive);

        return HasPositiveLength(start, end) ? new DateOnlyRange(start, end) : null;
    }

    /// <summary>Attempts to return the half-open intersection of two ranges.</summary>
    [DebuggerStepThrough]
    public static bool TryIntersection(this DateOnlyRange source, DateOnlyRange other, out DateOnlyRange result)
    {
        var intersection = source.Intersection(other);
        result = intersection.GetValueOrDefault();
        return intersection.HasValue;
    }

    /// <summary>Returns the union of two overlapping or adjacent ranges.</summary>
    [DebuggerStepThrough]
    public static DateOnlyRange? Union(this DateOnlyRange source, DateOnlyRange other)
    {
        if (!source.Overlaps(other) && !source.Touches(other))
        {
            return null;
        }

        var start = MinStart(source.StartInclusive, other.StartInclusive);
        var end = MaxEnd(source.EndExclusive, other.EndExclusive);
        return !start.HasValue && !end.HasValue ? null : new DateOnlyRange(start, end);
    }

    /// <summary>Attempts to merge two overlapping or adjacent ranges.</summary>
    [DebuggerStepThrough]
    public static bool TryMerge(this DateOnlyRange source, DateOnlyRange other, out DateOnlyRange result)
    {
        var union = source.Union(other);
        result = union.GetValueOrDefault();
        return union.HasValue;
    }

    /// <summary>Returns the finite gap between two disjoint ranges.</summary>
    [DebuggerStepThrough]
    public static DateOnlyRange? Gap(this DateOnlyRange source, DateOnlyRange other)
    {
        if (source.Overlaps(other) || source.Touches(other))
        {
            return null;
        }

        var ordered = source.CompareTo(other) <= 0 ? (First: source, Second: other) : (First: other, Second: source);
        return ordered.First.EndExclusive.HasValue && ordered.Second.StartInclusive.HasValue &&
            ordered.First.EndExclusive.Value < ordered.Second.StartInclusive.Value
            ? new DateOnlyRange(ordered.First.EndExclusive.Value, ordered.Second.StartInclusive.Value)
            : null;
    }

    /// <summary>Sorts and merges overlapping or adjacent ranges.</summary>
    [DebuggerStepThrough]
    public static IReadOnlyList<DateOnlyRange> Normalize(this IEnumerable<DateOnlyRange> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var ordered = source.Order().ToArray();
        if (ordered.Length == 0)
        {
            return [];
        }

        var result = new List<DateOnlyRange>();
        var current = ordered[0];
        foreach (var range in ordered.Skip(1))
        {
            if (current.TryMerge(range, out var merged))
            {
                current = merged;
                continue;
            }

            result.Add(current);
            current = range;
        }

        result.Add(current);
        return result;
    }

    /// <summary>Enumerates every date in a finite half-open date range.</summary>
    /// <param name="source">The finite source range.</param>
    /// <returns>The dates in the range.</returns>
    /// <remarks><example><code>foreach (var day in range.EachDay()) { }</code></example></remarks>
    /// <exception cref="InvalidOperationException">Thrown when the range has an open boundary.</exception>
    [DebuggerStepThrough]
    public static IEnumerable<DateOnly> EachDay(this DateOnlyRange source)
    {
        EnsureFinite(source);

        for (var date = source.StartInclusive.Value; date < source.EndExclusive.Value; date = date.AddDays(1))
        {
            yield return date;
        }
    }

    /// <summary>Splits a finite date range into month-bounded ranges.</summary>
    /// <param name="source">The finite source range.</param>
    /// <returns>Month-bounded ranges clipped to the source range.</returns>
    /// <remarks><example><code>var months = range.SplitByMonth();</code></example></remarks>
    /// <exception cref="InvalidOperationException">Thrown when the range has an open boundary.</exception>
    [DebuggerStepThrough]
    public static IEnumerable<DateOnlyRange> SplitByMonth(this DateOnlyRange source)
    {
        EnsureFinite(source);

        var cursor = source.StartInclusive.Value;
        while (cursor < source.EndExclusive.Value)
        {
            var monthStart = cursor.StartOfMonth();
            var next = monthStart == cursor ? cursor.AddMonths(1) : monthStart.AddMonths(1);
            if (next > source.EndExclusive.Value)
            {
                next = source.EndExclusive.Value;
            }

            yield return new DateOnlyRange(cursor, next);
            cursor = next;
        }
    }

    /// <summary>Enumerates business days in a finite date range.</summary>
    /// <param name="source">The finite source range.</param>
    /// <param name="calendar">The business calendar.</param>
    /// <returns>The business dates in the range.</returns>
    /// <remarks><example><code>var days = range.BusinessDays(calendar);</code></example></remarks>
    /// <exception cref="InvalidOperationException">Thrown when the range has an open boundary.</exception>
    [DebuggerStepThrough]
    public static IEnumerable<DateOnly> BusinessDays(this DateOnlyRange source, IBusinessCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        return source.EachDay().Where(calendar.IsBusinessDay);
    }

    /// <summary>Enumerates business days using the globally registered calendar for the current culture.</summary>
    /// <param name="source">The finite source range.</param>
    /// <returns>The business dates in the range.</returns>
    /// <remarks><example><code>var days = range.BusinessDays();</code></example></remarks>
    /// <exception cref="InvalidOperationException">Thrown when the range has an open boundary.</exception>
    [DebuggerStepThrough]
    public static IEnumerable<DateOnly> BusinessDays(this DateOnlyRange source)
    {
        return source.BusinessDays(BusinessCalendars.Resolve());
    }

    /// <summary>Enumerates business days using the globally registered calendar for a culture.</summary>
    /// <param name="source">The finite source range.</param>
    /// <param name="culture">The culture used to resolve the calendar.</param>
    /// <returns>The business dates in the range.</returns>
    /// <remarks><example><code>var days = range.BusinessDays(CultureInfo.GetCultureInfo("de-DE"));</code></example></remarks>
    /// <exception cref="InvalidOperationException">Thrown when the range has an open boundary.</exception>
    [DebuggerStepThrough]
    public static IEnumerable<DateOnly> BusinessDays(this DateOnlyRange source, CultureInfo culture)
    {
        return source.BusinessDays(BusinessCalendars.Resolve(culture));
    }

    /// <summary>Counts business days in a finite date range.</summary>
    /// <param name="source">The finite source range.</param>
    /// <param name="calendar">The business calendar.</param>
    /// <returns>The business-day count.</returns>
    /// <remarks><example><code>var count = range.BusinessDayCount(calendar);</code></example></remarks>
    /// <exception cref="InvalidOperationException">Thrown when the range has an open boundary.</exception>
    [DebuggerStepThrough]
    public static int BusinessDayCount(this DateOnlyRange source, IBusinessCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        EnsureFinite(source);

        return calendar.CountBusinessDays(source.StartInclusive.Value, source.EndExclusive.Value);
    }

    /// <summary>Counts business days using the globally registered calendar for the current culture.</summary>
    /// <param name="source">The finite source range.</param>
    /// <returns>The business-day count.</returns>
    /// <remarks><example><code>var count = range.BusinessDayCount();</code></example></remarks>
    /// <exception cref="InvalidOperationException">Thrown when the range has an open boundary.</exception>
    [DebuggerStepThrough]
    public static int BusinessDayCount(this DateOnlyRange source)
    {
        return source.BusinessDayCount(BusinessCalendars.Resolve());
    }

    /// <summary>Counts business days using the globally registered calendar for a culture.</summary>
    /// <param name="source">The finite source range.</param>
    /// <param name="culture">The culture used to resolve the calendar.</param>
    /// <returns>The business-day count.</returns>
    /// <remarks><example><code>var count = range.BusinessDayCount(CultureInfo.GetCultureInfo("en-US"));</code></example></remarks>
    /// <exception cref="InvalidOperationException">Thrown when the range has an open boundary.</exception>
    [DebuggerStepThrough]
    public static int BusinessDayCount(this DateOnlyRange source, CultureInfo culture)
    {
        return source.BusinessDayCount(BusinessCalendars.Resolve(culture));
    }

    /// <summary>Converts a date range to a date/time-offset range at start-of-day using a fixed offset.</summary>
    /// <param name="source">The source date range.</param>
    /// <param name="offset">The fixed offset, or zero when omitted.</param>
    /// <returns>The start-of-day date/time-offset range.</returns>
    /// <remarks><example><code>var instants = dateRange.AtStartAndEndOfDay(TimeSpan.Zero);</code></example></remarks>
    [DebuggerStepThrough]
    public static DateTimeOffsetRange AtStartAndEndOfDay(this DateOnlyRange source, TimeSpan? offset = null)
    {
        return new DateTimeOffsetRange(
            source.StartInclusive.HasValue ? source.StartInclusive.Value.AtStartOfDay(offset) : null,
            source.EndExclusive.HasValue ? source.EndExclusive.Value.AtStartOfDay(offset) : null);
    }

    /// <summary>Converts a date range to a date/time-offset range at local start-of-day using time-zone rules.</summary>
    /// <param name="source">The source date range.</param>
    /// <param name="timeZone">The time zone.</param>
    /// <param name="invalidTimePolicy">The invalid-time policy.</param>
    /// <param name="ambiguousTimePolicy">The ambiguous-time policy.</param>
    /// <returns>The start-of-day date/time-offset range.</returns>
    /// <remarks><example><code>var instants = dateRange.AtStartAndEndOfDay(timeZone);</code></example></remarks>
    [DebuggerStepThrough]
    public static DateTimeOffsetRange AtStartAndEndOfDay(
        this DateOnlyRange source,
        TimeZoneInfo timeZone,
        InvalidTimePolicy invalidTimePolicy = InvalidTimePolicy.Throw,
        AmbiguousTimePolicy ambiguousTimePolicy = AmbiguousTimePolicy.Throw)
    {
        ArgumentNullException.ThrowIfNull(timeZone);

        return new DateTimeOffsetRange(
            source.StartInclusive.HasValue
                ? source.StartInclusive.Value.ToDateTime(TimeOnly.MinValue).ToDateTimeOffset(timeZone, invalidTimePolicy, ambiguousTimePolicy)
                : null,
            source.EndExclusive.HasValue
                ? source.EndExclusive.Value.ToDateTime(TimeOnly.MinValue).ToDateTimeOffset(timeZone, invalidTimePolicy, ambiguousTimePolicy)
                : null);
    }

    /// <summary>Formats a date range as an ISO interval string.</summary>
    /// <param name="source">The source range.</param>
    /// <returns>The ISO interval string.</returns>
    /// <remarks><example><code>var text = range.ToIsoRangeString();</code></example></remarks>
    [DebuggerStepThrough]
    public static string ToIsoRangeString(this DateOnlyRange source)
        => $"{FormatDateOnlyBoundary(source.StartInclusive)}/{FormatDateOnlyBoundary(source.EndExclusive)}";

    /// <summary>Attempts to parse an ISO date interval string.</summary>
    /// <param name="source">The source interval text.</param>
    /// <param name="result">The parsed range when parsing succeeds.</param>
    /// <returns><c>true</c> when parsing succeeds.</returns>
    /// <remarks><example><code>if ("2026-01-01/2026-02-01".TryParseDateOnlyRange(out var range)) { }</code></example></remarks>
    [DebuggerStepThrough]
    public static bool TryParseDateOnlyRange(this string source, out DateOnlyRange result)
    {
        result = default;
        if (!TrySplitRange(source, out var startText, out var endText))
        {
            return false;
        }

        if (!TryParseDateOnlyBoundary(startText, out var start) || !TryParseDateOnlyBoundary(endText, out var end))
        {
            return false;
        }

        if (!start.HasValue && !end.HasValue)
        {
            return false;
        }

        try
        {
            result = new DateOnlyRange(start, end);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>Determines whether the first range ends before or exactly at the second range start.</summary>
    [DebuggerStepThrough]
    public static bool IsBefore(this TimeOnlyRange source, TimeOnlyRange other)
        => source.EndExclusive.HasValue && other.StartInclusive.HasValue && source.EndExclusive.Value <= other.StartInclusive.Value;

    /// <summary>Determines whether the first range starts after or exactly at the second range end.</summary>
    [DebuggerStepThrough]
    public static bool IsAfter(this TimeOnlyRange source, TimeOnlyRange other) => other.IsBefore(source);

    /// <summary>Determines whether two ranges touch at a boundary without overlapping.</summary>
    [DebuggerStepThrough]
    public static bool Touches(this TimeOnlyRange source, TimeOnlyRange other)
        => (source.EndExclusive.HasValue && other.StartInclusive.HasValue && source.EndExclusive.Value == other.StartInclusive.Value) ||
            (other.EndExclusive.HasValue && source.StartInclusive.HasValue && other.EndExclusive.Value == source.StartInclusive.Value);

    /// <summary>Determines whether two ranges are adjacent.</summary>
    [DebuggerStepThrough]
    public static bool IsAdjacentTo(this TimeOnlyRange source, TimeOnlyRange other) => source.Touches(other);

    /// <summary>Determines whether the source range starts before another range.</summary>
    [DebuggerStepThrough]
    public static bool StartsBefore(this TimeOnlyRange source, TimeOnlyRange other) => CompareStarts(source.StartInclusive, other.StartInclusive) < 0;

    /// <summary>Determines whether the source range ends after another range.</summary>
    [DebuggerStepThrough]
    public static bool EndsAfter(this TimeOnlyRange source, TimeOnlyRange other) => CompareEnds(source.EndExclusive, other.EndExclusive) > 0;

    /// <summary>Determines whether the source range fully contains another range.</summary>
    [DebuggerStepThrough]
    public static bool Contains(this TimeOnlyRange source, TimeOnlyRange other)
        => CompareStarts(source.StartInclusive, other.StartInclusive) <= 0 && CompareEnds(source.EndExclusive, other.EndExclusive) >= 0;

    /// <summary>Returns the half-open intersection of two ranges.</summary>
    [DebuggerStepThrough]
    public static TimeOnlyRange? Intersection(this TimeOnlyRange source, TimeOnlyRange other)
    {
        var start = MaxStart(source.StartInclusive, other.StartInclusive);
        var end = MinEnd(source.EndExclusive, other.EndExclusive);

        return HasPositiveLength(start, end) ? new TimeOnlyRange(start, end) : null;
    }

    /// <summary>Attempts to return the half-open intersection of two ranges.</summary>
    [DebuggerStepThrough]
    public static bool TryIntersection(this TimeOnlyRange source, TimeOnlyRange other, out TimeOnlyRange result)
    {
        var intersection = source.Intersection(other);
        result = intersection.GetValueOrDefault();
        return intersection.HasValue;
    }

    /// <summary>Returns the union of two overlapping or adjacent ranges.</summary>
    [DebuggerStepThrough]
    public static TimeOnlyRange? Union(this TimeOnlyRange source, TimeOnlyRange other)
    {
        if (!source.Overlaps(other) && !source.Touches(other))
        {
            return null;
        }

        var start = MinStart(source.StartInclusive, other.StartInclusive);
        var end = MaxEnd(source.EndExclusive, other.EndExclusive);
        return !start.HasValue && !end.HasValue ? null : new TimeOnlyRange(start, end);
    }

    /// <summary>Attempts to merge two overlapping or adjacent ranges.</summary>
    [DebuggerStepThrough]
    public static bool TryMerge(this TimeOnlyRange source, TimeOnlyRange other, out TimeOnlyRange result)
    {
        var union = source.Union(other);
        result = union.GetValueOrDefault();
        return union.HasValue;
    }

    /// <summary>Returns the finite gap between two disjoint ranges.</summary>
    [DebuggerStepThrough]
    public static TimeOnlyRange? Gap(this TimeOnlyRange source, TimeOnlyRange other)
    {
        if (source.Overlaps(other) || source.Touches(other))
        {
            return null;
        }

        var ordered = source.CompareTo(other) <= 0 ? (First: source, Second: other) : (First: other, Second: source);
        return ordered.First.EndExclusive.HasValue && ordered.Second.StartInclusive.HasValue &&
            ordered.First.EndExclusive.Value < ordered.Second.StartInclusive.Value
            ? new TimeOnlyRange(ordered.First.EndExclusive.Value, ordered.Second.StartInclusive.Value)
            : null;
    }

    /// <summary>Sorts and merges overlapping or adjacent ranges.</summary>
    [DebuggerStepThrough]
    public static IReadOnlyList<TimeOnlyRange> Normalize(this IEnumerable<TimeOnlyRange> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var ordered = source.Order().ToArray();
        if (ordered.Length == 0)
        {
            return [];
        }

        var result = new List<TimeOnlyRange>();
        var current = ordered[0];
        foreach (var range in ordered.Skip(1))
        {
            if (current.TryMerge(range, out var merged))
            {
                current = merged;
                continue;
            }

            result.Add(current);
            current = range;
        }

        result.Add(current);
        return result;
    }

    /// <summary>Formats a time range as an ISO interval string.</summary>
    /// <param name="source">The source range.</param>
    /// <returns>The ISO interval string.</returns>
    /// <remarks><example><code>var text = range.ToIsoRangeString();</code></example></remarks>
    [DebuggerStepThrough]
    public static string ToIsoRangeString(this TimeOnlyRange source)
        => $"{FormatTimeOnlyBoundary(source.StartInclusive)}/{FormatTimeOnlyBoundary(source.EndExclusive)}";

    /// <summary>Attempts to parse an ISO time interval string.</summary>
    /// <param name="source">The source interval text.</param>
    /// <param name="result">The parsed range when parsing succeeds.</param>
    /// <returns><c>true</c> when parsing succeeds.</returns>
    /// <remarks><example><code>if ("09:00:00/17:00:00".TryParseTimeOnlyRange(out var range)) { }</code></example></remarks>
    [DebuggerStepThrough]
    public static bool TryParseTimeOnlyRange(this string source, out TimeOnlyRange result)
    {
        result = default;
        if (!TrySplitRange(source, out var startText, out var endText))
        {
            return false;
        }

        if (!TryParseTimeOnlyBoundary(startText, out var start) || !TryParseTimeOnlyBoundary(endText, out var end))
        {
            return false;
        }

        if (!start.HasValue && !end.HasValue)
        {
            return false;
        }

        try
        {
            result = new TimeOnlyRange(start, end);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool HasPositiveLength(DateTime? start, DateTime? end) => !start.HasValue || !end.HasValue || start.Value < end.Value;

    private static bool HasPositiveLength(DateTimeOffset? start, DateTimeOffset? end) => !start.HasValue || !end.HasValue || start.Value < end.Value;

    private static bool HasPositiveLength(DateOnly? start, DateOnly? end) => !start.HasValue || !end.HasValue || start.Value < end.Value;

    private static bool HasPositiveLength(TimeOnly? start, TimeOnly? end) => !start.HasValue || !end.HasValue || start.Value < end.Value;

    private static void EnsureFinite(DateTimeRange source)
    {
        if (!source.StartInclusive.HasValue || !source.EndExclusive.HasValue)
        {
            throw new InvalidOperationException("This operation requires a finite DateTimeRange.");
        }
    }

    private static void EnsureFinite(DateOnlyRange source)
    {
        if (!source.StartInclusive.HasValue || !source.EndExclusive.HasValue)
        {
            throw new InvalidOperationException("This operation requires a finite DateOnlyRange.");
        }
    }

    private static int CompareStarts(DateTime? left, DateTime? right) => CompareNullableStart(left, right);

    private static int CompareStarts(DateTimeOffset? left, DateTimeOffset? right) => CompareNullableStart(left, right);

    private static int CompareStarts(DateOnly? left, DateOnly? right) => CompareNullableStart(left, right);

    private static int CompareStarts(TimeOnly? left, TimeOnly? right) => CompareNullableStart(left, right);

    private static int CompareEnds(DateTime? left, DateTime? right) => CompareNullableEnd(left, right);

    private static int CompareEnds(DateTimeOffset? left, DateTimeOffset? right) => CompareNullableEnd(left, right);

    private static int CompareEnds(DateOnly? left, DateOnly? right) => CompareNullableEnd(left, right);

    private static int CompareEnds(TimeOnly? left, TimeOnly? right) => CompareNullableEnd(left, right);

    private static T? MaxStart<T>(T? left, T? right)
        where T : struct, IComparable<T>
        => CompareNullableStart(left, right) >= 0 ? left : right;

    private static T? MinStart<T>(T? left, T? right)
        where T : struct, IComparable<T>
        => CompareNullableStart(left, right) <= 0 ? left : right;

    private static T? MinEnd<T>(T? left, T? right)
        where T : struct, IComparable<T>
        => CompareNullableEnd(left, right) <= 0 ? left : right;

    private static T? MaxEnd<T>(T? left, T? right)
        where T : struct, IComparable<T>
        => CompareNullableEnd(left, right) >= 0 ? left : right;

    private static int CompareNullableStart<T>(T? left, T? right)
        where T : struct, IComparable<T>
    {
        return (left.HasValue, right.HasValue) switch
        {
            (false, false) => 0,
            (false, true) => -1,
            (true, false) => 1,
            _ => left.Value.CompareTo(right.Value)
        };
    }

    private static int CompareNullableEnd<T>(T? left, T? right)
        where T : struct, IComparable<T>
    {
        return (left.HasValue, right.HasValue) switch
        {
            (false, false) => 0,
            (false, true) => 1,
            (true, false) => -1,
            _ => left.Value.CompareTo(right.Value)
        };
    }

    private static bool TrySplitRange(string source, out string start, out string end)
    {
        start = string.Empty;
        end = string.Empty;
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        var separator = source.IndexOf('/');
        if (separator < 0 || separator != source.LastIndexOf('/'))
        {
            return false;
        }

        start = source[..separator].Trim();
        end = source[(separator + 1)..].Trim();
        return true;
    }

    private static string FormatDateTimeBoundary(DateTime? value)
        => value.HasValue ? value.Value.ToString("O", CultureInfo.InvariantCulture) : string.Empty;

    private static string FormatDateTimeOffsetBoundary(DateTimeOffset? value)
        => value.HasValue ? value.Value.ToString("O", CultureInfo.InvariantCulture) : string.Empty;

    private static string FormatDateOnlyBoundary(DateOnly? value)
        => value.HasValue ? value.Value.ToIsoDateString() : string.Empty;

    private static string FormatTimeOnlyBoundary(TimeOnly? value)
        => value.HasValue ? value.Value.ToIsoTimeString() : string.Empty;

    private static bool TryParseDateTimeBoundary(string source, out DateTime? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(source))
        {
            return true;
        }

        if (DateTime.TryParse(
                source,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsed))
        {
            result = parsed;
            return true;
        }

        return false;
    }

    private static bool TryParseDateTimeOffsetBoundary(string source, out DateTimeOffset? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(source))
        {
            return true;
        }

        if (DateTimeOffset.TryParse(
                source,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsed))
        {
            result = parsed;
            return true;
        }

        return false;
    }

    private static bool TryParseDateOnlyBoundary(string source, out DateOnly? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(source))
        {
            return true;
        }

        if (DateOnly.TryParseExact(source, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            result = parsed;
            return true;
        }

        return false;
    }

    private static bool TryParseTimeOnlyBoundary(string source, out TimeOnly? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(source))
        {
            return true;
        }

        if (TimeOnly.TryParseExact(source, ["HH:mm:ss", "HH:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            result = parsed;
            return true;
        }

        return false;
    }
}
