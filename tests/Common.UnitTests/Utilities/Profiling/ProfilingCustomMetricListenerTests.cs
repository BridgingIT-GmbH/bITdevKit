// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Profiling;

using Microsoft.Extensions.Time.Testing;

[Collection(ProcessWideStateTestCollection.Name)]
public class ProfilingCustomMetricListenerTests
{
    private static readonly DateTimeOffset StartUtc = new(2026, 8, 7, 11, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task FlushAsync_ActiveSession_StoresCounterGaugeAndDurationObservations()
    {
        // Arrange
        await using var harness = await MetricHarness.CreateAsync(active: true);
        await harness.Listener.StartAsync(CancellationToken.None);
        using var metrics = new MetricsService();

        // Act
        metrics.AddCounter("tests.counter", 2);
        metrics.RecordHistogram("tests.duration", 12.5, "ms");
        metrics.SetGauge("tests.gauge", 7);
        await harness.Listener.FlushAsync();
        var data = await harness.Store.GetSessionDataAsync(harness.Session.Identity.Key);

        // Assert
        data.Value.MetricObservations.Count.ShouldBe(3);
        data.Value.MetricObservations.Single(item => item.MetricIdentifier == "tests.counter")
            .Kind.ShouldBe(ProfilingMetricKind.Counter);
        data.Value.MetricObservations.Single(item => item.MetricIdentifier == "tests.duration")
            .Kind.ShouldBe(ProfilingMetricKind.Duration);
        var gauge = data.Value.MetricObservations.Single(item =>
            item.MetricIdentifier == "tests.gauge"
        );
        gauge.Kind.ShouldBe(ProfilingMetricKind.Gauge);
        gauge.Value.ShouldBe(7);
    }

    [Fact]
    public async Task MetricCallback_InsideMeasuredScope_InheritsAmbientSegment()
    {
        // Arrange
        await using var harness = await MetricHarness.CreateAsync(active: true);
        await harness.Listener.StartAsync(CancellationToken.None);
        using var metrics = new MetricsService();
        var scopeResult = await harness.Measurements.BeginAsync("metric scope");

        // Act
        metrics.AddCounter("tests.ambient");
        await harness.Listener.FlushAsync();
        await scopeResult.Value.DisposeAsync();
        var data = await harness.Store.GetSessionDataAsync(harness.Session.Identity.Key);

        // Assert
        var segment = data.Value.Segments.ShouldHaveSingleItem();
        data.Value.MetricObservations.ShouldHaveSingleItem().SegmentId.ShouldBe(segment.Id);
    }

    [Fact]
    public async Task MetricCallback_WithoutActiveSession_StoresNothing()
    {
        // Arrange
        await using var harness = await MetricHarness.CreateAsync(active: false);
        await harness.Listener.StartAsync(CancellationToken.None);
        using var metrics = new MetricsService();

        // Act
        metrics.AddCounter("tests.idle");
        await harness.Listener.FlushAsync();
        var data = await harness.Store.GetSessionDataAsync(harness.Session.Identity.Key);

        // Assert
        data.Value.MetricObservations.ShouldBeEmpty();
    }

    [Fact]
    public async Task MetricCallback_TaggedOrUnstableMeasurement_RejectsHighCardinalityData()
    {
        // Arrange
        await using var harness = await MetricHarness.CreateAsync(active: true);
        await harness.Listener.StartAsync(CancellationToken.None);
        using var metrics = new MetricsService();
        MetricTag[] tags = [new("request_id", Guid.NewGuid().ToString())];

        // Act
        metrics.AddCounter("tests.tagged", tags: tags);
        metrics.AddCounter("tests invalid");
        await harness.Listener.FlushAsync();
        var data = await harness.Store.GetSessionDataAsync(harness.Session.Identity.Key);

        // Assert
        data.Value.MetricObservations.ShouldBeEmpty();
    }

    [Fact]
    public async Task MetricCallback_ManyDynamicIdentifiers_AcceptsOnlyFixedBound()
    {
        // Arrange
        await using var harness = await MetricHarness.CreateAsync(active: true);
        await harness.Listener.StartAsync(CancellationToken.None);
        using var metrics = new MetricsService();

        // Act
        for (var index = 0; index < 140; index++)
        {
            metrics.AddCounter($"tests.dynamic_{index}");
        }

        await harness.Listener.FlushAsync();
        var data = await harness.Store.GetSessionDataAsync(harness.Session.Identity.Key);

        // Assert
        data.Value.MetricObservations.Count.ShouldBe(128);
        data.Value.MetricObservations.ShouldAllBe(item => item.SegmentId == null);
    }

    private sealed class MetricHarness(
        InMemoryRuntimeProfilingStore store,
        RuntimeProfilingSession session,
        RuntimeProfilingCustomMetricListener listener,
        RuntimeProfilingMeasurementService measurements
    ) : IAsyncDisposable
    {
        public InMemoryRuntimeProfilingStore Store { get; } = store;

        public RuntimeProfilingSession Session { get; } = session;

        public RuntimeProfilingCustomMetricListener Listener { get; } = listener;

        public RuntimeProfilingMeasurementService Measurements { get; } = measurements;

        public static async Task<MetricHarness> CreateAsync(bool active)
        {
            var time = new FakeTimeProvider(StartUtc);
            var options = new ProfilingOptions { Enabled = true, Runtime = new() { Enabled = true, SamplingInterval = TimeSpan.FromMilliseconds(500), Duration = TimeSpan.FromMinutes(1), ParticipationDeadline = TimeSpan.FromSeconds(1), FinalizationGracePeriod = TimeSpan.FromSeconds(1) } };
            var store = new InMemoryRuntimeProfilingStore();
            var session = (
                await store.GetOrCreateActiveSessionAsync(
                    new(
                        ProfilingIdentityFactory.CreateRuntimeSession(),
                        "metrics",
                        StartUtc,
                        options.Runtime.SamplingInterval,
                        options.Runtime.Duration,
                        []
                    )
                )
            )
                .Value
                .Session;
            var registry = new InMemoryBroadcastRegistryStore(new BroadcastingOptions(), time);
            var identity = new TestBroadcastNodeIdentityProvider();
            await registry.UpsertAsync(
                new(
                    identity.GetNodeIdentity(),
                    null,
                    [BroadcastingOptions.DefaultScope],
                    StartUtc.Subtract(TimeSpan.FromMinutes(1)),
                    StartUtc,
                    null
                )
            );
            var nodes = new RuntimeProfilingNodeRegistrationAdapter(store, new ProfilingNodeIdentityProvider());
            var node = (
                await nodes.RegisterLocalAsync(await registry.FindAsync(identity.GetNodeIdentity()))
            ).Value;
            var activeContext = new RuntimeProfilingActiveSessionContext();
            if (active)
            {
                activeContext.Set(session, node);
            }

            var segments = new RuntimeProfilingSegmentContext();
            var control = new ExistingSessionControlService(session);
            var measurements = new RuntimeProfilingMeasurementService(
                options,
                control,
                store,
                nodes,
                registry,
                identity,
                activeContext,
                segments,
                time
            );
            var listener = new RuntimeProfilingCustomMetricListener(
                store,
                activeContext,
                segments,
                options,
                time
            );
            return new(store, session, listener, measurements);
        }

        public async ValueTask DisposeAsync()
        {
            await this.Listener.StopAsync(CancellationToken.None);
            this.Listener.Dispose();
        }
    }

    private sealed class TestBroadcastNodeIdentityProvider : IBroadcastNodeIdentityProvider
    {
        public string GetNodeIdentity() => "profiling-metric-node";
    }

    private sealed class ExistingSessionControlService(RuntimeProfilingSession session)
        : IRuntimeProfilingControlService
    {
        public Task<IResult<RuntimeProfilingStatus>> GetStatusAsync(
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public Task<IResult<RuntimeProfilingControlResult>> StartAsync(
            RuntimeProfilingStartRequest request,
            CancellationToken cancellationToken = default
        ) => Task.FromResult<IResult<RuntimeProfilingControlResult>>(Result<RuntimeProfilingControlResult>.Success(new(session, false, [])));

        public Task<IResult<RuntimeProfilingControlResult>> StopAsync(
            CancellationToken cancellationToken = default
        ) => Task.FromResult<IResult<RuntimeProfilingControlResult>>(Result<RuntimeProfilingControlResult>.Success(new(session, false, [])));

        public Task<IResult<RuntimeProfilingControlResult>> SnapshotAsync(
            string standaloneSessionName = null,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public Task<IResult<RuntimeProfilingControlResult>> CollectGarbageAsync(
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public Task<IResult<ProfilingMarker>> AddMarkerAsync(
            string name,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public Task<IResult<RuntimeProfilingControlResult>> RestartAsync(
            string sessionKey,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public Task<IResult<bool>> DeleteSessionAsync(
            string sessionKey,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public Task<IResult<int>> DeleteUnpinnedSessionsAsync(
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public Task<IResult<ProfilingClearResult>> ClearAsync(
            bool confirmed,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();
    }
}
