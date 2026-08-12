// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Tracks the bounded previous-GC state required to derive pause evidence between snapshots.
/// </summary>
/// <example><code>var state = new ProfilingGcObservationState();</code></example>
public sealed class ProfilingGcObservationState
{
    private long? latestObservedIndex;
    private TimeSpan cumulativePauseDuration;
    private TimeSpan? initialTotalPauseDuration;
    private TimeSpan? previousTotalPauseDuration;

    /// <summary>Observes the latest GC evidence and derives cumulative pause information.</summary>
    /// <param name="latest">The latest observed collection, when available.</param>
    /// <param name="latestGen2">The latest observed generation 2 collection, when available.</param>
    /// <param name="totalPauseDuration">The runtime total pause duration, when available.</param>
    /// <param name="elapsedSincePreviousCapture">The monotonic interval since the previous capture.</param>
    /// <returns>The current direct and derived GC evidence.</returns>
    /// <example><code>var result = state.Observe(latest, latestGen2, totalPause, elapsed);</code></example>
    public ProfilingGcObservationResult Observe(
        ProfilingGcObservation latest,
        ProfilingGcObservation latestGen2,
        TimeSpan? totalPauseDuration,
        TimeSpan? elapsedSincePreviousCapture
    )
    {
        var pauseIncrement = this.ObserveTotalPauseDuration(totalPauseDuration);
        if (latest?.Index is { } index)
        {
            if (
                totalPauseDuration is null
                && this.latestObservedIndex is { } previousIndex
                && index > previousIndex
                && latest.PauseDuration is { } pause
            )
            {
                pauseIncrement = pause;
                this.cumulativePauseDuration += pause;
            }

            this.latestObservedIndex = Math.Max(this.latestObservedIndex ?? index, index);
        }

        var pausePercent =
            (this.latestObservedIndex.HasValue || this.initialTotalPauseDuration.HasValue)
            && elapsedSincePreviousCapture is { } elapsed
            && elapsed > TimeSpan.Zero
                ? (double?)
                    Math.Clamp(pauseIncrement.TotalSeconds / elapsed.TotalSeconds * 100d, 0d, 100d)
                : null;

        return new ProfilingGcObservationResult(
            latest,
            latestGen2,
            this.latestObservedIndex.HasValue || this.initialTotalPauseDuration.HasValue
                ? this.cumulativePauseDuration
                : null,
            pausePercent
        );
    }

    private TimeSpan ObserveTotalPauseDuration(TimeSpan? totalPauseDuration)
    {
        if (totalPauseDuration is null || totalPauseDuration < TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        if (this.initialTotalPauseDuration is null)
        {
            this.initialTotalPauseDuration = totalPauseDuration;
            this.previousTotalPauseDuration = totalPauseDuration;
            return TimeSpan.Zero;
        }

        if (
            this.previousTotalPauseDuration is null
            || totalPauseDuration < this.previousTotalPauseDuration
        )
        {
            this.previousTotalPauseDuration = totalPauseDuration;
            return TimeSpan.Zero;
        }

        var increment = totalPauseDuration.Value - this.previousTotalPauseDuration.Value;
        this.previousTotalPauseDuration = totalPauseDuration;
        this.cumulativePauseDuration =
            totalPauseDuration.Value - this.initialTotalPauseDuration.Value;
        return increment;
    }

    /// <summary>Clears all previously observed GC state.</summary>
    /// <example><code>state.Reset();</code></example>
    public void Reset()
    {
        this.latestObservedIndex = null;
        this.cumulativePauseDuration = TimeSpan.Zero;
        this.initialTotalPauseDuration = null;
        this.previousTotalPauseDuration = null;
    }
}

/// <summary>Contains direct evidence for one garbage collection.</summary>
/// <param name="Index">The runtime collection index.</param>
/// <param name="Generation">The collected generation.</param>
/// <param name="ManagedHeapBytes">The managed heap size after collection.</param>
/// <param name="LargeObjectHeapBytes">The large object heap size after collection.</param>
/// <param name="Compacting">Whether the collection compacted memory.</param>
/// <param name="Concurrent">Whether the collection ran concurrently.</param>
/// <param name="PauseDuration">The pause duration reported for the collection.</param>
/// <example><code>var observation = new ProfilingGcObservation(1, 2, heap, loh, true, false, pause);</code></example>
public sealed record ProfilingGcObservation(
    long? Index,
    int? Generation,
    long? ManagedHeapBytes,
    long? LargeObjectHeapBytes,
    bool? Compacting,
    bool? Concurrent,
    TimeSpan? PauseDuration
);

/// <summary>Contains direct GC observations and derived pause evidence.</summary>
/// <param name="Latest">The latest observed collection.</param>
/// <param name="LatestGen2">The latest observed generation 2 collection.</param>
/// <param name="CumulativePauseDuration">The cumulative pause duration since observation began.</param>
/// <param name="PausePercent">The pause burden over the latest monotonic interval.</param>
/// <example><code>var pausePercent = result.PausePercent;</code></example>
public sealed record ProfilingGcObservationResult(
    ProfilingGcObservation Latest,
    ProfilingGcObservation LatestGen2,
    TimeSpan? CumulativePauseDuration,
    double? PausePercent
);
