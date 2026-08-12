// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Profiling;

using Microsoft.Extensions.Time.Testing;

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
        InMemoryProfilingStore store,
        ProfilingSession session,
        ProfilingCustomMetricListener listener,
        ProfilingMeasurementService measurements
    ) : IAsyncDisposable
    {
        public InMemoryProfilingStore Store { get; } = store;

        public ProfilingSession Session { get; } = session;

        public ProfilingCustomMetricListener Listener { get; } = listener;

        public ProfilingMeasurementService Measurements { get; } = measurements;

        public static async Task<MetricHarness> CreateAsync(bool active)
        {
            var time = new FakeTimeProvider(StartUtc);
            var options = new ProfilingOptions
            {
                Enabled = true,
                SamplingInterval = TimeSpan.FromMilliseconds(500),
                Duration = TimeSpan.FromMinutes(1),
                ParticipationDeadline = TimeSpan.FromSeconds(1),
                FinalizationGracePeriod = TimeSpan.FromSeconds(1),
            };
            var store = new InMemoryProfilingStore();
            var session = (
                await store.GetOrCreateActiveSessionAsync(
                    new(
                        ProfilingSessionIdentity.Create(),
                        "metrics",
                        StartUtc,
                        options.SamplingInterval,
                        options.Duration,
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
            var nodes = new ProfilingNodeIdentityProvider(store);
            var node = (
                await nodes.GetAsync(await registry.FindAsync(identity.GetNodeIdentity()))
            ).Value;
            var activeContext = new ProfilingActiveSessionContext();
            if (active)
            {
                activeContext.Set(session, node);
            }

            var segments = new ProfilingSegmentContext();
            var control = new ExistingSessionControlService(session);
            var measurements = new ProfilingMeasurementService(
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
            var listener = new ProfilingCustomMetricListener(
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

    private sealed class ExistingSessionControlService(ProfilingSession session)
        : IProfilingControlService
    {
        public Task<Result<ProfilingStatus>> GetStatusAsync(
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public Task<Result<ProfilingControlResult>> StartAsync(
            ProfilingStartRequest request,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(Result<ProfilingControlResult>.Success(new(session, false, [])));

        public Task<Result<ProfilingControlResult>> StopAsync(
            CancellationToken cancellationToken = default
        ) => Task.FromResult(Result<ProfilingControlResult>.Success(new(session, false, [])));

        public Task<Result<ProfilingControlResult>> SnapshotAsync(
            string standaloneSessionName = null,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public Task<Result<ProfilingControlResult>> CollectGarbageAsync(
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public Task<Result<ProfilingPhaseMarker>> AddPhaseMarkerAsync(
            string name,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public Task<Result<ProfilingControlResult>> RestartAsync(
            string sessionKey,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public Task<Result<bool>> DeleteSessionAsync(
            string sessionKey,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public Task<Result<int>> DeleteUnpinnedSessionsAsync(
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public Task<Result<ProfilingClearResult>> ClearAsync(
            bool confirmed,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();
    }
}
