// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Describes exact retained operation counts for a bounded event timeline, grouped by start time and kind.</summary>
/// <example><code>var count = timeline.TotalCount;</code></example>
public sealed record OperationProfilingTimeline
{
    /// <summary>Gets the maximum number of distinct kind lanes in one timeline response.</summary>
    /// <example><code>var maximum = OperationProfilingTimeline.MaximumKinds;</code></example>
    public const int MaximumKinds = 64;
    /// <summary>Gets the inclusive UTC start.</summary>
    /// <example><code>var value = timeline.FromUtc;</code></example>
    public DateTimeOffset FromUtc { get; init; }
    /// <summary>Gets the exclusive UTC end.</summary>
    /// <example><code>var value = timeline.ToUtc;</code></example>
    public DateTimeOffset ToUtc { get; init; }
    /// <summary>Gets the fixed time bucket width.</summary>
    /// <example><code>var value = timeline.BucketWidth;</code></example>
    public TimeSpan BucketWidth { get; init; }
    /// <summary>Gets the exact number of matching retained operations.</summary>
    /// <example><code>var value = timeline.TotalCount;</code></example>
    public long TotalCount { get; init; }
    /// <summary>Gets the bounded nonempty kind and time buckets.</summary>
    /// <example><code>var value = timeline.Buckets;</code></example>
    public IReadOnlyList<OperationProfilingTimelineBucket> Buckets { get; init; } = [];
    /// <summary>Gets the contributing node choices, ignoring the selected node filter.</summary>
    /// <example><code>var value = timeline.Nodes;</code></example>
    public IReadOnlyList<ProfilingNode> Nodes { get; init; } = [];
    /// <summary>Gets whether additional node choices were omitted.</summary>
    /// <example><code>var value = timeline.NodesTruncated;</code></example>
    public bool NodesTruncated { get; init; }

    /// <summary>Calculates a positive width for 12 to 256 buckets over a valid UTC window. Buckets are anchored to the UTC tick epoch.</summary>
    /// <example><code>var width = OperationProfilingTimeline.Width(fromUtc, toUtc, 72);</code></example>
    public static long Width(DateTimeOffset fromUtc, DateTimeOffset toUtc, int bucketCount)
    {
        if (bucketCount is < 12 or > 256 || fromUtc >= toUtc) { throw new ArgumentException("Timeline requires a valid window and 12 to 256 buckets."); }

        return Math.Max(1, (toUtc.UtcTicks - fromUtc.UtcTicks + bucketCount - 1) / bucketCount);
    }
}

/// <summary>Describes every matching operation started within one kind/time bucket. Empty buckets are omitted.</summary>
/// <example><code>var count = bucket.Count;</code></example>
public sealed record OperationProfilingTimelineBucket
{
    /// <summary>Gets the recorded operation kind.</summary>
    /// <example><code>var value = bucket.Kind;</code></example>
    public string Kind { get; init; }
    /// <summary>Gets the inclusive start, clipped to the selection.</summary>
    /// <example><code>var value = bucket.FromUtc;</code></example>
    public DateTimeOffset FromUtc { get; init; }
    /// <summary>Gets the exclusive end, clipped to the selection.</summary>
    /// <example><code>var value = bucket.ToUtc;</code></example>
    public DateTimeOffset ToUtc { get; init; }
    /// <summary>Gets the exact retained operation count.</summary>
    /// <example><code>var value = bucket.Count;</code></example>
    public long Count { get; init; }
    /// <summary>Gets the number of failed operations.</summary>
    /// <example><code>var value = bucket.FailedCount;</code></example>
    public long FailedCount { get; init; }
    /// <summary>Gets the number of canceled operations.</summary>
    /// <example><code>var value = bucket.CanceledCount;</code></example>
    public long CanceledCount { get; init; }
    /// <summary>Gets the number of aborted operations.</summary>
    /// <example><code>var value = bucket.AbortedCount;</code></example>
    public long AbortedCount { get; init; }
    /// <summary>Gets the number of incomplete operations.</summary>
    /// <example><code>var value = bucket.IncompleteCount;</code></example>
    public long IncompleteCount { get; init; }
}
