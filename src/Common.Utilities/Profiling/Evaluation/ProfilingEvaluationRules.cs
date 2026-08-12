// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Provides the fixed interpretation-rule portion of <see cref="ProfilingEvaluator"/>.
/// </summary>
/// <example><code>var evaluator = new ProfilingEvaluator(options, store);</code></example>
public sealed partial class ProfilingEvaluator
{
    private const double CpuSustainedThreshold = 70;
    private const double CpuStrongAverageThreshold = 85;
    private const double CpuStrongIntervalThreshold = 80;
    private const double CpuSustainedRatio = 0.60;
    private const double CpuStrongRatio = 0.80;
    private const double CpuRisePoints = 20;
    private const double RelativeGrowthPercent = 20;
    private const long ManagedGrowthFloor = 32L * 1024 * 1024;
    private const long PrivateGrowthFloor = 64L * 1024 * 1024;
    private const long LohGrowthFloor = 32L * 1024 * 1024;
    private const double LohFragmentationThreshold = 20;
    private const double LohFragmentationRisePoints = 10;
    private const double AllocationSustainedThreshold = 50d * 1024 * 1024;
    private const double AllocationRiseFloor = 10d * 1024 * 1024;
    private const double Gen0ChurnRate = 0.5;
    private const double NotableGcPauseThreshold = 5;
    private const double StrongGcPauseThreshold = 10;
    private const double FullGcRateThreshold = 0.1;

    private const string CpuAction = "Capture a CPU profile and inspect hot methods.";
    private const string HeapAction = "Compare heap types and retained sizes.";
    private const string RetentionAction = "Inspect retained object roots after Gen2.";
    private const string ProcessMemoryAction = "Review native allocations and memory mappings.";
    private const string LohFragmentationAction =
        "Inspect large-object allocation and reuse patterns.";
    private const string AllocationAction = "Inspect the highest allocation hot paths.";
    private const string AllocationChurnAction = "Reduce short-lived allocation in hot paths.";
    private const string AllocationGrowthAction = "Inspect allocations that remain reachable.";
    private const string GcNotableAction = "Inspect GC events and heap pressure.";
    private const string GcInvestigateAction = "Inspect GC pauses, allocations, and retained heap.";

    private static IReadOnlyList<ProfilingSignal> EvaluateRules(EvaluationFacts facts)
    {
        var signals = new List<ProfilingSignal>();

        var managedGrowth = IsMeaningfulGrowth(
            facts.ManagedHeapStart,
            facts.ManagedHeapEnd,
            ManagedGrowthFloor
        );
        var privateGrowth = IsMeaningfulGrowth(
            facts.PrivateMemoryStart,
            facts.PrivateMemoryEnd,
            PrivateGrowthFloor
        );
        var lohGrowth = IsMeaningfulGrowth(facts.LohStart, facts.LohEnd, LohGrowthFloor);
        var sustainedAllocation = Meets(facts.AllocationAverage, AllocationSustainedThreshold);
        var notableGcPause = Meets(facts.GcPauseBurdenPercent, NotableGcPauseThreshold);
        var strongGcPause = Meets(facts.GcPauseBurdenPercent, StrongGcPauseThreshold);
        var frequentFullGc = facts.Gen2Delta is >= 2 && Meets(facts.Gen2Rate, FullGcRateThreshold);

        AddCpuSignals(facts, signals, managedGrowth, sustainedAllocation, notableGcPause);
        AddMemorySignals(facts, signals, managedGrowth, privateGrowth, lohGrowth);
        AddAllocationSignals(facts, signals, sustainedAllocation, managedGrowth);
        AddGcSignals(
            facts,
            signals,
            notableGcPause,
            strongGcPause,
            frequentFullGc,
            sustainedAllocation,
            managedGrowth,
            lohGrowth
        );

        return signals.OrderBy(x => x.Identifier, StringComparer.Ordinal).ToArray();
    }

    private static void AddCpuSignals(
        EvaluationFacts facts,
        ICollection<ProfilingSignal> signals,
        bool managedGrowth,
        bool sustainedAllocation,
        bool notableGcPause
    )
    {
        if (facts.Mode is ProfilingEvaluationMode.TwoSnapshots)
        {
            if (
                facts.CpuFirstHalfAverage is not null
                && facts.CpuSecondHalfAverage is not null
                && Meets(facts.CpuSecondHalfAverage - facts.CpuFirstHalfAverage, CpuRisePoints)
                && Meets(facts.CpuSecondHalfAverage, CpuSustainedThreshold)
            )
            {
                signals.Add(
                    Signal(
                        "two-snapshot-cpu-rise",
                        ProfilingSignalLabel.Notable,
                        "CPU usage increased materially between the selected snapshots.",
                        Confidence(facts),
                        CpuAction,
                        Evidence(
                            ("cpu-a", facts.CpuFirstHalfAverage.Value, "percent"),
                            ("cpu-b", facts.CpuSecondHalfAverage.Value, "percent"),
                            ("rise-threshold", CpuRisePoints, "percentage-points"),
                            ("ending-threshold", CpuSustainedThreshold, "percent")
                        )
                    )
                );
            }

            return;
        }

        var strong =
            Meets(facts.CpuAverage, CpuStrongAverageThreshold)
            && Meets(facts.CpuAtLeast80Ratio, CpuStrongRatio);
        var sustained =
            Meets(facts.CpuAverage, CpuSustainedThreshold)
            && Meets(facts.CpuAtLeast70Ratio, CpuSustainedRatio);
        var rising =
            facts.CpuFirstHalfAverage is not null
            && facts.CpuSecondHalfAverage is not null
            && Meets(facts.CpuSecondHalfAverage - facts.CpuFirstHalfAverage, CpuRisePoints)
            && Meets(facts.CpuEnding, CpuSustainedThreshold);
        var independentSupport = rising || managedGrowth || sustainedAllocation || notableGcPause;

        if (strong)
        {
            signals.Add(
                Signal(
                    "strong-sustained-cpu",
                    ProfilingSignalLabel.Investigate,
                    "CPU usage remained at a strongly elevated level.",
                    Confidence(
                        facts,
                        highEligible: independentSupport,
                        hasAllInputs: facts.HasCpuInput
                    ),
                    CpuAction,
                    Evidence(
                        ("cpu-average", facts.CpuAverage.Value, "percent"),
                        ("interval-ratio", 100 * facts.CpuAtLeast80Ratio, "percent"),
                        ("average-threshold", CpuStrongAverageThreshold, "percent"),
                        ("interval-threshold", 100 * CpuStrongRatio, "percent")
                    )
                )
            );
        }
        else if (sustained)
        {
            signals.Add(
                Signal(
                    "sustained-cpu",
                    ProfilingSignalLabel.Notable,
                    "CPU usage remained elevated across the evaluated timeline.",
                    Confidence(
                        facts,
                        highEligible: independentSupport,
                        hasAllInputs: facts.HasCpuInput
                    ),
                    CpuAction,
                    Evidence(
                        ("cpu-average", facts.CpuAverage.Value, "percent"),
                        ("interval-ratio", 100 * facts.CpuAtLeast70Ratio, "percent"),
                        ("average-threshold", CpuSustainedThreshold, "percent"),
                        ("interval-threshold", 100 * CpuSustainedRatio, "percent")
                    )
                )
            );
        }

        if (rising)
        {
            signals.Add(
                Signal(
                    "rising-cpu",
                    ProfilingSignalLabel.Notable,
                    "CPU usage increased in the second half and ended elevated.",
                    Confidence(facts),
                    CpuAction,
                    Evidence(
                        ("first-half-average", facts.CpuFirstHalfAverage.Value, "percent"),
                        ("second-half-average", facts.CpuSecondHalfAverage.Value, "percent"),
                        ("ending", facts.CpuEnding.Value, "percent"),
                        ("rise-threshold", CpuRisePoints, "percentage-points")
                    )
                )
            );
        }
    }

    private static void AddMemorySignals(
        EvaluationFacts facts,
        ICollection<ProfilingSignal> signals,
        bool managedGrowth,
        bool privateGrowth,
        bool lohGrowth
    )
    {
        var possibleRetention =
            managedGrowth
            && facts.Gen2Delta is >= 1
            && IsMeaningfulGrowth(
                facts.ManagedHeapStart,
                facts.LatestGen2ManagedHeapBytes,
                ManagedGrowthFloor
            );
        if (possibleRetention)
        {
            signals.Add(
                Signal(
                    "possible-retention",
                    ProfilingSignalLabel.Investigate,
                    "Managed heap remained elevated after a directly observed Gen2 collection.",
                    Confidence(
                        facts,
                        highEligible: true,
                        hasAllInputs: facts.HasManagedHeapInput
                            && facts.HasGcInput
                            && facts.LatestGen2ManagedHeapBytes is not null
                    ),
                    RetentionAction,
                    Evidence(
                        ("heap-start", facts.ManagedHeapStart.Value, "bytes"),
                        ("heap-end", facts.ManagedHeapEnd.Value, "bytes"),
                        ("post-gen2-heap", facts.LatestGen2ManagedHeapBytes.Value, "bytes"),
                        ("gen2-count-delta", facts.Gen2Delta.Value, "count"),
                        ("growth-threshold", RelativeGrowthPercent, "percent"),
                        ("growth-floor", ManagedGrowthFloor, "bytes")
                    )
                )
            );
        }
        else if (managedGrowth)
        {
            signals.Add(
                GrowthSignal(
                    facts,
                    "managed-heap-growth",
                    "Managed heap increased materially over the evaluated scope.",
                    facts.ManagedHeapStart.Value,
                    facts.ManagedHeapEnd.Value,
                    ManagedGrowthFloor,
                    HeapAction
                )
            );
        }

        if (privateGrowth && facts.ManagedHeapStart is not null && facts.ManagedHeapEnd is not null)
        {
            var privateDelta = facts.PrivateMemoryEnd.Value - facts.PrivateMemoryStart.Value;
            var heapDelta = Math.Max(0, facts.ManagedHeapEnd.Value - facts.ManagedHeapStart.Value);
            if (heapDelta < privateDelta / 2d)
            {
                signals.Add(
                    Signal(
                        "unexplained-process-memory-growth",
                        ProfilingSignalLabel.Investigate,
                        "Private memory growth was not primarily explained by managed heap growth.",
                        Confidence(facts),
                        ProcessMemoryAction,
                        Evidence(
                            ("private-memory-delta", privateDelta, "bytes"),
                            ("managed-heap-delta", heapDelta, "bytes"),
                            ("growth-threshold", RelativeGrowthPercent, "percent"),
                            ("growth-floor", PrivateGrowthFloor, "bytes")
                        )
                    )
                );
            }
        }

        if (lohGrowth)
        {
            signals.Add(
                GrowthSignal(
                    facts,
                    "loh-growth",
                    "Large object heap size increased materially over the evaluated scope.",
                    facts.LohStart.Value,
                    facts.LohEnd.Value,
                    LohGrowthFloor,
                    HeapAction
                )
            );
        }

        if (
            facts.LohFragmentationStart is not null
            && Meets(facts.LohFragmentationEnd, LohFragmentationThreshold)
            && Meets(
                facts.LohFragmentationEnd - facts.LohFragmentationStart,
                LohFragmentationRisePoints
            )
        )
        {
            signals.Add(
                Signal(
                    "loh-fragmentation",
                    ProfilingSignalLabel.Notable,
                    "Large object heap fragmentation ended elevated after a material increase.",
                    Confidence(facts),
                    LohFragmentationAction,
                    Evidence(
                        ("fragmentation-start", facts.LohFragmentationStart.Value, "percent"),
                        ("fragmentation-end", facts.LohFragmentationEnd.Value, "percent"),
                        ("ending-threshold", LohFragmentationThreshold, "percent"),
                        ("rise-threshold", LohFragmentationRisePoints, "percentage-points")
                    )
                )
            );
        }
    }

    private static void AddAllocationSignals(
        EvaluationFacts facts,
        ICollection<ProfilingSignal> signals,
        bool sustainedAllocation,
        bool managedGrowth
    )
    {
        if (facts.Mode is ProfilingEvaluationMode.TwoSnapshots)
        {
            if (
                IsAtLeastDoubleWithFloor(
                    facts.AllocationStart,
                    facts.AllocationEnd,
                    AllocationRiseFloor
                )
            )
            {
                signals.Add(
                    Signal(
                        "two-snapshot-allocation-rise",
                        ProfilingSignalLabel.Notable,
                        "Allocation rate increased materially between the selected snapshots.",
                        Confidence(facts),
                        AllocationAction,
                        Evidence(
                            ("allocation-a", facts.AllocationStart.Value, "bytes-per-second"),
                            ("allocation-b", facts.AllocationEnd.Value, "bytes-per-second"),
                            ("multiple-threshold", 2, "ratio"),
                            ("increase-floor", AllocationRiseFloor, "bytes-per-second")
                        )
                    )
                );
            }

            return;
        }

        var rising = IsAtLeastDoubleWithFloor(
            facts.AllocationFirstHalfAverage,
            facts.AllocationSecondHalfAverage,
            AllocationRiseFloor
        );
        var churn = sustainedAllocation && Meets(facts.Gen0Rate, Gen0ChurnRate) && !managedGrowth;
        var withHeapGrowth = sustainedAllocation && managedGrowth;

        if (rising)
        {
            signals.Add(
                Signal(
                    "rising-allocation",
                    ProfilingSignalLabel.Notable,
                    "Allocation rate increased materially in the second half.",
                    Confidence(facts),
                    AllocationAction,
                    Evidence(
                        (
                            "first-half-average",
                            facts.AllocationFirstHalfAverage.Value,
                            "bytes-per-second"
                        ),
                        (
                            "second-half-average",
                            facts.AllocationSecondHalfAverage.Value,
                            "bytes-per-second"
                        ),
                        ("multiple-threshold", 2, "ratio"),
                        ("increase-floor", AllocationRiseFloor, "bytes-per-second")
                    )
                )
            );
        }

        if (withHeapGrowth)
        {
            signals.Add(
                Signal(
                    "allocation-with-heap-growth",
                    ProfilingSignalLabel.Investigate,
                    "Sustained allocation coincided with material managed heap growth.",
                    Confidence(
                        facts,
                        highEligible: true,
                        hasAllInputs: facts.HasAllocationInput && facts.HasManagedHeapInput
                    ),
                    AllocationGrowthAction,
                    Evidence(
                        ("allocation-average", facts.AllocationAverage.Value, "bytes-per-second"),
                        ("allocation-threshold", AllocationSustainedThreshold, "bytes-per-second"),
                        (
                            "managed-heap-delta",
                            facts.ManagedHeapEnd.Value - facts.ManagedHeapStart.Value,
                            "bytes"
                        )
                    )
                )
            );
        }
        else if (churn)
        {
            signals.Add(
                Signal(
                    "allocation-churn",
                    ProfilingSignalLabel.Investigate,
                    "Sustained allocation coincided with frequent Gen0 collection without material heap growth.",
                    Confidence(
                        facts,
                        highEligible: true,
                        hasAllInputs: facts.HasAllocationInput
                            && facts.HasGcInput
                            && facts.HasManagedHeapInput
                    ),
                    AllocationChurnAction,
                    Evidence(
                        ("allocation-average", facts.AllocationAverage.Value, "bytes-per-second"),
                        ("gen0-rate", facts.Gen0Rate.Value, "collections-per-second"),
                        ("gen0-rate-threshold", Gen0ChurnRate, "collections-per-second")
                    )
                )
            );
        }
        else if (sustainedAllocation)
        {
            signals.Add(
                Signal(
                    "sustained-allocation",
                    ProfilingSignalLabel.Notable,
                    "Allocation rate remained elevated across the evaluated timeline.",
                    Confidence(facts),
                    AllocationAction,
                    Evidence(
                        ("allocation-average", facts.AllocationAverage.Value, "bytes-per-second"),
                        ("average-threshold", AllocationSustainedThreshold, "bytes-per-second")
                    )
                )
            );
        }
    }

    private static void AddGcSignals(
        EvaluationFacts facts,
        ICollection<ProfilingSignal> signals,
        bool notableGcPause,
        bool strongGcPause,
        bool frequentFullGc,
        bool sustainedAllocation,
        bool managedGrowth,
        bool lohGrowth
    )
    {
        var supportingCount = new[]
        {
            frequentFullGc,
            sustainedAllocation,
            managedGrowth,
            lohGrowth,
        }.Count(x => x);
        var gcPressure = notableGcPause && supportingCount >= 1;
        var strongGcPressure = strongGcPause || (notableGcPause && supportingCount >= 2);

        if (strongGcPressure)
        {
            signals.Add(
                Signal(
                    "strong-gc-pressure",
                    ProfilingSignalLabel.Investigate,
                    "GC pause burden and supporting pressure evidence were strongly elevated.",
                    Confidence(
                        facts,
                        highEligible: supportingCount >= 1,
                        hasAllInputs: facts.HasGcInput
                    ),
                    GcInvestigateAction,
                    Evidence(
                        ("gc-pause-burden", facts.GcPauseBurdenPercent.Value, "percent"),
                        ("strong-pause-threshold", StrongGcPauseThreshold, "percent"),
                        ("supporting-condition-count", supportingCount, "count")
                    )
                )
            );
            return;
        }

        if (gcPressure)
        {
            signals.Add(
                Signal(
                    "gc-pressure",
                    ProfilingSignalLabel.Investigate,
                    "Notable GC pause burden coincided with other memory or collection pressure.",
                    Confidence(facts, highEligible: true, hasAllInputs: facts.HasGcInput),
                    GcInvestigateAction,
                    Evidence(
                        ("gc-pause-burden", facts.GcPauseBurdenPercent.Value, "percent"),
                        ("pause-threshold", NotableGcPauseThreshold, "percent"),
                        ("supporting-condition-count", supportingCount, "count")
                    )
                )
            );
            return;
        }

        if (strongGcPause)
        {
            signals.Add(
                Signal(
                    "strong-gc-pause",
                    ProfilingSignalLabel.Investigate,
                    "GC pause burden reached the strong fixed threshold.",
                    Confidence(facts),
                    GcInvestigateAction,
                    Evidence(
                        ("gc-pause-burden", facts.GcPauseBurdenPercent.Value, "percent"),
                        ("pause-threshold", StrongGcPauseThreshold, "percent")
                    )
                )
            );
        }
        else if (notableGcPause)
        {
            signals.Add(
                Signal(
                    "notable-gc-pause",
                    ProfilingSignalLabel.Notable,
                    "GC pause burden reached the notable fixed threshold.",
                    Confidence(facts),
                    GcNotableAction,
                    Evidence(
                        ("gc-pause-burden", facts.GcPauseBurdenPercent.Value, "percent"),
                        ("pause-threshold", NotableGcPauseThreshold, "percent")
                    )
                )
            );
        }

        if (frequentFullGc)
        {
            signals.Add(
                Signal(
                    "frequent-full-gc",
                    ProfilingSignalLabel.Notable,
                    "Gen2 collections were frequent across the evaluated scope.",
                    Confidence(facts),
                    GcNotableAction,
                    Evidence(
                        ("gen2-count-delta", facts.Gen2Delta.Value, "count"),
                        ("gen2-rate", facts.Gen2Rate.Value, "collections-per-second"),
                        ("count-threshold", 2, "count"),
                        ("rate-threshold", FullGcRateThreshold, "collections-per-second")
                    )
                )
            );
        }
    }

    private static ProfilingSignal GrowthSignal(
        EvaluationFacts facts,
        string identifier,
        string explanation,
        long start,
        long end,
        long floor,
        string action
    ) =>
        Signal(
            identifier,
            ProfilingSignalLabel.Notable,
            explanation,
            Confidence(facts),
            action,
            Evidence(
                ("start", start, "bytes"),
                ("end", end, "bytes"),
                ("growth-percent", 100d * (end - start) / start, "percent"),
                ("growth-threshold", RelativeGrowthPercent, "percent"),
                ("growth-floor", floor, "bytes")
            )
        );

    private static bool IsMeaningfulGrowth(long? start, long? end, long floor) =>
        start is > 0
        && end is not null
        && end.Value - start.Value >= floor
        && 100d * (end.Value - start.Value) / start.Value >= RelativeGrowthPercent;

    private static bool IsAtLeastDoubleWithFloor(double? start, double? end, double floor) =>
        start is >= 0
        && end is not null
        && Meets(end.Value, 2 * start.Value)
        && Meets(end.Value - start.Value, floor);

    private static bool Meets(double? value, double threshold) =>
        value is not null && value.Value + 0.000001 >= threshold;

    private static bool Meets(double value, double threshold) => value + 0.000001 >= threshold;

    private static ProfilingSignalConfidence Confidence(
        EvaluationFacts facts,
        bool highEligible = false,
        bool hasAllInputs = true
    )
    {
        if (facts.Mode is ProfilingEvaluationMode.TwoSnapshots)
        {
            return ProfilingSignalConfidence.Low;
        }

        return
            facts.HighConfidenceWindow
            && facts.HighConfidenceAllowed
            && highEligible
            && hasAllInputs
            ? ProfilingSignalConfidence.High
            : ProfilingSignalConfidence.Medium;
    }

    private static ProfilingSignal Signal(
        string identifier,
        ProfilingSignalLabel label,
        string explanation,
        ProfilingSignalConfidence confidence,
        string action,
        IReadOnlyList<ProfilingSignalEvidence> evidence
    ) => new(identifier, label, explanation, evidence, confidence, action);

    private static IReadOnlyList<ProfilingSignalEvidence> Evidence(
        params (string Identifier, double Value, string Unit)[] values
    ) => values.Select(x => new ProfilingSignalEvidence(x.Identifier, x.Value, x.Unit)).ToArray();
}
