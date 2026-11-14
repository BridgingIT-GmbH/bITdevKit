// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Retains a fixed number of observations and returns bounded, chronological time windows.</summary>
/// <typeparam name="T">The observation type.</typeparam>
/// <remarks>
/// The oldest appended observation is overwritten when capacity is reached. This type is not thread-safe;
/// callers synchronize reads and writes and keep retained observations immutable. Timestamps need not be appended in order.
/// </remarks>
/// <example><code>
/// var history = new BoundedTimeSeries&lt;Measurement&gt;(120, point =&gt; point.At);
/// history.Add(new Measurement(DateTimeOffset.UtcNow, 42));
/// var window = history.Query(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow, 30);
/// </code></example>
public sealed class BoundedTimeSeries<T>
{
    private readonly T[] points;
    private readonly Func<T, DateTimeOffset> timestamp;
    private int position;

    /// <summary>Creates a ring with a finite observation capacity.</summary>
    /// <param name="capacity">Maximum retained observations; zero disables retention.</param>
    /// <param name="timestamp">Selects an observation's actual timestamp.</param>
    /// <exception cref="ArgumentOutOfRangeException">The capacity is negative.</exception>
    /// <exception cref="ArgumentNullException">The timestamp selector is null.</exception>
    public BoundedTimeSeries(int capacity, Func<T, DateTimeOffset> timestamp)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);
        ArgumentNullException.ThrowIfNull(timestamp);
        this.points = new T[capacity];
        this.timestamp = timestamp;
    }

    /// <summary>Gets the maximum number of retained observations.</summary>
    public int Capacity => this.points.Length;

    /// <summary>Gets the number of observations currently retained.</summary>
    public int Count { get; private set; }

    /// <summary>Appends an observation, overwriting the oldest append when full.</summary>
    /// <param name="point">The observation to retain. A zero-capacity ring ignores it.</param>
    public void Add(T point)
    {
        if (this.Capacity == 0) { return; }

        this.points[this.position] = point;
        this.position = (this.position + 1) % this.Capacity;
        this.Count = Math.Min(this.Count + 1, this.Capacity);
    }

    /// <summary>Selects an inclusive time window and downsamples it into at most the requested number of points.</summary>
    /// <param name="from">The earliest included timestamp.</param>
    /// <param name="to">The latest included timestamp.</param>
    /// <param name="maximumPoints">A positive upper bound on returned observations.</param>
    /// <param name="aggregate">Combines each chronological bucket; null keeps its last actual observation.</param>
    /// <returns>An independent array ordered by actual timestamp before aggregation.</returns>
    /// <remarks>
    /// Downsampling partitions the selected observations into equally sized buckets by index.
    /// No observations or timestamps are interpolated. A supplied aggregator owns value, timestamp and missing-data semantics.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The point limit is not positive.</exception>
    /// <exception cref="ArgumentException">The end precedes the start.</exception>
    /// <example><code>
    /// var points = history.Query(start, end, 30, bucket =&gt; bucket[^1]);
    /// </code></example>
    public IReadOnlyList<T> Query(DateTimeOffset from, DateTimeOffset to, int maximumPoints, Func<IReadOnlyList<T>, T> aggregate = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumPoints);
        if (to < from) { throw new ArgumentException("The end must not precede the start.", nameof(to)); }

        var selected = this.points.Take(this.Count).Where(point => this.timestamp(point) >= from && this.timestamp(point) <= to)
            .OrderBy(this.timestamp).ToArray();
        if (selected.Length <= maximumPoints) { return selected; }

        return selected.Select((point, index) => (Point: point, Bucket: (int)((long)index * maximumPoints / selected.Length)))
            .GroupBy(item => item.Bucket).Select(group => aggregate is null ? group.Last().Point : aggregate(group.Select(item => item.Point).ToArray())).ToArray();
    }
}