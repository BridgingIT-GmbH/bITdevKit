// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a half-open <see cref="TimeOnly"/> range using [start, end) semantics, with one optional open boundary.
/// </summary>
/// <remarks><example><code>var range = new TimeOnlyRange(start, end);</code></example></remarks>
public readonly record struct TimeOnlyRange : IComparable<TimeOnlyRange>, IComparable
{
    /// <summary>
    /// Initializes a new half-open <see cref="TimeOnly"/> range with one optional open boundary.
    /// </summary>
    /// <param name="startInclusive">The inclusive range start, or <c>null</c> for an open start.</param>
    /// <param name="endExclusive">The exclusive range end, or <c>null</c> for an open end.</param>
    /// <exception cref="ArgumentException">Thrown when both boundaries are open, or when the end is before the start.</exception>
    public TimeOnlyRange(TimeOnly? startInclusive, TimeOnly? endExclusive)
    {
        if (startInclusive is null && endExclusive is null)
        {
            throw new ArgumentException("A TimeOnlyRange must have a start or an end boundary.", nameof(startInclusive));
        }

        if (startInclusive.HasValue && endExclusive.HasValue && endExclusive.Value < startInclusive.Value)
        {
            throw new ArgumentException("Range end must be greater than or equal to range start.", nameof(endExclusive));
        }

        this.StartInclusive = startInclusive;
        this.EndExclusive = endExclusive;
    }

    /// <summary>Gets the inclusive range start, or <c>null</c> when the start is open.</summary>
    public TimeOnly? StartInclusive { get; }

    /// <summary>Gets the exclusive range end, or <c>null</c> when the end is open.</summary>
    public TimeOnly? EndExclusive { get; }

    /// <summary>Gets a value indicating whether the start boundary is open.</summary>
    public bool IsOpenStart => !this.StartInclusive.HasValue;

    /// <summary>Gets a value indicating whether the end boundary is open.</summary>
    public bool IsOpenEnd => !this.EndExclusive.HasValue;

    /// <summary>Gets the range duration.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the range has an open boundary.</exception>
    public TimeSpan Duration => this.StartInclusive.HasValue && this.EndExclusive.HasValue
        ? this.EndExclusive.Value - this.StartInclusive.Value
        : throw new InvalidOperationException("Open-ended TimeOnlyRange values do not have a finite duration.");

    /// <summary>Determines whether a value is inside the half-open range.</summary>
    /// <param name="value">The value to test.</param>
    /// <returns><c>true</c> when value is greater than or equal to start, when present, and less than end, when present.</returns>
    /// <remarks><example><code>var contains = range.Contains(value);</code></example></remarks>
    public bool Contains(TimeOnly value)
    {
        this.EnsureUsable();

        return (!this.StartInclusive.HasValue || value >= this.StartInclusive.Value) &&
            (!this.EndExclusive.HasValue || value < this.EndExclusive.Value);
    }

    /// <summary>Determines whether this range overlaps another half-open range.</summary>
    /// <param name="other">The range to compare.</param>
    /// <returns><c>true</c> when the ranges overlap.</returns>
    /// <remarks><example><code>var overlaps = range.Overlaps(other);</code></example></remarks>
    public bool Overlaps(TimeOnlyRange other)
    {
        this.EnsureUsable();
        other.EnsureUsable();

        return IsStartBeforeEnd(this.StartInclusive, other.EndExclusive) &&
            IsStartBeforeEnd(other.StartInclusive, this.EndExclusive);
    }

    /// <summary>Compares this range to another range for sorting.</summary>
    /// <param name="other">The range to compare.</param>
    /// <returns>A negative value when this range sorts before the other range, zero when equal, or a positive value when after.</returns>
    /// <remarks><example><code>ranges.Sort();</code></example></remarks>
    public int CompareTo(TimeOnlyRange other)
    {
        this.EnsureUsable();
        other.EnsureUsable();

        var startComparison = CompareStarts(this.StartInclusive, other.StartInclusive);
        return startComparison != 0 ? startComparison : CompareEnds(this.EndExclusive, other.EndExclusive);
    }

    /// <summary>Compares this range to another object for sorting.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns>A negative value when this range sorts before the other range, zero when equal, or a positive value when after.</returns>
    /// <remarks><example><code>Array.Sort(items);</code></example></remarks>
    /// <exception cref="ArgumentException">Thrown when <paramref name="obj"/> is not a <see cref="TimeOnlyRange"/>.</exception>
    public int CompareTo(object obj)
    {
        return obj is null
            ? 1
            : obj is TimeOnlyRange other
                ? this.CompareTo(other)
                : throw new ArgumentException("Object must be a TimeOnlyRange.", nameof(obj));
    }

    /// <summary>Compares whether one range sorts before another range.</summary>
    public static bool operator <(TimeOnlyRange left, TimeOnlyRange right) => left.CompareTo(right) < 0;

    /// <summary>Compares whether one range sorts after another range.</summary>
    public static bool operator >(TimeOnlyRange left, TimeOnlyRange right) => left.CompareTo(right) > 0;

    /// <summary>Compares whether one range sorts before or equal to another range.</summary>
    public static bool operator <=(TimeOnlyRange left, TimeOnlyRange right) => left.CompareTo(right) <= 0;

    /// <summary>Compares whether one range sorts after or equal to another range.</summary>
    public static bool operator >=(TimeOnlyRange left, TimeOnlyRange right) => left.CompareTo(right) >= 0;

    private static bool IsStartBeforeEnd(TimeOnly? start, TimeOnly? end)
    {
        return !start.HasValue || !end.HasValue || start.Value < end.Value;
    }

    private static int CompareStarts(TimeOnly? left, TimeOnly? right)
    {
        return (left.HasValue, right.HasValue) switch
        {
            (false, false) => 0,
            (false, true) => -1,
            (true, false) => 1,
            _ => left.Value.CompareTo(right.Value)
        };
    }

    private static int CompareEnds(TimeOnly? left, TimeOnly? right)
    {
        return (left.HasValue, right.HasValue) switch
        {
            (false, false) => 0,
            (false, true) => 1,
            (true, false) => -1,
            _ => left.Value.CompareTo(right.Value)
        };
    }

    private void EnsureUsable()
    {
        if (!this.StartInclusive.HasValue && !this.EndExclusive.HasValue)
        {
            throw new InvalidOperationException("A TimeOnlyRange must have a start or an end boundary.");
        }
    }
}
