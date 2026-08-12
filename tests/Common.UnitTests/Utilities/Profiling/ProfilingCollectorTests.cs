// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Profiling;

using Microsoft.Extensions.Time.Testing;

public class ProfilingCollectorTests
{
    private static readonly DateTimeOffset StartUtc = new(2026, 8, 7, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task StartAndStopAsync_DuplicateCommands_AreIdempotent()
    {
        // Arrange
        var harness = await CollectorHarness.CreateAsync();
        var session = await harness.CreateSessionAsync();

        // Act
        var firstStart = await harness.Collector.StartAsync(session);
        var repeatedStart = await harness.Collector.StartAsync(session);
        var firstStop = await harness.Collector.StopAsync(session.Identity.Id);
        var repeatedStop = await harness.Collector.StopAsync(session.Identity.Id);

        // Assert
        firstStart.IsSuccess.ShouldBeTrue();
        repeatedStart.IsSuccess.ShouldBeTrue();
        firstStop.IsSuccess.ShouldBeTrue();
        repeatedStop.IsSuccess.ShouldBeTrue();
        var data = (await harness.Store.GetSessionDataAsync(session.Identity.Key)).Value;
        data.Participations.ShouldHaveSingleItem()
            .State.ShouldBe(ProfilingParticipationState.Stopped);
        harness.ActiveSession.Current.ShouldBeNull();
    }

    [Fact]
    public async Task StopForHostAsync_WithActiveSessionContext_ClearsCapturedStateWithoutDereferencingCompletionState()
    {
        // Arrange
        var harness = await CollectorHarness.CreateAsync();
        var session = await harness.CreateSessionAsync();
        await harness.Collector.StartAsync(session);

        // Act
        await harness.Collector.StopForHostAsync(CancellationToken.None);

        // Assert
        harness.ActiveSession.Current.ShouldBeNull();
        var data = (await harness.Store.GetSessionDataAsync(session.Identity.Key)).Value;
        var participation = data.Participations.ShouldHaveSingleItem();
        participation.State.ShouldBe(ProfilingParticipationState.Failed);
        participation.Failure.ShouldBe("Host stopped before profiling collection completed.");
    }

    [Fact]
    public async Task ScheduledCapture_SlowProbe_SkipsAbsoluteOpportunitiesWithoutOverlap()
    {
        // Arrange
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var probe = new RecordingProbe(
            async (request, cancellationToken) =>
            {
                await release.Task.ConfigureAwait(false);
                return SuccessSnapshot(request, StartUtc.AddMilliseconds(1200));
            }
        );
        var harness = await CollectorHarness.CreateAsync(probe);
        var session = await harness.CreateSessionAsync(duration: TimeSpan.FromSeconds(3));

        // Act
        await harness.Collector.StartAsync(session);
        await probe.WaitForCallsAsync(1);
        harness.Time.Advance(TimeSpan.FromMilliseconds(1200));
        release.SetResult();
        await WaitUntilAsync(async () =>
            (await harness.Store.GetSessionDataAsync(session.Identity.Key)).Value.Snapshots.Count
            == 1
        );
        await harness.Collector.StopAsync(session.Identity.Id);

        // Assert
        var data = (await harness.Store.GetSessionDataAsync(session.Identity.Key)).Value;
        var participation = data.Participations.ShouldHaveSingleItem();
        participation.SuccessfulCaptureCount.ShouldBe(1);
        participation.SkippedCaptureCount.ShouldBe(2);
        participation.FailedCaptureCount.ShouldBe(0);
        data.Snapshots.ShouldHaveSingleItem().Sequence.ShouldBe(1);
        probe.MaximumConcurrentCalls.ShouldBe(1);
    }

    [Fact]
    public async Task CaptureAsync_WhileScheduledCaptureIsActive_RemainsSingleFlight()
    {
        // Arrange
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var probe = new RecordingProbe(
            async (request, cancellationToken) =>
            {
                if (request.Sequence == 1)
                {
                    await release.Task.ConfigureAwait(false);
                }

                return SuccessSnapshot(request, StartUtc.AddMilliseconds(request.Sequence * 100));
            }
        );
        var harness = await CollectorHarness.CreateAsync(probe);
        var session = await harness.CreateSessionAsync(duration: TimeSpan.FromSeconds(3));
        await harness.Collector.StartAsync(session);
        await probe.WaitForCallsAsync(1);

        // Act
        var manualCapture = harness.Collector.CaptureAsync(
            session,
            ProfilingNodeRole.ExpectedParticipant
        );
        harness.Time.Advance(TimeSpan.FromMilliseconds(600));
        release.SetResult();
        var manualResult = await manualCapture;
        await harness.Collector.StopAsync(session.Identity.Id);

        // Assert
        manualResult.IsSuccess.ShouldBeTrue();
        probe.MaximumConcurrentCalls.ShouldBe(1);
        var data = (await harness.Store.GetSessionDataAsync(session.Identity.Key)).Value;
        data.Snapshots.Select(snapshot => snapshot.Sequence).ShouldBe([1, 2]);
        data.Participations.ShouldHaveSingleItem().SkippedCaptureCount.ShouldBe(1);
    }

    [Fact]
    public async Task CaptureAsync_NewAdHocState_PrimesRatesAndStoresOnlyMeasuredSnapshot()
    {
        // Arrange
        var callCount = 0;
        var probe = new RecordingProbe(
            (request, cancellationToken) =>
            {
                var call = Interlocked.Increment(ref callCount);
                return Task.FromResult(
                    SuccessSnapshot(
                        request,
                        StartUtc.AddMilliseconds(call * 500),
                        call == 2 ? 32d : null,
                        call == 2 ? 4_096d : null
                    )
                );
            }
        );
        var harness = await CollectorHarness.CreateAsync(probe);
        var session = await harness.CreateSessionAsync();

        // Act
        var capture = harness.Collector.CaptureAsync(
            session,
            ProfilingNodeRole.ExpectedParticipant
        );
        await probe.WaitForCallsAsync(1);
        await AdvanceAndDrainAsync(
            harness.Time,
            ProfilingOptions.MinimumSamplingInterval
        );
        var result = await capture;
        var data = (await harness.Store.GetSessionDataAsync(session.Identity.Key)).Value;

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.CpuUsagePercent.ShouldBe(32d);
        result.Value.AllocationRateBytesPerSecond.ShouldBe(4_096d);
        Volatile.Read(ref callCount).ShouldBe(2);
        var stored = data.Snapshots.ShouldHaveSingleItem();
        stored.Sequence.ShouldBe(1);
        stored.CpuUsagePercent.ShouldBe(32d);
        stored.AllocationRateBytesPerSecond.ShouldBe(4_096d);
    }

    [Fact]
    public async Task StopAsync_CaptureAlreadyInFlight_PreservesValidLateWriteAndFinalTotals()
    {
        // Arrange
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        FakeTimeProvider time = null;
        var probe = new RecordingProbe(
            async (request, cancellationToken) =>
            {
                await release.Task.ConfigureAwait(false);
                return SuccessSnapshot(request, time.GetUtcNow());
            }
        );
        var harness = await CollectorHarness.CreateAsync(probe);
        time = harness.Time;
        var session = await harness.CreateSessionAsync(duration: TimeSpan.FromSeconds(3));
        await harness.Collector.StartAsync(session);
        await probe.WaitForCallsAsync(1);

        // Act
        var stop = harness.Collector.StopAsync(session.Identity.Id);
        harness.Time.Advance(TimeSpan.FromMilliseconds(100));
        release.SetResult();
        var stopResult = await stop;

        // Assert
        stopResult.IsSuccess.ShouldBeTrue();
        var data = (await harness.Store.GetSessionDataAsync(session.Identity.Key)).Value;
        data.Snapshots.ShouldHaveSingleItem().Sequence.ShouldBe(1);
        var participation = data.Participations.ShouldHaveSingleItem();
        participation.State.ShouldBe(ProfilingParticipationState.Stopped);
        participation.SuccessfulCaptureCount.ShouldBe(1);
    }

    [Fact]
    public async Task ScheduledCapture_FailedThenSuccessful_PreservesTotalsAndSequence()
    {
        // Arrange
        var probe = new RecordingProbe(
            (request, cancellationToken) =>
                Task.FromResult(
                    request.Sequence == 1 && request.FailedCaptureCount == 0
                        ? Result<ProfilingSnapshot>
                            .Failure()
                            .WithError(new ProfilingUnavailableError("Probe unavailable."))
                        : SuccessSnapshot(request, StartUtc.AddMilliseconds(500))
                )
        );
        var harness = await CollectorHarness.CreateAsync(probe);
        var session = await harness.CreateSessionAsync(duration: TimeSpan.FromSeconds(2));
        await harness.Collector.StartAsync(session);
        await probe.WaitForCallsAsync(1);

        // Act
        await AdvanceAndDrainAsync(harness.Time, TimeSpan.FromMilliseconds(500));
        await probe.WaitForCallsAsync(2);
        await harness.Collector.StopAsync(session.Identity.Id);

        // Assert
        var data = (await harness.Store.GetSessionDataAsync(session.Identity.Key)).Value;
        var participation = data.Participations.ShouldHaveSingleItem();
        participation.SuccessfulCaptureCount.ShouldBe(1);
        participation.FailedCaptureCount.ShouldBe(1);
        var snapshot = data.Snapshots.ShouldHaveSingleItem();
        snapshot.Sequence.ShouldBe(1);
        snapshot.FailedCaptureCount.ShouldBe(1);
    }

    [Fact]
    public async Task StartAsync_NewerSession_ReplacesOlderLocalCollector()
    {
        // Arrange
        var harness = await CollectorHarness.CreateAsync();
        var older = await harness.CreateSessionAsync(duration: TimeSpan.FromSeconds(5));
        await harness.Collector.StartAsync(older);
        await harness.Store.TryTransitionSessionAsync(
            older.Identity.Id,
            [ProfilingSessionState.Running],
            ProfilingSessionState.Stopped,
            harness.Time.GetUtcNow()
        );
        await AdvanceAndDrainAsync(harness.Time, TimeSpan.FromMilliseconds(100));
        var newer = await harness.CreateSessionAsync(duration: TimeSpan.FromSeconds(5));

        // Act
        var result = await harness.Collector.StartAsync(newer);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var olderData = (await harness.Store.GetSessionDataAsync(older.Identity.Key)).Value;
        olderData
            .Participations.ShouldHaveSingleItem()
            .State.ShouldBe(ProfilingParticipationState.Stopped);
        var newerData = (await harness.Store.GetSessionDataAsync(newer.Identity.Key)).Value;
        newerData
            .Participations.ShouldHaveSingleItem()
            .State.ShouldBe(ProfilingParticipationState.Collecting);
        await harness.Collector.StopAsync(newer.Identity.Id);
    }

    [Fact]
    public async Task StartAsync_UnstoredNewerSession_DoesNotReplaceCurrentCollector()
    {
        // Arrange
        var harness = await CollectorHarness.CreateAsync();
        var current = await harness.CreateSessionAsync(duration: TimeSpan.FromSeconds(5));
        await harness.Collector.StartAsync(current);
        var unstored = current with
        {
            Identity = ProfilingSessionIdentity.Create(),
            StartedUtc = current.StartedUtc.AddMilliseconds(100),
            EndsUtc = current.EndsUtc.AddMilliseconds(100),
        };

        // Act
        var result = await harness.Collector.StartAsync(unstored);

        // Assert
        result.IsFailure.ShouldBeTrue();
        var data = (await harness.Store.GetSessionDataAsync(current.Identity.Key)).Value;
        data.Participations.ShouldHaveSingleItem()
            .State.ShouldBe(ProfilingParticipationState.Collecting);
        await harness.Collector.StopAsync(current.Identity.Id);
    }

    [Fact]
    public async Task ScheduledCollection_WithoutExplicitStop_ContinuesToOriginalEndAndFinalizes()
    {
        // Arrange
        var options = CreateOptions(duration: TimeSpan.FromSeconds(1));
        var harness = await CollectorHarness.CreateAsync(options: options);
        var session = await harness.CreateSessionAsync(duration: options.Duration);
        await harness.Collector.StartAsync(session);

        // Act
        await AdvanceAndDrainAsync(harness.Time, TimeSpan.FromMilliseconds(500));
        await AdvanceAndDrainAsync(harness.Time, TimeSpan.FromMilliseconds(500));
        await AdvanceAndDrainAsync(harness.Time, options.FinalizationGracePeriod);
        await WaitUntilAsync(async () =>
            (await harness.Store.FindSessionAsync(session.Identity.Key)).Value.State
            == ProfilingSessionState.Completed
        );

        // Assert
        var data = (await harness.Store.GetSessionDataAsync(session.Identity.Key)).Value;
        data.Session.State.ShouldBe(ProfilingSessionState.Completed);
        data.Snapshots.Count.ShouldBe(2);
        data.Participations.ShouldHaveSingleItem()
            .State.ShouldBe(ProfilingParticipationState.Completed);
    }

    [Fact]
    public async Task FinalizeAsync_CompetingCallers_CompleteSessionOnce()
    {
        // Arrange
        var options = CreateOptions(duration: TimeSpan.FromSeconds(1));
        var time = new FakeTimeProvider(StartUtc);
        var store = new InMemoryProfilingStore();
        var session = await CreateSessionAsync(store, time, options);
        time.Advance(session.Duration + options.FinalizationGracePeriod);
        var finalizer = new ProfilingSessionFinalizer(store, options, time);

        // Act
        var results = await Task.WhenAll(
            finalizer.FinalizeAsync(session),
            finalizer.FinalizeAsync(session)
        );

        // Assert
        results.ShouldAllBe(result => result.IsSuccess);
        results.ShouldAllBe(result => result.Value.State == ProfilingSessionState.Completed);
        (await store.ListSessionsAsync()).Value.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task ReconcileAsync_OverdueRunningSession_FinalizesOnSingleStartupPass()
    {
        // Arrange
        var options = CreateOptions(duration: TimeSpan.FromSeconds(1));
        var time = new FakeTimeProvider(StartUtc);
        var store = new InMemoryProfilingStore();
        var session = await CreateSessionAsync(store, time, options);
        time.Advance(session.Duration + options.FinalizationGracePeriod);
        var finalizer = new ProfilingSessionFinalizer(store, options, time);
        var reconciler = new ProfilingStartupReconciler(store, options, time, finalizer);

        // Act
        var result = await reconciler.ReconcileAsync();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(1);
        (await store.FindSessionAsync(session.Identity.Key)).Value.State.ShouldBe(
            ProfilingSessionState.Completed
        );
    }

    [Fact]
    public async Task HostedService_IdleRuntime_PerformsOnlyOneStartupStoreInspection()
    {
        // Arrange
        var options = CreateOptions();
        var time = new FakeTimeProvider(StartUtc);
        var store = Substitute.For<IProfilingStore>();
        store
            .ListSessionsAsync(Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(
                    Result<IReadOnlyList<ProfilingSession>>.Success(Array.Empty<ProfilingSession>())
                )
            );
        var finalizer = new ProfilingSessionFinalizer(store, options, time);
        var reconciler = new ProfilingStartupReconciler(store, options, time, finalizer);
        var collector = new ProfilingCollector(
            store,
            Substitute.For<IProfilingSnapshotProbe>(),
            Substitute.For<IProfilingRuntimeContextFactory>(),
            Substitute.For<IProfilingNodeIdentityProvider>(),
            finalizer,
            options,
            time
        );
        var hostedService = new ProfilingCollectorHostedService(collector, reconciler);

        // Act
        await hostedService.StartAsync(CancellationToken.None);
        time.Advance(TimeSpan.FromHours(1));
        await Task.Yield();
        await hostedService.StopAsync(CancellationToken.None);

        // Assert
        await store.Received(1).ListSessionsAsync(Arg.Any<CancellationToken>());
        await store.DidNotReceive().GetActiveSessionAsync(Arg.Any<CancellationToken>());
    }

    private static ProfilingOptions CreateOptions(
        TimeSpan? duration = null,
        TimeSpan? samplingInterval = null
    ) =>
        new()
        {
            Enabled = true,
            Duration = duration ?? TimeSpan.FromSeconds(3),
            SamplingInterval = samplingInterval ?? TimeSpan.FromMilliseconds(500),
            FinalizationGracePeriod = TimeSpan.FromSeconds(1),
        };

    private static async Task<ProfilingSession> CreateSessionAsync(
        IProfilingStore store,
        TimeProvider time,
        ProfilingOptions options
    )
    {
        var result = await store.GetOrCreateActiveSessionAsync(
            new ProfilingSessionCreateRequest(
                ProfilingSessionIdentity.Create(),
                time.GetUtcNow().ToString(ProfilingOptions.DefaultSessionNameFormat),
                time.GetUtcNow(),
                options.SamplingInterval,
                options.Duration,
                []
            )
        );
        return result.Value.Session;
    }

    private static Result<ProfilingSnapshot> SuccessSnapshot(
        ProfilingCaptureRequest request,
        DateTimeOffset timestampUtc,
        double? cpuUsagePercent = null,
        double? allocationRateBytesPerSecond = null
    ) =>
        Result<ProfilingSnapshot>.Success(
            new ProfilingSnapshot
            {
                Identity = ProfilingSnapshotIdentity.Create(),
                SessionId = request.Session.Identity.Id,
                SessionKey = request.Session.Identity.Key,
                NodeId = request.Node.Identity.Id,
                NodeKey = request.Node.Identity.Key,
                TimestampUtc = timestampUtc,
                HostName = request.Node.HostName,
                ProcessId = request.Node.ProcessId,
                Sequence = request.Sequence,
                ScheduledElapsed = request.ScheduledElapsed,
                CaptureStartedElapsed = request.CaptureStartedElapsed,
                SkippedCaptureCount = request.SkippedCaptureCount,
                FailedCaptureCount = request.FailedCaptureCount,
                CpuUsagePercent = cpuUsagePercent,
                AllocationRateBytesPerSecond = allocationRateBytesPerSecond,
            }
        );

    private static async Task AdvanceAndDrainAsync(FakeTimeProvider time, TimeSpan duration)
    {
        time.Advance(duration);
        await Task.Yield();
        await Task.Yield();
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        for (var attempt = 0; attempt < 1_000; attempt++)
        {
            if (await condition())
            {
                return;
            }

            await Task.Yield();
        }

        throw new TimeoutException("The expected asynchronous test condition was not reached.");
    }

    private sealed class RecordingProbe(
        Func<ProfilingCaptureRequest, CancellationToken, Task<Result<ProfilingSnapshot>>> capture
    ) : IProfilingSnapshotProbe
    {
        private int activeCalls;
        private int callCount;

        public int MaximumConcurrentCalls { get; private set; }

        public async Task<Result<ProfilingSnapshot>> CaptureAsync(
            ProfilingCaptureRequest request,
            CancellationToken cancellationToken = default
        )
        {
            var active = Interlocked.Increment(ref this.activeCalls);
            this.MaximumConcurrentCalls = Math.Max(this.MaximumConcurrentCalls, active);
            Interlocked.Increment(ref this.callCount);
            try
            {
                return await capture(request, cancellationToken);
            }
            finally
            {
                Interlocked.Decrement(ref this.activeCalls);
            }
        }

        public Task WaitForCallsAsync(int expected) =>
            WaitUntilAsync(() => Task.FromResult(Volatile.Read(ref this.callCount) >= expected));
    }

    private sealed class TestBroadcastNodeIdentityProvider : IBroadcastNodeIdentityProvider
    {
        public string GetNodeIdentity() => "profiling-test-node";
    }

    private sealed class CollectorHarness(
        FakeTimeProvider time,
        InMemoryProfilingStore store,
        ProfilingOptions options,
        ProfilingCollector collector,
        ProfilingActiveSessionContext activeSession
    )
    {
        public FakeTimeProvider Time { get; } = time;

        public InMemoryProfilingStore Store { get; } = store;

        public ProfilingOptions Options { get; } = options;

        public ProfilingCollector Collector { get; } = collector;

        public ProfilingActiveSessionContext ActiveSession { get; } = activeSession;

        public static async Task<CollectorHarness> CreateAsync(
            IProfilingSnapshotProbe probe = null,
            ProfilingOptions options = null
        )
        {
            var configuredOptions = options ?? CreateOptions();
            var time = new FakeTimeProvider(StartUtc);
            var store = new InMemoryProfilingStore();
            var registry = new InMemoryBroadcastRegistryStore(new BroadcastingOptions(), time);
            var broadcastIdentity = new TestBroadcastNodeIdentityProvider();
            await registry.UpsertAsync(
                new BroadcastNodeRegistrationRequest(
                    broadcastIdentity.GetNodeIdentity(),
                    null,
                    [BroadcastingOptions.DefaultScope],
                    StartUtc.Subtract(TimeSpan.FromSeconds(1)),
                    StartUtc,
                    null
                )
            );
            var finalizer = new ProfilingSessionFinalizer(store, configuredOptions, time);
            var activeSession = new ProfilingActiveSessionContext();
            var collector = new ProfilingCollector(
                store,
                probe
                    ?? new RecordingProbe(
                        (request, cancellationToken) =>
                            Task.FromResult(SuccessSnapshot(request, time.GetUtcNow()))
                    ),
                new ProfilingRuntimeContextFactory(),
                new ProfilingNodeIdentityProvider(store),
                finalizer,
                configuredOptions,
                time,
                registry,
                broadcastIdentity,
                activeSession
            );
            return new CollectorHarness(time, store, configuredOptions, collector, activeSession);
        }

        public Task<ProfilingSession> CreateSessionAsync(
            TimeSpan? duration = null,
            TimeSpan? samplingInterval = null
        )
        {
            var sessionOptions = CreateOptions(
                duration ?? this.Options.Duration,
                samplingInterval ?? this.Options.SamplingInterval
            );
            return ProfilingCollectorTests.CreateSessionAsync(
                this.Store,
                this.Time,
                sessionOptions
            );
        }
    }
}
