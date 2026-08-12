// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Profiling;

using static ProfilingRuntimeTestData;

public class ProfilingNodeIdentityProviderTests
{
    [Fact]
    public async Task GetAsync_MissingBroadcastIdentity_ReturnsValidationFailure()
    {
        // Arrange
        var provider = new ProfilingNodeIdentityProvider(new InMemoryProfilingStore());
        var registration = CreateRegistration(
            " ",
            new DateTimeOffset(2026, 8, 7, 10, 0, 0, TimeSpan.Zero)
        );

        // Act
        var result = await provider.GetAsync(registration);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(error => error is ProfilingValidationError);
    }

    [Fact]
    public async Task GetAsync_SameBroadcastProcess_ReturnsStableProcessLifetimeNode()
    {
        // Arrange
        var provider = new ProfilingNodeIdentityProvider(new InMemoryProfilingStore());
        var processStartedUtc = new DateTimeOffset(2026, 8, 7, 10, 0, 0, TimeSpan.Zero);
        var registration = CreateRegistration("broadcast-node", processStartedUtc);

        // Act
        var first = await provider.GetAsync(registration);
        var repeated = await provider.GetAsync(registration);
        var restarted = await provider.GetAsync(
            CreateRegistration("broadcast-node", processStartedUtc.AddMinutes(1))
        );

        // Assert
        first.IsSuccess.ShouldBeTrue();
        repeated.IsSuccess.ShouldBeTrue();
        first.Value.ShouldBe(repeated.Value);
        first.Value.Identity.Id.ShouldNotBe(Guid.Empty);
        first.Value.Identity.Key.Length.ShouldBe(8);
        first.Value.HostName.ShouldBe(Environment.MachineName);
        first.Value.ProcessId.ShouldBe(Environment.ProcessId);
        restarted.Value.Identity.ShouldNotBe(first.Value.Identity);
    }

    private static BroadcastNodeRegistration CreateRegistration(
        string nodeIdentity,
        DateTimeOffset processStartedUtc
    ) =>
        new()
        {
            NodeIdentity = nodeIdentity,
            ProcessStartedUtc = processStartedUtc,
            RegisteredUtc = processStartedUtc,
        };
}

public class ProfilingRuntimeContextFactoryTests
{
    [Fact]
    public void Create_RuntimeValues_MapsOnlyApprovedNonSensitiveContext()
    {
        // Arrange
        var processStartedUtc = new DateTimeOffset(2026, 8, 7, 10, 0, 0, TimeSpan.Zero);
        var session = CreateSession(processStartedUtc);
        var node = CreateNode(processStartedUtc);
        var source = new StubRuntimeContextSource(
            new ProfilingRuntimeContextValues(
                "sample-app",
                "1.2.3",
                ".NET test runtime",
                "10.0.0",
                "Test OS",
                "X64",
                "Arm64",
                true,
                12,
                processStartedUtc.AddDays(-1),
                true
            )
        );
        var sut = new ProfilingRuntimeContextFactory(source);

        // Act
        var result = sut.Create(session, node);

        // Assert
        result.SessionId.ShouldBe(session.Identity.Id);
        result.NodeId.ShouldBe(node.Identity.Id);
        result.ApplicationName.ShouldBe("sample-app");
        result.ApplicationVersion.ShouldBe("1.2.3");
        result.RuntimeDescription.ShouldBe(".NET test runtime");
        result.RuntimeVersion.ShouldBe("10.0.0");
        result.OperatingSystemDescription.ShouldBe("Test OS");
        result.OperatingSystemArchitecture.ShouldBe("X64");
        result.ProcessArchitecture.ShouldBe("Arm64");
        result.ServerGarbageCollection.ShouldBe(true);
        result.LogicalProcessorCount.ShouldBe(12);
        result.ProcessStartedUtc.ShouldBe(processStartedUtc);
        result.DebuggerAttached.ShouldBeTrue();
        typeof(ProfilingRuntimeContext)
            .GetProperties()
            .Select(property => property.Name)
            .ShouldNotContain(name =>
                name.Contains("Command", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Environment", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Path", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Credential", StringComparison.OrdinalIgnoreCase)
            );
    }

    [Fact]
    public void Create_SystemRuntime_ReturnsPortableRuntimeAndArchitectureContext()
    {
        // Arrange
        var processStartedUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
        var sut = new ProfilingRuntimeContextFactory();

        // Act
        var result = sut.Create(CreateSession(processStartedUtc), CreateNode(processStartedUtc));

        // Assert
        result.RuntimeDescription.ShouldNotBeNullOrWhiteSpace();
        result.RuntimeVersion.ShouldNotBeNullOrWhiteSpace();
        result.OperatingSystemDescription.ShouldNotBeNullOrWhiteSpace();
        result.OperatingSystemArchitecture.ShouldNotBeNullOrWhiteSpace();
        result.ProcessArchitecture.ShouldNotBeNullOrWhiteSpace();
        result.LogicalProcessorCount.ShouldNotBeNull();
        result.LogicalProcessorCount.Value.ShouldBeGreaterThan(0);
        result.ProcessStartedUtc.ShouldBe(processStartedUtc);
    }

    private sealed class StubRuntimeContextSource(ProfilingRuntimeContextValues values)
        : IProfilingRuntimeContextSource
    {
        public ProfilingRuntimeContextValues Capture() => values;
    }
}

public class ProfilingSnapshotProbeTests
{
    [Fact]
    public async Task CaptureAsync_InvalidRequest_ReturnsValidationFailureWithoutSampling()
    {
        // Arrange
        var source = new SequenceRuntimeSnapshotSource([]);
        var sut = new ProfilingSnapshotProbe(
            new ManualProfilingTimeProvider(DateTimeOffset.UtcNow),
            source
        );

        // Act
        var result = await sut.CaptureAsync(null);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(error => error is ProfilingValidationError);
        source.CaptureCount.ShouldBe(0);
    }

    [Fact]
    public async Task CaptureAsync_ValidRequest_PreservesUtcAndMonotonicTiming()
    {
        // Arrange
        var utcNow = new DateTimeOffset(2026, 8, 7, 12, 0, 0, TimeSpan.FromHours(2));
        var timeProvider = new ManualProfilingTimeProvider(utcNow);
        var source = new SequenceRuntimeSnapshotSource(
            [new ProfilingRuntimeSample { LogicalProcessorCount = 4 }],
            () => timeProvider.Advance(TimeSpan.FromMilliseconds(25))
        );
        var request = CreateRequest(utcNow.ToUniversalTime());
        var sut = new ProfilingSnapshotProbe(timeProvider, source);

        // Act
        var result = await sut.CaptureAsync(request);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.TimestampUtc.ShouldBe(utcNow.ToUniversalTime());
        result.Value.TimestampUtc.Offset.ShouldBe(TimeSpan.Zero);
        result.Value.ScheduledElapsed.ShouldBe(request.ScheduledElapsed);
        result.Value.CaptureStartedElapsed.ShouldBe(request.CaptureStartedElapsed);
        result.Value.CaptureDuration.ShouldBe(TimeSpan.FromMilliseconds(25));
        result.Value.Sequence.ShouldBe(request.Sequence);
        result.Value.SkippedCaptureCount.ShouldBe(request.SkippedCaptureCount);
        result.Value.FailedCaptureCount.ShouldBe(request.FailedCaptureCount);
    }

    [Fact]
    public async Task CaptureAsync_ConsecutiveSamples_ComputesCpuAndAllocationRates()
    {
        // Arrange
        var startedUtc = new DateTimeOffset(2026, 8, 7, 10, 0, 0, TimeSpan.Zero);
        var timeProvider = new ManualProfilingTimeProvider(startedUtc);
        var source = new SequenceRuntimeSnapshotSource([
            new ProfilingRuntimeSample
            {
                ProcessCpuDuration = TimeSpan.FromSeconds(10),
                LogicalProcessorCount = 2,
                TotalAllocatedBytes = 100,
                TotalPhysicalMemoryBytes = 1_000,
                AvailablePhysicalMemoryBytes = 400,
            },
            new ProfilingRuntimeSample
            {
                ProcessCpuDuration = TimeSpan.FromSeconds(11),
                LogicalProcessorCount = 2,
                TotalAllocatedBytes = 300,
            },
        ]);
        var request = CreateRequest(startedUtc);
        var sut = new ProfilingSnapshotProbe(timeProvider, source);

        // Act
        var first = await sut.CaptureAsync(request);
        timeProvider.Advance(TimeSpan.FromSeconds(2));
        var second = await sut.CaptureAsync(request with { Sequence = 2 });

        // Assert
        first.Value.ProcessCpuDuration.ShouldBe(TimeSpan.FromSeconds(10));
        first.Value.TotalAllocatedBytes.ShouldBe(100);
        first.Value.TotalPhysicalMemoryBytes.ShouldBe(1_000);
        first.Value.AvailablePhysicalMemoryBytes.ShouldBe(400);
        first.Value.UsedPhysicalMemoryBytes.ShouldBe(600);
        first.Value.CpuUsagePercent.ShouldBeNull();
        first.Value.AllocationRateBytesPerSecond.ShouldBeNull();
        second.Value.ProcessCpuDuration.ShouldBe(TimeSpan.FromSeconds(11));
        second.Value.TotalAllocatedBytes.ShouldBe(300);
        second.Value.CpuUsagePercent.ShouldBe(25d);
        second.Value.AllocationRateBytesPerSecond.ShouldBe(100d);
    }

    [Fact]
    public async Task CaptureAsync_DirectGcEvidence_MapsLatestAndLatestGen2Independently()
    {
        // Arrange
        var startedUtc = new DateTimeOffset(2026, 8, 7, 10, 0, 0, TimeSpan.Zero);
        var timeProvider = new ManualProfilingTimeProvider(startedUtc);
        var source = new SequenceRuntimeSnapshotSource([
            new ProfilingRuntimeSample
            {
                LatestGc = CreateGcObservation(10, 0, 1_000, 100, false, true, 10),
                LatestGen2Gc = CreateGcObservation(8, 2, 8_000, 800, true, false, 20),
                TotalGcPauseDuration = TimeSpan.FromSeconds(1),
            },
            new ProfilingRuntimeSample
            {
                LatestGc = CreateGcObservation(11, 2, 11_000, 1_100, true, false, 100),
                LatestGen2Gc = CreateGcObservation(11, 2, 11_000, 1_100, true, false, 100),
                TotalGcPauseDuration = TimeSpan.FromMilliseconds(1_100),
            },
        ]);
        var request = CreateRequest(startedUtc);
        var sut = new ProfilingSnapshotProbe(timeProvider, source);

        // Act
        var first = await sut.CaptureAsync(request);
        timeProvider.Advance(TimeSpan.FromSeconds(1));
        var second = await sut.CaptureAsync(request with { Sequence = 2 });

        // Assert
        first.Value.LatestGcIndex.ShouldBe(10);
        first.Value.LatestGcGeneration.ShouldBe(0);
        first.Value.LatestGen2GcIndex.ShouldBe(8);
        first.Value.LatestGen2ManagedHeapBytes.ShouldBe(8_000);
        first.Value.LatestGen2LargeObjectHeapBytes.ShouldBe(800);
        first.Value.LatestGen2GcCompacting.ShouldBe(true);
        first.Value.LatestGen2GcConcurrent.ShouldBe(false);
        first.Value.CumulativeGcPauseDuration.ShouldBe(TimeSpan.Zero);
        first.Value.GcPausePercent.ShouldBeNull();
        second.Value.LatestGcIndex.ShouldBe(11);
        second.Value.LatestGen2GcIndex.ShouldBe(11);
        second.Value.CumulativeGcPauseDuration.ShouldBe(TimeSpan.FromMilliseconds(100));
        second.Value.GcPausePercent.ShouldBe(10d);
    }

    [Fact]
    public async Task CaptureAsync_UnavailableRuntimeMetrics_RemainsSuccessfulWithNullEvidence()
    {
        // Arrange
        var startedUtc = new DateTimeOffset(2026, 8, 7, 10, 0, 0, TimeSpan.Zero);
        var sut = new ProfilingSnapshotProbe(
            new ManualProfilingTimeProvider(startedUtc),
            new SequenceRuntimeSnapshotSource([ProfilingRuntimeSample.Empty])
        );

        // Act
        var result = await sut.CaptureAsync(CreateRequest(startedUtc));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ProcessCpuDuration.ShouldBeNull();
        result.Value.CpuUsagePercent.ShouldBeNull();
        result.Value.WorkingSetBytes.ShouldBeNull();
        result.Value.TotalPhysicalMemoryBytes.ShouldBeNull();
        result.Value.TotalAllocatedBytes.ShouldBeNull();
        result.Value.LatestGcIndex.ShouldBeNull();
        result.Value.CumulativeGcPauseDuration.ShouldBeNull();
        result.Value.ThreadPoolThreadCount.ShouldBeNull();
        result.Value.TotalUsedSocketCount.ShouldBeNull();
    }

    [Fact]
    public async Task CaptureAsync_UnsupportedRuntimeSource_DoesNotEscapeException()
    {
        // Arrange
        var startedUtc = new DateTimeOffset(2026, 8, 7, 10, 0, 0, TimeSpan.Zero);
        var sut = new ProfilingSnapshotProbe(
            new ManualProfilingTimeProvider(startedUtc),
            new UnsupportedRuntimeSnapshotSource()
        );

        // Act
        var result = await sut.CaptureAsync(CreateRequest(startedUtc));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ManagedMemoryBytes.ShouldBeNull();
        result.Value.LatestGcIndex.ShouldBeNull();
    }

    [Fact]
    public async Task CaptureAsync_SystemRuntime_CapturesCoreRawMetricsWithoutMutation()
    {
        // Arrange
        var startedUtc = DateTimeOffset.UtcNow;
        var session = CreateSession(startedUtc);
        var node = CreateNode(startedUtc);
        var request = CreateRequest(startedUtc) with { Session = session, Node = node };
        var originalSession = session with { };
        var originalNode = node with { };
        var sut = new ProfilingSnapshotProbe();

        // Act
        var result = await sut.CaptureAsync(request);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ProcessCpuDuration.ShouldNotBeNull();
        result.Value.LogicalProcessorCount.ShouldNotBeNull();
        result.Value.LogicalProcessorCount.Value.ShouldBeGreaterThan(0);
        result.Value.WorkingSetBytes.ShouldNotBeNull();
        result.Value.ManagedMemoryBytes.ShouldNotBeNull();
        result.Value.TotalAllocatedBytes.ShouldNotBeNull();
        result.Value.Gen0CollectionCount.ShouldNotBeNull();
        result.Value.ThreadPoolThreadCount.ShouldNotBeNull();
        session.ShouldBe(originalSession);
        node.ShouldBe(originalNode);
    }

    private static ProfilingGcObservation CreateGcObservation(
        long index,
        int generation,
        long heapBytes,
        long largeObjectHeapBytes,
        bool compacting,
        bool concurrent,
        int pauseMilliseconds
    ) =>
        new(
            index,
            generation,
            heapBytes,
            largeObjectHeapBytes,
            compacting,
            concurrent,
            TimeSpan.FromMilliseconds(pauseMilliseconds)
        );

    private sealed class ManualProfilingTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset utcNow = utcNow;
        private long timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override DateTimeOffset GetUtcNow() => this.utcNow;

        public override long GetTimestamp() => this.timestamp;

        public void Advance(TimeSpan amount)
        {
            this.utcNow = this.utcNow.Add(amount);
            this.timestamp += amount.Ticks;
        }
    }

    private sealed class SequenceRuntimeSnapshotSource(
        IReadOnlyList<ProfilingRuntimeSample> samples,
        Action onCapture = null
    ) : IProfilingRuntimeSnapshotSource
    {
        private readonly Queue<ProfilingRuntimeSample> samples = new(samples);

        public int CaptureCount { get; private set; }

        public ProfilingRuntimeSample Capture()
        {
            this.CaptureCount++;
            onCapture?.Invoke();
            return this.samples.Dequeue();
        }
    }

    private sealed class UnsupportedRuntimeSnapshotSource : IProfilingRuntimeSnapshotSource
    {
        public ProfilingRuntimeSample Capture() =>
            throw new PlatformNotSupportedException("Unavailable in this test environment.");
    }
}

public static class ProfilingRuntimeTestData
{
    public static ProfilingSession CreateSession(DateTimeOffset startedUtc) =>
        new()
        {
            Identity = ProfilingSessionIdentity.Create(),
            State = ProfilingSessionState.Running,
            StartedUtc = startedUtc,
            EndsUtc = startedUtc.AddSeconds(30),
            SamplingInterval = TimeSpan.FromSeconds(1),
            Duration = TimeSpan.FromSeconds(30),
        };

    public static ProfilingNode CreateNode(DateTimeOffset processStartedUtc)
    {
        var correlation = new ProfilingNodeCorrelation("broadcast-node", processStartedUtc);
        return new ProfilingNode
        {
            Identity = ProfilingNodeIdentity.Create(),
            Correlation = correlation,
            HostName = "test-host",
            ProcessId = 42,
        };
    }

    public static ProfilingCaptureRequest CreateRequest(DateTimeOffset startedUtc) =>
        new(
            CreateSession(startedUtc),
            CreateNode(startedUtc),
            1,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1).Add(TimeSpan.FromMilliseconds(5)),
            2,
            1
        );
}
