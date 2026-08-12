// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Profiling;

using System.Text.Json;

public class ProfilingEvaluatorTests
{
    private const string SessionKey = "sess0001";
    private const string NodeKey = "node0001";
    private const long MiB = 1024 * 1024;

    [Fact]
    public async Task EvaluateAsync_FixedRuleBoundaries_AreInclusive()
    {
        // Arrange
        var cases = CreateBoundaryCases();

        // Act
        foreach (var boundary in cases)
        {
            var below = await EvaluateAsync(boundary.Create(0.999));
            var exact = await EvaluateAsync(boundary.Create(1));
            var above = await EvaluateAsync(boundary.Create(1.001));

            // Assert
            Assert.DoesNotContain(
                below.Value.Signals,
                x => x.Identifier == boundary.SignalIdentifier
            );
            Assert.True(
                exact.Value.Signals.Any(x => x.Identifier == boundary.SignalIdentifier),
                $"Exact boundary did not emit '{boundary.SignalIdentifier}'."
            );
            Assert.True(
                above.Value.Signals.Any(x => x.Identifier == boundary.SignalIdentifier),
                $"Above boundary did not emit '{boundary.SignalIdentifier}'."
            );
        }
    }

    [Fact]
    public async Task EvaluateAsync_PairMode_ValidatesKeysScopeAndOrdering()
    {
        // Arrange
        var data = BuildData(new() { Pair = true });
        var evaluator = CreateEvaluator(data);
        var first = data.Snapshots[0];
        var second = data.Snapshots[1];

        // Act
        var missingSecond = await evaluator.EvaluateAsync(
            new(SessionKey, NodeKey, first.Identity.Key)
        );
        var reversed = await evaluator.EvaluateAsync(
            new(SessionKey, NodeKey, second.Identity.Key, first.Identity.Key)
        );
        var otherNodeData = data with
        {
            Snapshots = [first, second with { NodeKey = "node0002", NodeId = Guid.NewGuid() }],
        };
        var otherNode = await CreateEvaluator(otherNodeData)
            .EvaluateAsync(new(SessionKey, NodeKey, first.Identity.Key, second.Identity.Key));

        // Assert
        missingSecond.IsFailure.ShouldBeTrue();
        missingSecond.Errors.ShouldContain(x => x is ProfilingValidationError);
        reversed.IsFailure.ShouldBeTrue();
        reversed.Errors.ShouldContain(x => x is ProfilingValidationError);
        otherNode.IsFailure.ShouldBeTrue();
        otherNode.Errors.ShouldContain(x => x is ProfilingValidationError);
    }

    [Fact]
    public async Task EvaluateAsync_MinimumTimelineWindow_GatesSignals()
    {
        // Arrange
        var insufficientCount = new Scenario
        {
            SnapshotCount = 4,
            SpanSeconds = 5,
            CpuInterval = _ => 75,
        };
        var insufficientSpan = insufficientCount with { SnapshotCount = 5, SpanSeconds = 4.999 };
        var sufficient = insufficientSpan with { SpanSeconds = 5 };

        // Act
        var countResult = await EvaluateAsync(insufficientCount);
        var spanResult = await EvaluateAsync(insufficientSpan);
        var sufficientResult = await EvaluateAsync(sufficient);

        // Assert
        countResult.Value.Signals.ShouldBeEmpty();
        spanResult.Value.Signals.ShouldBeEmpty();
        countResult.Value.DataQuality.Sufficiency.ShouldBe(ProfilingDataSufficiency.Collecting);
        countResult.Value.Limitations.ShouldContain("Collecting enough data for analysis.");
        sufficientResult.Value.Signals.ShouldContain(x => x.Identifier == "sustained-cpu");
    }

    [Fact]
    public async Task EvaluateAsync_HighConfidenceWindow_RequiresTenSnapshotsAndSeconds()
    {
        // Arrange
        var baseScenario = HighConfidenceScenario();
        var belowCount = baseScenario with { SnapshotCount = 9 };
        var belowSpan = baseScenario with { SpanSeconds = 9.999 };

        // Act
        var countResult = await EvaluateAsync(belowCount);
        var spanResult = await EvaluateAsync(belowSpan);
        var exactResult = await EvaluateAsync(baseScenario);

        // Assert
        Signal(countResult, "strong-sustained-cpu")
            .Confidence.ShouldBe(ProfilingSignalConfidence.Medium);
        Signal(spanResult, "strong-sustained-cpu")
            .Confidence.ShouldBe(ProfilingSignalConfidence.Medium);
        Signal(exactResult, "strong-sustained-cpu")
            .Confidence.ShouldBe(ProfilingSignalConfidence.High);
    }

    [Fact]
    public async Task EvaluateAsync_SamplingAndDebuggerLimitations_CapHighConfidence()
    {
        // Arrange
        var cases = new[]
        {
            new QualityCase(
                HighConfidenceScenario() with
                {
                    FailedCaptures = 1,
                },
                "One or more snapshot captures failed."
            ),
            new QualityCase(
                HighConfidenceScenario() with
                {
                    SkippedCaptures = 2,
                },
                "Sampling coverage is below 90%."
            ),
            new QualityCase(
                HighConfidenceScenario() with
                {
                    CaptureDuration = TimeSpan.FromMilliseconds(250),
                },
                "P95 snapshot capture overhead is at least 25%."
            ),
            new QualityCase(
                HighConfidenceScenario() with
                {
                    SamplingDelay = TimeSpan.FromMilliseconds(500),
                },
                "P95 sampling delay is at least 50% of the configured interval."
            ),
            new QualityCase(
                HighConfidenceScenario() with
                {
                    DebuggerAttached = true,
                },
                "Debugger attached; timing and runtime behavior may be affected."
            ),
        };

        // Act
        foreach (var qualityCase in cases)
        {
            var result = await EvaluateAsync(qualityCase.Scenario);

            // Assert
            Signal(result, "strong-sustained-cpu")
                .Confidence.ShouldBe(ProfilingSignalConfidence.Medium);
            result.Value.Limitations.ShouldContain(qualityCase.Limitation);
            result.Value.DataQuality.Sufficiency.ShouldBe(ProfilingDataSufficiency.Limited);
        }
    }

    [Fact]
    public async Task EvaluateAsync_CounterResetAndMissingMetrics_ExcludeAffectedEvidence()
    {
        // Arrange
        var data = BuildData(new() { AllocationInterval = _ => 60 * MiB });
        var snapshots = data.Snapshots.ToArray();
        snapshots[5] = snapshots[5] with
        {
            TotalAllocatedBytes = snapshots[4].TotalAllocatedBytes - 1,
        };
        var resetData = data with { Snapshots = snapshots };
        var missingCpu = new Scenario { IncludeCpu = false, CpuInterval = _ => 90 };

        // Act
        var reset = await CreateEvaluator(resetData).EvaluateAsync(new(SessionKey, NodeKey));
        var missing = await EvaluateAsync(missingCpu);

        // Assert
        reset.Value.Limitations.ShouldContain(
            "One or more invalid monotonic or cumulative-counter intervals were excluded."
        );
        reset.Value.DataQuality.Sufficiency.ShouldBe(ProfilingDataSufficiency.Limited);
        missing.Value.DataQuality.MissingInputs.ShouldContain("cpu");
        missing.Value.Signals.ShouldNotContain(x => x.Identifier.Contains("cpu"));
    }

    [Fact]
    public async Task EvaluateAsync_UtcClockMovement_UsesMonotonicIntervals()
    {
        // Arrange
        var scenario = new Scenario { ReverseUtc = true, CpuInterval = _ => 75 };

        // Act
        var result = await EvaluateAsync(scenario);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Signals.ShouldContain(x => x.Identifier == "sustained-cpu");
        result.Value.Limitations.ShouldNotContain(x =>
            x.Contains("monotonic", StringComparison.OrdinalIgnoreCase)
        );
    }

    [Fact]
    public async Task EvaluateAsync_IsolatedCpuPeak_EmitsNoCpuSignal()
    {
        // Arrange
        var scenario = new Scenario
        {
            CpuInterval = fraction => Math.Abs(fraction - 0.444) < 0.01 ? 100 : 10,
        };

        // Act
        var result = await EvaluateAsync(scenario);

        // Assert
        result.Value.KPIs.Single(x => x.Identifier == "cpu-peak").Value.ShouldBe(100);
        result.Value.Signals.ShouldNotContain(x => x.Identifier.Contains("cpu"));
    }

    [Fact]
    public async Task EvaluateAsync_SameInput_ReturnsStructurallyEqualResult()
    {
        // Arrange
        var evaluator = CreateEvaluator(BuildData(HighConfidenceScenario()));
        var request = new ProfilingEvaluationRequest(SessionKey, NodeKey);

        // Act
        var first = await evaluator.EvaluateAsync(request);
        var second = await evaluator.EvaluateAsync(request);

        // Assert
        JsonSerializer.Serialize(first.Value).ShouldBe(JsonSerializer.Serialize(second.Value));
    }

    [Fact]
    public async Task EvaluateAsync_ReadsOnceAndPerformsNoStoreWrites()
    {
        // Arrange
        var data = BuildData(new());
        var store = Substitute.For<IProfilingStore>();
        store
            .GetSessionDataAsync(SessionKey, Arg.Any<CancellationToken>())
            .Returns(Result<ProfilingSessionData>.Success(data));
        var evaluator = new ProfilingEvaluator(new ProfilingOptions { Enabled = true }, store);

        // Act
        var result = await evaluator.EvaluateAsync(new(SessionKey, NodeKey));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await store.Received(1).GetSessionDataAsync(SessionKey, Arg.Any<CancellationToken>());
        store.ReceivedCalls().Select(x => x.GetMethodInfo().Name).ShouldBe(["GetSessionDataAsync"]);
    }

    [Fact]
    public async Task EvaluateAsync_PairSignals_AlwaysHaveLowConfidence()
    {
        // Arrange
        var scenario = new Scenario
        {
            Pair = true,
            CpuSnapshotStart = 50,
            CpuSnapshotEnd = 70,
            ManagedHeapStart = 160 * MiB,
            ManagedHeapEnd = 192 * MiB,
            AllocationSnapshotStart = 10 * MiB,
            AllocationSnapshotEnd = 20 * MiB,
        };

        // Act
        var result = await EvaluateAsync(scenario);

        // Assert
        result.Value.Scope.Mode.ShouldBe(ProfilingEvaluationMode.TwoSnapshots);
        result.Value.Signals.ShouldNotBeEmpty();
        result.Value.Signals.ShouldAllBe(x => x.Confidence == ProfilingSignalConfidence.Low);
    }

    [Fact]
    public async Task EvaluateAsync_SessionState_SetsProvisionalAndTerminalLimitations()
    {
        // Arrange
        var running = new Scenario { State = ProfilingSessionState.Running };
        var completed = running with { State = ProfilingSessionState.Completed };
        var warnings = running with { State = ProfilingSessionState.CompletedWithWarnings };

        // Act
        var runningResult = await EvaluateAsync(running);
        var completedResult = await EvaluateAsync(completed);
        var warningResult = await EvaluateAsync(warnings);

        // Assert
        runningResult.Value.Scope.Provisional.ShouldBeTrue();
        completedResult.Value.Scope.Provisional.ShouldBeFalse();
        completedResult.Value.Limitations.ShouldNotContain(x =>
            x.Contains("incomplete", StringComparison.OrdinalIgnoreCase)
        );
        warningResult.Value.Scope.Provisional.ShouldBeFalse();
        warningResult.Value.Limitations.ShouldContain(
            "Session completed with warnings; analysis may represent incomplete data."
        );
    }

    [Fact]
    public async Task EvaluateAsync_DisabledUnavailableAndInvalidKey_ReturnTypedFailures()
    {
        // Arrange
        var disabled = new ProfilingEvaluator(new ProfilingOptions(), null);
        var unavailable = new ProfilingEvaluator(new ProfilingOptions { Enabled = true }, null);
        var evaluator = CreateEvaluator(BuildData(new()));

        // Act
        var disabledResult = await disabled.EvaluateAsync(new(SessionKey, NodeKey));
        var unavailableResult = await unavailable.EvaluateAsync(new(SessionKey, NodeKey));
        var invalidResult = await evaluator.EvaluateAsync(new("INVALID", NodeKey));

        // Assert
        disabledResult.Errors.ShouldContain(x => x is ProfilingDisabledError);
        unavailableResult.Errors.ShouldContain(x => x is ProfilingUnavailableError);
        invalidResult.Errors.ShouldContain(x => x is ProfilingInvalidKeyError);
    }

    [Fact]
    public async Task EvaluateAsync_IrregularIntervals_UsesTimeWeightedAverage()
    {
        // Arrange
        var data = BuildData(new() { SnapshotCount = 3, SpanSeconds = 10 });
        var snapshots = data.Snapshots.ToArray();
        snapshots[0] = snapshots[0] with
        {
            CaptureStartedElapsed = TimeSpan.Zero,
            ScheduledElapsed = TimeSpan.Zero,
            ProcessCpuDuration = TimeSpan.Zero,
            CpuUsagePercent = 10,
        };
        snapshots[1] = snapshots[1] with
        {
            CaptureStartedElapsed = TimeSpan.FromSeconds(1),
            ScheduledElapsed = TimeSpan.FromSeconds(1),
            ProcessCpuDuration = TimeSpan.FromSeconds(0.1),
            CpuUsagePercent = 10,
        };
        snapshots[2] = snapshots[2] with
        {
            CaptureStartedElapsed = TimeSpan.FromSeconds(10),
            ScheduledElapsed = TimeSpan.FromSeconds(10),
            ProcessCpuDuration = TimeSpan.FromSeconds(8.2),
            CpuUsagePercent = 90,
        };

        // Act
        var result = await CreateEvaluator(data with { Snapshots = snapshots })
            .EvaluateAsync(new(SessionKey, NodeKey));

        // Assert
        result
            .Value.KPIs.Single(x => x.Identifier == "cpu-average")
            .Value.Value.ShouldBe(82, tolerance: 0.000001);
    }

    [Fact]
    public async Task EvaluateAsync_P95Values_UseNearestRank()
    {
        // Arrange
        var data = BuildData(new() { SnapshotCount = 20, SpanSeconds = 20 });
        var snapshots = data
            .Snapshots.Select(
                (snapshot, index) =>
                    snapshot with
                    {
                        CaptureDuration = TimeSpan.FromMilliseconds(index + 1),
                        CaptureStartedElapsed =
                            snapshot.ScheduledElapsed + TimeSpan.FromMilliseconds(index + 1),
                    }
            )
            .ToArray();

        // Act
        var result = await CreateEvaluator(data with { Snapshots = snapshots })
            .EvaluateAsync(new(SessionKey, NodeKey));

        // Assert
        result.Value.DataQuality.CaptureDurationP95.ShouldBe(TimeSpan.FromMilliseconds(19));
        result.Value.DataQuality.SamplingDelayP95.ShouldBe(TimeSpan.FromMilliseconds(19));
    }

    [Fact]
    public async Task EvaluateAsync_MissingSequence_AddsDataQualityLimitation()
    {
        // Arrange
        var data = BuildData(new());
        var snapshots = data
            .Snapshots.Select(
                (snapshot, index) => index == 0 ? snapshot : snapshot with { Sequence = index + 2 }
            )
            .ToArray();

        // Act
        var result = await CreateEvaluator(data with { Snapshots = snapshots })
            .EvaluateAsync(new(SessionKey, NodeKey));

        // Assert
        result.Value.Limitations.ShouldContain(
            "One or more expected samples were missing from the node-local sequence."
        );
        result.Value.DataQuality.Sufficiency.ShouldBe(ProfilingDataSufficiency.Limited);
    }

    [Fact]
    public async Task EvaluateAsync_FixedSignals_UseApprovedLabelsAndActions()
    {
        // Arrange
        var observed = new Dictionary<string, ProfilingSignal>(StringComparer.Ordinal);

        // Act
        foreach (var boundary in CreateBoundaryCases())
        {
            var result = await EvaluateAsync(boundary.Create(1));
            foreach (var signal in result.Value.Signals)
            {
                observed.TryAdd(signal.Identifier, signal);
            }
        }

        // Assert
        AssertSignal(
            observed,
            "sustained-cpu",
            ProfilingSignalLabel.Notable,
            "Capture a CPU profile and inspect hot methods."
        );
        AssertSignal(
            observed,
            "strong-sustained-cpu",
            ProfilingSignalLabel.Investigate,
            "Capture a CPU profile and inspect hot methods."
        );
        AssertSignal(
            observed,
            "managed-heap-growth",
            ProfilingSignalLabel.Notable,
            "Compare heap types and retained sizes."
        );
        AssertSignal(
            observed,
            "possible-retention",
            ProfilingSignalLabel.Investigate,
            "Inspect retained object roots after Gen2."
        );
        AssertSignal(
            observed,
            "unexplained-process-memory-growth",
            ProfilingSignalLabel.Investigate,
            "Review native allocations and memory mappings."
        );
        AssertSignal(
            observed,
            "loh-fragmentation",
            ProfilingSignalLabel.Notable,
            "Inspect large-object allocation and reuse patterns."
        );
        AssertSignal(
            observed,
            "sustained-allocation",
            ProfilingSignalLabel.Notable,
            "Inspect the highest allocation hot paths."
        );
        AssertSignal(
            observed,
            "allocation-churn",
            ProfilingSignalLabel.Investigate,
            "Reduce short-lived allocation in hot paths."
        );
        AssertSignal(
            observed,
            "allocation-with-heap-growth",
            ProfilingSignalLabel.Investigate,
            "Inspect allocations that remain reachable."
        );
        AssertSignal(
            observed,
            "notable-gc-pause",
            ProfilingSignalLabel.Notable,
            "Inspect GC events and heap pressure."
        );
        AssertSignal(
            observed,
            "strong-gc-pressure",
            ProfilingSignalLabel.Investigate,
            "Inspect GC pauses, allocations, and retained heap."
        );
    }

    [Fact]
    public async Task EvaluateAsync_StrongerSignals_SuppressWeakerDuplicateEvidence()
    {
        // Arrange
        var strongCpu = new Scenario { CpuInterval = _ => 90 };
        var retention = new Scenario
        {
            ManagedHeapStart = 160 * MiB,
            ManagedHeapEnd = 192 * MiB,
            Gen2Delta = 1,
            LatestGen2ManagedHeapBytes = 192 * MiB,
        };
        var allocationGrowth = new Scenario
        {
            AllocationInterval = _ => 60 * MiB,
            ManagedHeapStart = 160 * MiB,
            ManagedHeapEnd = 192 * MiB,
        };
        var strongGc = new Scenario { GcPausePercent = 10 };

        // Act
        var cpuResult = await EvaluateAsync(strongCpu);
        var retentionResult = await EvaluateAsync(retention);
        var allocationResult = await EvaluateAsync(allocationGrowth);
        var gcResult = await EvaluateAsync(strongGc);

        // Assert
        cpuResult.Value.Signals.ShouldContain(x => x.Identifier == "strong-sustained-cpu");
        cpuResult.Value.Signals.ShouldNotContain(x => x.Identifier == "sustained-cpu");
        retentionResult.Value.Signals.ShouldContain(x => x.Identifier == "possible-retention");
        retentionResult.Value.Signals.ShouldNotContain(x => x.Identifier == "managed-heap-growth");
        allocationResult.Value.Signals.ShouldContain(x =>
            x.Identifier == "allocation-with-heap-growth"
        );
        allocationResult.Value.Signals.ShouldNotContain(x =>
            x.Identifier == "sustained-allocation"
        );
        gcResult.Value.Signals.ShouldHaveSingleItem().Identifier.ShouldBe("strong-gc-pressure");
    }

    private static IReadOnlyList<BoundaryCase> CreateBoundaryCases() =>
        [
            new("sustained-cpu", factor => new() { CpuInterval = _ => 70 * factor }),
            new("strong-sustained-cpu", factor => new() { CpuInterval = _ => 85 * factor }),
            new(
                "rising-cpu",
                factor =>
                    new() { CpuInterval = fraction => fraction < 0.5 ? 50 : 50 + (20 * factor) }
            ),
            new(
                "two-snapshot-cpu-rise",
                factor =>
                    new()
                    {
                        Pair = true,
                        CpuSnapshotStart = 50,
                        CpuSnapshotEnd = 50 + (20 * factor),
                    }
            ),
            new(
                "managed-heap-growth",
                factor =>
                    new()
                    {
                        Pair = true,
                        ManagedHeapStart = 160 * MiB,
                        ManagedHeapEnd = 160 * MiB + (long)(32 * MiB * factor),
                    }
            ),
            new(
                "possible-retention",
                factor =>
                    new()
                    {
                        ManagedHeapStart = 160 * MiB,
                        ManagedHeapEnd = 160 * MiB + (long)(32 * MiB * factor),
                        Gen2Delta = 1,
                        LatestGen2ManagedHeapBytes = 160 * MiB + (long)(32 * MiB * factor),
                    }
            ),
            new(
                "unexplained-process-memory-growth",
                factor =>
                    new()
                    {
                        Pair = true,
                        PrivateMemoryStart = 320 * MiB,
                        PrivateMemoryEnd = 320 * MiB + (long)(64 * MiB * factor),
                    }
            ),
            new(
                "loh-growth",
                factor =>
                    new()
                    {
                        Pair = true,
                        LohStart = 160 * MiB,
                        LohEnd = 160 * MiB + (long)(32 * MiB * factor),
                    }
            ),
            new(
                "loh-fragmentation",
                factor =>
                    new()
                    {
                        Pair = true,
                        LohFragmentationStart = 10,
                        LohFragmentationEnd = 10 + (10 * factor),
                    }
            ),
            new(
                "rising-allocation",
                factor =>
                    new()
                    {
                        AllocationInterval = fraction =>
                            fraction < 0.5 ? 10 * MiB : 10 * MiB + (10 * MiB * factor),
                    }
            ),
            new(
                "sustained-allocation",
                factor => new() { AllocationInterval = _ => 50 * MiB * factor }
            ),
            new(
                "allocation-churn",
                factor =>
                    new()
                    {
                        SpanSeconds = 10 / factor,
                        AllocationInterval = _ => 60 * MiB,
                        Gen0Delta = 5,
                    }
            ),
            new(
                "allocation-with-heap-growth",
                factor =>
                    new()
                    {
                        AllocationInterval = _ => 60 * MiB,
                        ManagedHeapStart = 160 * MiB,
                        ManagedHeapEnd = 160 * MiB + (long)(32 * MiB * factor),
                    }
            ),
            new(
                "two-snapshot-allocation-rise",
                factor =>
                    new()
                    {
                        Pair = true,
                        AllocationSnapshotStart = 10 * MiB,
                        AllocationSnapshotEnd = 10 * MiB + (10 * MiB * factor),
                    }
            ),
            new("notable-gc-pause", factor => new() { GcPausePercent = 5 * factor }),
            new("strong-gc-pressure", factor => new() { GcPausePercent = 10 * factor }),
            new(
                "frequent-full-gc",
                factor =>
                    new()
                    {
                        Gen2Delta =
                            factor < 1 ? 1
                            : factor == 1 ? 2
                            : 3,
                    }
            ),
            new("frequent-full-gc", factor => new() { SpanSeconds = 20 / factor, Gen2Delta = 2 }),
            new(
                "gc-pressure",
                factor => new() { GcPausePercent = 5 * factor, AllocationInterval = _ => 60 * MiB }
            ),
            new(
                "strong-gc-pressure",
                factor =>
                    factor < 1 ? new() { GcPausePercent = 5, Gen2Delta = 2 }
                    : factor == 1
                        ? new()
                        {
                            GcPausePercent = 5,
                            Gen2Delta = 2,
                            AllocationInterval = _ => 60 * MiB,
                        }
                    : new()
                    {
                        GcPausePercent = 5,
                        Gen2Delta = 2,
                        AllocationInterval = _ => 60 * MiB,
                        ManagedHeapStart = 160 * MiB,
                        ManagedHeapEnd = 192 * MiB,
                    }
            ),
        ];

    private static Scenario HighConfidenceScenario() =>
        new()
        {
            SnapshotCount = 10,
            SpanSeconds = 10,
            CpuInterval = fraction => fraction < 0.5 ? 80 : 105,
        };

    private static async Task<Result<ProfilingEvaluationResult>> EvaluateAsync(Scenario scenario)
    {
        var data = BuildData(scenario);
        var evaluator = CreateEvaluator(data);
        var request = scenario.Pair
            ? new(
                SessionKey,
                NodeKey,
                data.Snapshots[0].Identity.Key,
                data.Snapshots[^1].Identity.Key
            )
            : new ProfilingEvaluationRequest(SessionKey, NodeKey);
        return await evaluator.EvaluateAsync(request);
    }

    private static ProfilingEvaluator CreateEvaluator(ProfilingSessionData data)
    {
        var store = Substitute.For<IProfilingStore>();
        store
            .GetSessionDataAsync(SessionKey, Arg.Any<CancellationToken>())
            .Returns(Result<ProfilingSessionData>.Success(data));
        return new(new ProfilingOptions { Enabled = true }, store);
    }

    private static ProfilingSessionData BuildData(Scenario scenario)
    {
        var sessionId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var nodeId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var count = scenario.Pair ? 2 : scenario.SnapshotCount;
        var span = scenario.Pair ? 1 : scenario.SpanSeconds;
        var step = count > 1 ? span / (count - 1) : 0;
        var cpuDuration = 0d;
        var allocated = 0d;
        var snapshots = new List<ProfilingSnapshot>();

        for (var index = 0; index < count; index++)
        {
            var elapsedSeconds = index * step;
            var intervalFraction = count <= 2 ? 1 : Math.Clamp((index - 1d) / (count - 2), 0, 1);
            var cpu = scenario.CpuInterval(intervalFraction);
            var allocation = scenario.AllocationInterval(intervalFraction);
            if (index > 0)
            {
                cpuDuration += cpu / 100 * step;
                allocated += allocation * step;
            }

            var fraction = count == 1 ? 0 : index / (double)(count - 1);
            var timestampOffset = scenario.ReverseUtc ? -elapsedSeconds : elapsedSeconds;
            snapshots.Add(
                new()
                {
                    Identity = new(
                        Guid.Parse($"30000000-0000-0000-0000-{index + 1:000000000000}"),
                        $"s{index + 1:0000000}"
                    ),
                    SessionId = sessionId,
                    NodeId = nodeId,
                    SessionKey = SessionKey,
                    NodeKey = NodeKey,
                    TimestampUtc = DateTimeOffset.UnixEpoch.AddSeconds(timestampOffset),
                    Sequence = index + 1,
                    ScheduledElapsed = TimeSpan.FromSeconds(elapsedSeconds),
                    CaptureStartedElapsed =
                        TimeSpan.FromSeconds(elapsedSeconds) + scenario.SamplingDelay,
                    CaptureDuration = scenario.CaptureDuration,
                    SkippedCaptureCount = (long)(scenario.SkippedCaptures * fraction),
                    FailedCaptureCount = (long)(scenario.FailedCaptures * fraction),
                    CpuUsagePercent = scenario.IncludeCpu
                        ? index == 0 && scenario.CpuSnapshotStart is not null
                            ? scenario.CpuSnapshotStart
                            : index == count - 1 && scenario.CpuSnapshotEnd is not null
                                ? scenario.CpuSnapshotEnd
                                : cpu
                        : null,
                    ProcessCpuDuration = scenario.IncludeCpu
                        ? TimeSpan.FromSeconds(cpuDuration)
                        : null,
                    LogicalProcessorCount = scenario.IncludeCpu ? 1 : null,
                    ManagedHeapSizeBytes = Interpolate(
                        scenario.ManagedHeapStart,
                        scenario.ManagedHeapEnd,
                        fraction
                    ),
                    PrivateMemoryBytes = Interpolate(
                        scenario.PrivateMemoryStart,
                        scenario.PrivateMemoryEnd,
                        fraction
                    ),
                    LargeObjectHeapBytes = Interpolate(
                        scenario.LohStart,
                        scenario.LohEnd,
                        fraction
                    ),
                    HeapFragmentationPercent = 5,
                    LargeObjectHeapFragmentationPercent = Interpolate(
                        scenario.LohFragmentationStart,
                        scenario.LohFragmentationEnd,
                        fraction
                    ),
                    TotalAllocatedBytes = (long)allocated,
                    AllocationRateBytesPerSecond =
                        index == 0 && scenario.AllocationSnapshotStart is not null
                            ? scenario.AllocationSnapshotStart
                        : index == count - 1 && scenario.AllocationSnapshotEnd is not null
                            ? scenario.AllocationSnapshotEnd
                        : allocation,
                    Gen0CollectionCount = (long)Math.Floor(scenario.Gen0Delta * fraction),
                    Gen1CollectionCount = 0,
                    Gen2CollectionCount = (long)Math.Floor(scenario.Gen2Delta * fraction),
                    LatestGen2GcIndex =
                        index == count - 1 && scenario.LatestGen2ManagedHeapBytes is not null
                            ? scenario.Gen2Delta
                            : null,
                    LatestGen2ManagedHeapBytes =
                        index == count - 1 ? scenario.LatestGen2ManagedHeapBytes : null,
                    CumulativeGcPauseDuration = TimeSpan.FromSeconds(
                        scenario.GcPausePercent / 100 * elapsedSeconds
                    ),
                    GcPausePercent = scenario.GcPausePercent,
                }
            );
        }

        return new()
        {
            Session = new()
            {
                Identity = new(sessionId, SessionKey),
                State = scenario.State,
                StartedUtc = DateTimeOffset.UnixEpoch,
                EndsUtc = DateTimeOffset.UnixEpoch.AddSeconds(span),
                SamplingInterval = TimeSpan.FromSeconds(1),
                Duration = TimeSpan.FromSeconds(Math.Max(1, span)),
            },
            Participations =
            [
                new()
                {
                    SessionId = sessionId,
                    NodeId = nodeId,
                    SessionKey = SessionKey,
                    NodeKey = NodeKey,
                    Role = ProfilingNodeRole.ExpectedParticipant,
                    State = ProfilingParticipationState.Completed,
                    SuccessfulCaptureCount = count,
                    SkippedCaptureCount = scenario.SkippedCaptures,
                    FailedCaptureCount = scenario.FailedCaptures,
                },
            ],
            Nodes =
            [
                new()
                {
                    Identity = new(nodeId, NodeKey),
                    Correlation = new("broadcast-node", DateTimeOffset.UnixEpoch),
                },
            ],
            RuntimeContexts =
            [
                new()
                {
                    SessionId = sessionId,
                    NodeId = nodeId,
                    SessionKey = SessionKey,
                    NodeKey = NodeKey,
                    LogicalProcessorCount = 1,
                    DebuggerAttached = scenario.DebuggerAttached,
                },
            ],
            Snapshots = snapshots,
        };
    }

    private static long Interpolate(long start, long end, double fraction) =>
        start + (long)((end - start) * fraction);

    private static double Interpolate(double start, double end, double fraction) =>
        start + ((end - start) * fraction);

    private static ProfilingSignal Signal(
        Result<ProfilingEvaluationResult> result,
        string identifier
    ) => result.Value.Signals.Single(x => x.Identifier == identifier);

    private static void AssertSignal(
        IReadOnlyDictionary<string, ProfilingSignal> signals,
        string identifier,
        ProfilingSignalLabel label,
        string action
    )
    {
        signals.ShouldContainKey(identifier);
        signals[identifier].Label.ShouldBe(label);
        signals[identifier].SuggestedAction.ShouldBe(action);
    }

    private sealed record BoundaryCase(string SignalIdentifier, Func<double, Scenario> Create);

    private sealed record QualityCase(Scenario Scenario, string Limitation);

    private sealed record Scenario
    {
        public bool Pair { get; init; }

        public int SnapshotCount { get; init; } = 11;

        public double SpanSeconds { get; init; } = 10;

        public Func<double, double> CpuInterval { get; init; } = _ => 10;

        public double? CpuSnapshotStart { get; init; }

        public double? CpuSnapshotEnd { get; init; }

        public bool IncludeCpu { get; init; } = true;

        public long ManagedHeapStart { get; init; } = 160 * MiB;

        public long ManagedHeapEnd { get; init; } = 160 * MiB;

        public long PrivateMemoryStart { get; init; } = 320 * MiB;

        public long PrivateMemoryEnd { get; init; } = 320 * MiB;

        public long LohStart { get; init; } = 64 * MiB;

        public long LohEnd { get; init; } = 64 * MiB;

        public double LohFragmentationStart { get; init; } = 5;

        public double LohFragmentationEnd { get; init; } = 5;

        public Func<double, double> AllocationInterval { get; init; } = _ => MiB;

        public double? AllocationSnapshotStart { get; init; }

        public double? AllocationSnapshotEnd { get; init; }

        public long Gen0Delta { get; init; }

        public long Gen2Delta { get; init; }

        public long? LatestGen2ManagedHeapBytes { get; init; }

        public double GcPausePercent { get; init; }

        public long SkippedCaptures { get; init; }

        public long FailedCaptures { get; init; }

        public TimeSpan CaptureDuration { get; init; } = TimeSpan.FromMilliseconds(10);

        public TimeSpan SamplingDelay { get; init; }

        public bool DebuggerAttached { get; init; }

        public bool ReverseUtc { get; init; }

        public ProfilingSessionState State { get; init; } = ProfilingSessionState.Completed;
    }
}
