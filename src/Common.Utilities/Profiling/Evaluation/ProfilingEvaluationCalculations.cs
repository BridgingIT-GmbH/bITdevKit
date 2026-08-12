// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Provides the calculation portion of <see cref="ProfilingEvaluator"/>.
/// </summary>
/// <example><code>var evaluator = new ProfilingEvaluator(options, store);</code></example>
public sealed partial class ProfilingEvaluator
{
    private static EvaluationFacts CalculateFacts(
        ProfilingEvaluationMode mode,
        ProfilingSession session,
        ProfilingNodeParticipation participation,
        ProfilingRuntimeContext runtimeContext,
        IReadOnlyList<ProfilingSnapshot> snapshots
    )
    {
        var ordered = snapshots
            .OrderBy(x => x.Sequence)
            .ThenBy(x => x.CaptureStartedElapsed)
            .ThenBy(x => x.Identity.Key, StringComparer.Ordinal)
            .ToArray();
        var intervals = BuildIntervals(ordered, out var invalidIntervals, out var missingSamples);
        var elapsed =
            ordered.Length < 2
                ? TimeSpan.Zero
                : ordered[^1].CaptureStartedElapsed - ordered[0].CaptureStartedElapsed;
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
            invalidIntervals = true;
        }

        var cpuAverage = WeightedAverage(intervals, x => x.CpuPercent);
        var allocationAverage = WeightedAverage(intervals, x => x.AllocationRateBytesPerSecond);
        var temporalMidpoint =
            ordered.Length == 0
                ? 0
                : ordered[0].CaptureStartedElapsed.TotalSeconds + (elapsed.TotalSeconds / 2);
        var cpuFirstHalf = WeightedAverage(
            intervals,
            x => x.CpuPercent,
            double.NegativeInfinity,
            temporalMidpoint
        );
        var cpuSecondHalf = WeightedAverage(
            intervals,
            x => x.CpuPercent,
            temporalMidpoint,
            double.PositiveInfinity
        );
        var allocationFirstHalf = WeightedAverage(
            intervals,
            x => x.AllocationRateBytesPerSecond,
            double.NegativeInfinity,
            temporalMidpoint
        );
        var allocationSecondHalf = WeightedAverage(
            intervals,
            x => x.AllocationRateBytesPerSecond,
            temporalMidpoint,
            double.PositiveInfinity
        );

        if (mode is ProfilingEvaluationMode.TwoSnapshots && ordered.Length == 2)
        {
            cpuFirstHalf = ordered[0].CpuUsagePercent;
            cpuSecondHalf = ordered[1].CpuUsagePercent;
            allocationFirstHalf = ordered[0].AllocationRateBytesPerSecond;
            allocationSecondHalf = ordered[1].AllocationRateBytesPerSecond;
        }

        var first = ordered.FirstOrDefault();
        var last = ordered.LastOrDefault();
        var captureDurationP95 = Percentile95(ordered.Select(x => x.CaptureDuration));
        var samplingDelayP95 = Percentile95(
            ordered.Select(x => Max(TimeSpan.Zero, x.CaptureStartedElapsed - x.ScheduledElapsed))
        );
        double? captureOverhead =
            session.SamplingInterval > TimeSpan.Zero && captureDurationP95 is not null
                ? 100
                    * captureDurationP95.Value.TotalSeconds
                    / session.SamplingInterval.TotalSeconds
                : null;

        var (successfulCaptures, skippedCaptures, failedCaptures) = CalculateCaptureTotals(
            mode,
            participation,
            ordered
        );
        var captureAttempts = successfulCaptures + skippedCaptures + failedCaptures;
        double? samplingCoverage =
            captureAttempts > 0 ? 100d * successfulCaptures / captureAttempts : null;

        var hasCpu =
            intervals.Any(x => x.CpuPercent is not null)
            || ordered.Any(x => x.CpuUsagePercent is not null);
        var hasManagedHeap =
            first?.ManagedHeapSizeBytes is not null && last?.ManagedHeapSizeBytes is not null;
        var hasPrivateMemory =
            first?.PrivateMemoryBytes is not null && last?.PrivateMemoryBytes is not null;
        var hasLoh =
            first?.LargeObjectHeapBytes is not null && last?.LargeObjectHeapBytes is not null;
        var hasAllocation =
            intervals.Any(x => x.AllocationRateBytesPerSecond is not null)
            || ordered.Any(x => x.AllocationRateBytesPerSecond is not null);
        var hasGc = intervals.Any(x =>
            x.Gen0Delta is not null
            || x.Gen1Delta is not null
            || x.Gen2Delta is not null
            || x.GcPauseBurdenPercent is not null
        );

        var availableInputs = new List<string>();
        var missingInputs = new List<string>();
        AddInput("cpu", hasCpu, availableInputs, missingInputs);
        AddInput("managed-heap", hasManagedHeap, availableInputs, missingInputs);
        AddInput("private-memory", hasPrivateMemory, availableInputs, missingInputs);
        AddInput("loh", hasLoh, availableInputs, missingInputs);
        AddInput("allocation", hasAllocation, availableInputs, missingInputs);
        AddInput("gc", hasGc, availableInputs, missingInputs);
        AddInput("sampling", ordered.Length > 0, availableInputs, missingInputs);

        var enoughTimeline =
            mode is ProfilingEvaluationMode.TwoSnapshots
            || (ordered.Length >= 5 && elapsed >= TimeSpan.FromSeconds(5));
        var qualityLimited =
            failedCaptures > 0
            || samplingCoverage is < 90
            || captureOverhead is >= 25
            || (
                samplingDelayP95 is not null
                && session.SamplingInterval > TimeSpan.Zero
                && samplingDelayP95.Value >= TimeSpan.FromTicks(session.SamplingInterval.Ticks / 2)
            )
            || invalidIntervals
            || missingSamples
            || runtimeContext?.DebuggerAttached is true;
        var limitations = BuildLimitations(
            mode,
            session,
            runtimeContext,
            ordered.Length,
            elapsed,
            failedCaptures,
            samplingCoverage,
            captureOverhead,
            samplingDelayP95,
            invalidIntervals,
            missingSamples,
            missingInputs.Count > 0
        );
        var sufficiency =
            !enoughTimeline ? ProfilingDataSufficiency.Collecting
            : qualityLimited || missingInputs.Count > 0 ? ProfilingDataSufficiency.Limited
            : ProfilingDataSufficiency.Sufficient;

        var gen0Delta = Sum(intervals.Select(x => x.Gen0Delta));
        var gen1Delta = Sum(intervals.Select(x => x.Gen1Delta));
        var gen2Delta = Sum(intervals.Select(x => x.Gen2Delta));
        var gcPauseBurden = WeightedAverage(intervals, x => x.GcPauseBurdenPercent);
        var managedHeapStart = first?.ManagedHeapSizeBytes;
        var managedHeapEnd = last?.ManagedHeapSizeBytes;
        var privateMemoryStart = first?.PrivateMemoryBytes;
        var privateMemoryEnd = last?.PrivateMemoryBytes;
        var lohStart = first?.LargeObjectHeapBytes;
        var lohEnd = last?.LargeObjectHeapBytes;
        var cpuPeak = MaxValue(ordered.Select(x => x.CpuUsagePercent));
        var allocationPeak = MaxValue(ordered.Select(x => x.AllocationRateBytesPerSecond));
        var kpis = BuildKpis(
            cpuAverage,
            cpuPeak,
            last?.CpuUsagePercent
                ?? intervals.LastOrDefault(x => x.CpuPercent is not null)?.CpuPercent,
            Difference(cpuSecondHalf, cpuFirstHalf),
            managedHeapStart,
            managedHeapEnd,
            MaxValue(ordered.Select(x => ToDouble(x.ManagedHeapSizeBytes))),
            privateMemoryStart,
            privateMemoryEnd,
            MaxValue(ordered.Select(x => ToDouble(x.PrivateMemoryBytes))),
            lohStart,
            lohEnd,
            MaxValue(ordered.Select(x => ToDouble(x.LargeObjectHeapBytes))),
            last?.HeapFragmentationPercent,
            Difference(last?.HeapFragmentationPercent, first?.HeapFragmentationPercent),
            last?.LargeObjectHeapFragmentationPercent,
            Difference(
                last?.LargeObjectHeapFragmentationPercent,
                first?.LargeObjectHeapFragmentationPercent
            ),
            allocationAverage,
            allocationPeak,
            Difference(allocationSecondHalf, allocationFirstHalf),
            gcPauseBurden,
            gen0Delta,
            gen1Delta,
            gen2Delta,
            Rate(gen0Delta, elapsed),
            Rate(gen1Delta, elapsed),
            Rate(gen2Delta, elapsed),
            samplingCoverage,
            skippedCaptures,
            failedCaptures,
            captureDurationP95,
            captureOverhead,
            samplingDelayP95
        );

        return new()
        {
            Mode = mode,
            Snapshots = ordered,
            Elapsed = elapsed,
            CanEmitSignals = enoughTimeline,
            HighConfidenceAllowed = !qualityLimited && runtimeContext?.DebuggerAttached is not true,
            HighConfidenceWindow =
                mode is ProfilingEvaluationMode.NodeSession
                && ordered.Length >= 10
                && elapsed >= TimeSpan.FromSeconds(10),
            HasInvalidIntervals = invalidIntervals,
            DataQuality = new()
            {
                Sufficiency = sufficiency,
                AvailableInputs = availableInputs,
                MissingInputs = missingInputs,
                SamplingCoveragePercent = samplingCoverage,
                SkippedCaptureCount = skippedCaptures,
                FailedCaptureCount = failedCaptures,
                CaptureDurationP95 = captureDurationP95,
                CaptureOverheadP95Percent = captureOverhead,
                SamplingDelayP95 = samplingDelayP95,
            },
            Kpis = kpis,
            Limitations = limitations,
            CpuAverage = cpuAverage,
            CpuFirstHalfAverage = cpuFirstHalf,
            CpuSecondHalfAverage = cpuSecondHalf,
            CpuEnding =
                last?.CpuUsagePercent
                ?? intervals.LastOrDefault(x => x.CpuPercent is not null)?.CpuPercent,
            CpuAtLeast70Ratio = WeightedRatio(intervals, x => x.CpuPercent is >= 70),
            CpuAtLeast80Ratio = WeightedRatio(intervals, x => x.CpuPercent is >= 80),
            ManagedHeapStart = managedHeapStart,
            ManagedHeapEnd = managedHeapEnd,
            PrivateMemoryStart = privateMemoryStart,
            PrivateMemoryEnd = privateMemoryEnd,
            LohStart = lohStart,
            LohEnd = lohEnd,
            LohFragmentationStart = first?.LargeObjectHeapFragmentationPercent,
            LohFragmentationEnd = last?.LargeObjectHeapFragmentationPercent,
            LatestGen2ManagedHeapBytes = ordered
                .LastOrDefault(x => x.LatestGen2ManagedHeapBytes is not null)
                ?.LatestGen2ManagedHeapBytes,
            AllocationAverage = allocationAverage,
            AllocationFirstHalfAverage = allocationFirstHalf,
            AllocationSecondHalfAverage = allocationSecondHalf,
            AllocationStart = first?.AllocationRateBytesPerSecond,
            AllocationEnd = last?.AllocationRateBytesPerSecond,
            Gen0Delta = gen0Delta,
            Gen1Delta = gen1Delta,
            Gen2Delta = gen2Delta,
            Gen0Rate = Rate(gen0Delta, elapsed),
            Gen1Rate = Rate(gen1Delta, elapsed),
            Gen2Rate = Rate(gen2Delta, elapsed),
            GcPauseBurdenPercent = gcPauseBurden,
            HasCpuInput = hasCpu,
            HasManagedHeapInput = hasManagedHeap,
            HasPrivateMemoryInput = hasPrivateMemory,
            HasLohInput = hasLoh,
            HasAllocationInput = hasAllocation,
            HasGcInput = hasGc,
        };
    }

    private static IReadOnlyList<EvaluationInterval> BuildIntervals(
        IReadOnlyList<ProfilingSnapshot> snapshots,
        out bool invalidIntervals,
        out bool missingSamples
    )
    {
        var result = new List<EvaluationInterval>();
        invalidIntervals = false;
        missingSamples = false;
        for (var index = 1; index < snapshots.Count; index++)
        {
            var start = snapshots[index - 1];
            var end = snapshots[index];
            missingSamples |= end.Sequence > start.Sequence + 1;
            var elapsed =
                end.CaptureStartedElapsed.TotalSeconds - start.CaptureStartedElapsed.TotalSeconds;
            if (end.Sequence <= start.Sequence || elapsed <= 0)
            {
                invalidIntervals = true;
                continue;
            }

            var counterReset = false;
            var cpu = CalculateCpu(start, end, elapsed, ref counterReset);
            var allocation = CalculateRate(
                start.TotalAllocatedBytes,
                end.TotalAllocatedBytes,
                elapsed,
                end.AllocationRateBytesPerSecond,
                ref counterReset
            );
            var gen0 = CalculateDelta(
                start.Gen0CollectionCount,
                end.Gen0CollectionCount,
                ref counterReset
            );
            var gen1 = CalculateDelta(
                start.Gen1CollectionCount,
                end.Gen1CollectionCount,
                ref counterReset
            );
            var gen2 = CalculateDelta(
                start.Gen2CollectionCount,
                end.Gen2CollectionCount,
                ref counterReset
            );
            var pause = CalculatePause(start, end, elapsed, ref counterReset);
            invalidIntervals |= counterReset;
            result.Add(
                new(
                    start.CaptureStartedElapsed.TotalSeconds,
                    end.CaptureStartedElapsed.TotalSeconds,
                    elapsed,
                    cpu,
                    allocation,
                    gen0,
                    gen1,
                    gen2,
                    pause,
                    counterReset
                )
            );
        }

        return result;
    }

    private static double? CalculateCpu(
        ProfilingSnapshot start,
        ProfilingSnapshot end,
        double elapsedSeconds,
        ref bool counterReset
    )
    {
        if (
            start.ProcessCpuDuration is not null
            && end.ProcessCpuDuration is not null
            && (end.LogicalProcessorCount ?? start.LogicalProcessorCount) is > 0
        )
        {
            var delta =
                end.ProcessCpuDuration.Value.TotalSeconds
                - start.ProcessCpuDuration.Value.TotalSeconds;
            if (delta < 0)
            {
                counterReset = true;
                return null;
            }

            return Round(
                100
                    * delta
                    / elapsedSeconds
                    / (end.LogicalProcessorCount ?? start.LogicalProcessorCount).Value
            );
        }

        return end.CpuUsagePercent;
    }

    private static double? CalculateRate(
        long? start,
        long? end,
        double elapsedSeconds,
        double? fallback,
        ref bool counterReset
    )
    {
        if (start is null || end is null)
        {
            return fallback;
        }

        var delta = end.Value - start.Value;
        if (delta < 0)
        {
            counterReset = true;
            return null;
        }

        return Round(delta / elapsedSeconds);
    }

    private static long? CalculateDelta(long? start, long? end, ref bool counterReset)
    {
        if (start is null || end is null)
        {
            return null;
        }

        var delta = end.Value - start.Value;
        if (delta < 0)
        {
            counterReset = true;
            return null;
        }

        return delta;
    }

    private static double? CalculatePause(
        ProfilingSnapshot start,
        ProfilingSnapshot end,
        double elapsedSeconds,
        ref bool counterReset
    )
    {
        if (start.CumulativeGcPauseDuration is null || end.CumulativeGcPauseDuration is null)
        {
            return end.GcPausePercent;
        }

        var delta =
            end.CumulativeGcPauseDuration.Value.TotalSeconds
            - start.CumulativeGcPauseDuration.Value.TotalSeconds;
        if (delta < 0)
        {
            counterReset = true;
            return null;
        }

        return Round(100 * delta / elapsedSeconds);
    }

    private static double? WeightedAverage(
        IReadOnlyList<EvaluationInterval> intervals,
        Func<EvaluationInterval, double?> selector,
        double rangeStart = double.NegativeInfinity,
        double rangeEnd = double.PositiveInfinity
    )
    {
        var weighted = 0d;
        var duration = 0d;
        foreach (var interval in intervals)
        {
            var value = selector(interval);
            if (value is null)
            {
                continue;
            }

            var overlapStart = Math.Max(interval.StartSeconds, rangeStart);
            var overlapEnd = Math.Min(interval.EndSeconds, rangeEnd);
            var overlap = overlapEnd - overlapStart;
            if (overlap <= 0)
            {
                continue;
            }

            weighted += value.Value * overlap;
            duration += overlap;
        }

        return duration > 0 ? Round(weighted / duration) : null;
    }

    private static double WeightedRatio(
        IReadOnlyList<EvaluationInterval> intervals,
        Func<EvaluationInterval, bool> predicate
    )
    {
        var valid = intervals.Where(x => x.CpuPercent is not null).ToArray();
        var duration = valid.Sum(x => x.DurationSeconds);
        return duration > 0 ? valid.Where(predicate).Sum(x => x.DurationSeconds) / duration : 0;
    }

    private static (long Successful, long Skipped, long Failed) CalculateCaptureTotals(
        ProfilingEvaluationMode mode,
        ProfilingNodeParticipation participation,
        IReadOnlyList<ProfilingSnapshot> snapshots
    )
    {
        if (mode is ProfilingEvaluationMode.TwoSnapshots && snapshots.Count == 2)
        {
            return (
                Math.Max(2, snapshots[1].Sequence - snapshots[0].Sequence + 1),
                NonNegativeDelta(
                    snapshots[0].SkippedCaptureCount,
                    snapshots[1].SkippedCaptureCount
                ),
                NonNegativeDelta(snapshots[0].FailedCaptureCount, snapshots[1].FailedCaptureCount)
            );
        }

        return participation is not null
            ? (
                Math.Max(participation.SuccessfulCaptureCount, snapshots.Count),
                participation.SkippedCaptureCount,
                participation.FailedCaptureCount
            )
            : (
                snapshots.Count,
                snapshots.LastOrDefault()?.SkippedCaptureCount ?? 0,
                snapshots.LastOrDefault()?.FailedCaptureCount ?? 0
            );
    }

    private static IReadOnlyList<string> BuildLimitations(
        ProfilingEvaluationMode mode,
        ProfilingSession session,
        ProfilingRuntimeContext runtimeContext,
        int snapshotCount,
        TimeSpan elapsed,
        long failedCaptures,
        double? samplingCoverage,
        double? captureOverhead,
        TimeSpan? samplingDelayP95,
        bool invalidIntervals,
        bool missingSamples,
        bool hasMissingInputs
    )
    {
        var result = new List<string>();
        if (
            mode is ProfilingEvaluationMode.NodeSession
            && (snapshotCount < 5 || elapsed < TimeSpan.FromSeconds(5))
        )
        {
            result.Add("Collecting enough data for analysis.");
        }

        switch (session.State)
        {
            case ProfilingSessionState.CompletedWithWarnings:
                result.Add(
                    "Session completed with warnings; analysis may represent incomplete data."
                );
                break;
            case ProfilingSessionState.Stopped:
                result.Add("Session was stopped; analysis may represent an incomplete collection.");
                break;
            case ProfilingSessionState.Failed:
                result.Add("Session failed; analysis is limited to the available observations.");
                break;
        }

        if (runtimeContext?.DebuggerAttached is true)
        {
            result.Add("Debugger attached; timing and runtime behavior may be affected.");
        }

        if (failedCaptures > 0)
        {
            result.Add("One or more snapshot captures failed.");
        }

        if (samplingCoverage is < 90)
        {
            result.Add("Sampling coverage is below 90%.");
        }

        if (captureOverhead is >= 25)
        {
            result.Add("P95 snapshot capture overhead is at least 25%.");
        }

        if (
            samplingDelayP95 is not null
            && session.SamplingInterval > TimeSpan.Zero
            && samplingDelayP95.Value >= TimeSpan.FromTicks(session.SamplingInterval.Ticks / 2)
        )
        {
            result.Add("P95 sampling delay is at least 50% of the configured interval.");
        }

        if (invalidIntervals)
        {
            result.Add(
                "One or more invalid monotonic or cumulative-counter intervals were excluded."
            );
        }

        if (missingSamples)
        {
            result.Add("One or more expected samples were missing from the node-local sequence.");
        }

        if (hasMissingInputs)
        {
            result.Add("Some runtime metrics were unavailable; affected signals were suppressed.");
        }

        return result.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<ProfilingKpi> BuildKpis(
        double? cpuAverage,
        double? cpuPeak,
        double? cpuEnding,
        double? cpuChange,
        long? managedHeapStart,
        long? managedHeapEnd,
        double? managedHeapPeak,
        long? privateMemoryStart,
        long? privateMemoryEnd,
        double? privateMemoryPeak,
        long? lohStart,
        long? lohEnd,
        double? lohPeak,
        double? heapFragmentationEnd,
        double? heapFragmentationChange,
        double? lohFragmentationEnd,
        double? lohFragmentationChange,
        double? allocationAverage,
        double? allocationPeak,
        double? allocationChange,
        double? gcPauseBurden,
        long? gen0Delta,
        long? gen1Delta,
        long? gen2Delta,
        double? gen0Rate,
        double? gen1Rate,
        double? gen2Rate,
        double? samplingCoverage,
        long skippedCaptures,
        long failedCaptures,
        TimeSpan? captureDurationP95,
        double? captureOverhead,
        TimeSpan? samplingDelayP95
    ) =>
        [
            new("cpu-average", cpuAverage, "percent"),
            new("cpu-peak", cpuPeak, "percent"),
            new("cpu-ending", cpuEnding, "percent"),
            new("cpu-change", cpuChange, "percentage-points"),
            new("managed-heap-start", managedHeapStart, "bytes"),
            new("managed-heap-end", managedHeapEnd, "bytes"),
            new("managed-heap-change", Difference(managedHeapEnd, managedHeapStart), "bytes"),
            new(
                "managed-heap-change-percent",
                PercentageChange(managedHeapStart, managedHeapEnd),
                "percent"
            ),
            new("managed-heap-peak", managedHeapPeak, "bytes"),
            new("private-memory-start", privateMemoryStart, "bytes"),
            new("private-memory-end", privateMemoryEnd, "bytes"),
            new("private-memory-change", Difference(privateMemoryEnd, privateMemoryStart), "bytes"),
            new(
                "private-memory-change-percent",
                PercentageChange(privateMemoryStart, privateMemoryEnd),
                "percent"
            ),
            new("private-memory-peak", privateMemoryPeak, "bytes"),
            new("loh-start", lohStart, "bytes"),
            new("loh-end", lohEnd, "bytes"),
            new("loh-change", Difference(lohEnd, lohStart), "bytes"),
            new("loh-change-percent", PercentageChange(lohStart, lohEnd), "percent"),
            new("loh-peak", lohPeak, "bytes"),
            new("heap-fragmentation-ending", heapFragmentationEnd, "percent"),
            new("heap-fragmentation-change", heapFragmentationChange, "percentage-points"),
            new("loh-fragmentation-ending", lohFragmentationEnd, "percent"),
            new("loh-fragmentation-change", lohFragmentationChange, "percentage-points"),
            new("allocation-average", allocationAverage, "bytes-per-second"),
            new("allocation-peak", allocationPeak, "bytes-per-second"),
            new("allocation-change", allocationChange, "bytes-per-second"),
            new("gc-pause-burden", gcPauseBurden, "percent"),
            new("gen0-count-delta", gen0Delta, "count"),
            new("gen1-count-delta", gen1Delta, "count"),
            new("gen2-count-delta", gen2Delta, "count"),
            new("gen0-rate", gen0Rate, "collections-per-second"),
            new("gen1-rate", gen1Rate, "collections-per-second"),
            new("gen2-rate", gen2Rate, "collections-per-second"),
            new("sampling-coverage", samplingCoverage, "percent"),
            new("skipped-captures", skippedCaptures, "count"),
            new("failed-captures", failedCaptures, "count"),
            new("capture-duration-p95", captureDurationP95?.TotalMilliseconds, "milliseconds"),
            new("capture-overhead-p95", captureOverhead, "percent"),
            new("sampling-delay-p95", samplingDelayP95?.TotalMilliseconds, "milliseconds"),
        ];

    private static TimeSpan? Percentile95(IEnumerable<TimeSpan> source)
    {
        var ordered = source.OrderBy(x => x).ToArray();
        if (ordered.Length == 0)
        {
            return null;
        }

        var rank = (int)Math.Ceiling(0.95 * ordered.Length);
        return ordered[Math.Max(0, rank - 1)];
    }

    private static double? MaxValue(IEnumerable<double?> source)
    {
        var values = source.Where(x => x is not null).Select(x => x.Value).ToArray();
        return values.Length == 0 ? null : values.Max();
    }

    private static long? Sum(IEnumerable<long?> source)
    {
        var values = source.Where(x => x is not null).Select(x => x.Value).ToArray();
        return values.Length == 0 ? null : values.Sum();
    }

    private static double? Rate(long? count, TimeSpan elapsed) =>
        count is not null && elapsed > TimeSpan.Zero
            ? Round(count.Value / elapsed.TotalSeconds)
            : null;

    private static double? Difference(double? end, double? start) =>
        end is not null && start is not null ? end.Value - start.Value : null;

    private static double? Difference(long? end, long? start) =>
        end is not null && start is not null ? end.Value - start.Value : null;

    private static double? PercentageChange(long? start, long? end) =>
        start is > 0 && end is not null ? 100d * (end.Value - start.Value) / start.Value : null;

    private static double? ToDouble(long? value) => value;

    private static long NonNegativeDelta(long start, long end) => Math.Max(0, end - start);

    private static TimeSpan Max(TimeSpan first, TimeSpan second) =>
        first >= second ? first : second;

    private static double Round(double value) =>
        Math.Round(value, 9, MidpointRounding.AwayFromZero);

    private static void AddInput(
        string name,
        bool available,
        ICollection<string> availableInputs,
        ICollection<string> missingInputs
    )
    {
        (available ? availableInputs : missingInputs).Add(name);
    }
}
