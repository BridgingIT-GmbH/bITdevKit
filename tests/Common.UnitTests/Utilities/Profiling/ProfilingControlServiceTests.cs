// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Profiling;

using Microsoft.Extensions.Time.Testing;

public class ProfilingControlServiceTests
{
    private static readonly DateTimeOffset StartUtc = new(2026, 8, 7, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task StartAsync_MultipleTargetsAndLocalStore_DoesNotMutateOrPublish()
    {
        // Arrange
        var harness = CreateHarness(
            new InMemoryProfilingStore(),
            CreateRegistration("node-a"),
            CreateRegistration("node-b")
        );

        // Act
        var result = await harness.Sut.StartAsync(new("load"));

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(error => error is ProfilingSharedStoreRequiredError);
        harness.Broadcasts.PublishCount.ShouldBe(0);
        (await harness.Store.ListSessionsAsync()).Value.ShouldBeEmpty();
        (await harness.Store.GetSessionDataOrDefaultAsync()).ShouldBeNull();
    }

    [Fact]
    public async Task StartAsync_ConcurrentRequests_CreateOneSessionAndPublishOnce()
    {
        // Arrange
        var harness = CreateHarness(new InMemoryProfilingStore(), CreateRegistration("node-a"));

        // Act
        var results = await Task.WhenAll(
            Enumerable.Range(0, 16).Select(_ => Task.Run(() => harness.Sut.StartAsync(new("load"))))
        );

        // Assert
        results.ShouldAllBe(result => result.IsSuccess);
        results.Select(result => result.Value.Session.Identity.Id).Distinct().Count().ShouldBe(1);
        results.Count(result => result.Value.Created).ShouldBe(1);
        harness.Broadcasts.PublishCount.ShouldBe(1);
        (await harness.Store.ListSessionsAsync()).Value.Count.ShouldBe(1);
    }

    [Fact]
    public async Task StartAsync_MixedOutcomes_RecordsOnlyAcceptedExpectedParticipants()
    {
        // Arrange
        var store = new SharedProfilingStore();
        var harness = CreateHarness(
            store,
            CreateRegistration("node-a"),
            CreateRegistration("node-b"),
            CreateRegistration("node-c")
        );
        harness.Broadcasts.OutcomeSelector = target =>
            target.NodeIdentity switch
            {
                "node-a" => BroadcastDeliveryOutcome.Accepted,
                "node-b" => BroadcastDeliveryOutcome.Rejected,
                _ => BroadcastDeliveryOutcome.Unreachable,
            };

        // Act
        var result = await harness.Sut.StartAsync(new("load"));
        var data = await store.GetSessionDataAsync(result.Value.Session.Identity.Key);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result
            .Value.NodeOutcomes.Select(outcome => outcome.Outcome)
            .ShouldBe([
                BroadcastDeliveryOutcome.Accepted,
                BroadcastDeliveryOutcome.Rejected,
                BroadcastDeliveryOutcome.Unreachable,
            ]);
        data.Value.Participations.ShouldHaveSingleItem();
        data.Value.Participations[0].Role.ShouldBe(ProfilingNodeRole.ExpectedParticipant);
        data.Value.Participations[0].State.ShouldBe(ProfilingParticipationState.Accepted);
    }

    [Fact]
    public async Task StartAsync_LateRegistration_DoesNotJoinFixedParticipantSet()
    {
        // Arrange
        var store = new SharedProfilingStore();
        var harness = CreateHarness(store, CreateRegistration("node-a"));
        var startResult = await harness.Sut.StartAsync(new("load"));

        // Act
        harness.Broadcasts.Targets = [CreateRegistration("node-a"), CreateRegistration("node-b")];
        var data = await store.GetSessionDataAsync(startResult.Value.Session.Identity.Key);

        // Assert
        harness.Broadcasts.PublishedSnapshots.ShouldHaveSingleItem();
        harness.Broadcasts.PublishedSnapshots[0].TargetCount.ShouldBe(1);
        data.Value.Participations.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task SnapshotAsync_ActiveSession_TargetsLateNodeWithoutChangingExpectedSet()
    {
        // Arrange
        var store = new SharedProfilingStore();
        var harness = CreateHarness(store, CreateRegistration("node-a"));
        var startResult = await harness.Sut.StartAsync(new("load"));
        harness.Broadcasts.Targets = [CreateRegistration("node-a"), CreateRegistration("node-b")];

        // Act
        var snapshotResult = await harness.Sut.SnapshotAsync();
        var data = await store.GetSessionDataAsync(startResult.Value.Session.Identity.Key);

        // Assert
        snapshotResult.IsSuccess.ShouldBeTrue();
        harness.Broadcasts.PublishedSnapshots.Count.ShouldBe(2);
        harness.Broadcasts.PublishedSnapshots[1].TargetCount.ShouldBe(2);
        harness
            .Broadcasts.PublishedPayloads[1]
            .ShouldBeOfType<ProfilingSnapshotBroadcast>()
            .Role.ShouldBe(ProfilingNodeRole.AdHocContributor);
        data.Value.Participations.ShouldHaveSingleItem();
        data.Value.Participations[0].Role.ShouldBe(ProfilingNodeRole.ExpectedParticipant);
    }

    [Fact]
    public async Task FinalizeAsync_FailedAdHocContributor_DoesNotCreateCompletionWarning()
    {
        // Arrange
        var store = new InMemoryProfilingStore();
        var time = new FakeTimeProvider(StartUtc);
        var options = CreateOptions();
        var session = (
            await store.GetOrCreateActiveSessionAsync(
                new(
                    ProfilingSessionIdentity.Create(),
                    "load",
                    StartUtc,
                    options.SamplingInterval,
                    TimeSpan.FromSeconds(1),
                    []
                )
            )
        )
            .Value
            .Session;
        var expected = await CreateNodeAsync(store, "node-a");
        var adHoc = await CreateNodeAsync(store, "node-b");
        await store.UpsertParticipationAsync(
            CreateParticipation(
                session,
                expected,
                ProfilingNodeRole.ExpectedParticipant,
                ProfilingParticipationState.Completed
            )
        );
        await store.UpsertParticipationAsync(
            CreateParticipation(
                session,
                adHoc,
                ProfilingNodeRole.AdHocContributor,
                ProfilingParticipationState.Failed
            )
        );
        time.Advance(TimeSpan.FromSeconds(3));
        var sut = new ProfilingSessionFinalizer(store, options, time);

        // Act
        var result = await sut.FinalizeAsync(session);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.State.ShouldBe(ProfilingSessionState.Completed);
    }

    [Fact]
    public async Task SnapshotAsync_WithoutActiveSession_CompletesStandaloneSession()
    {
        // Arrange
        var harness = CreateHarness(new InMemoryProfilingStore(), CreateRegistration("node-a"));

        // Act
        var result = await harness.Sut.SnapshotAsync();
        var data = await harness.Store.GetSessionDataAsync(result.Value.Session.Identity.Key);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Created.ShouldBeTrue();
        result.Value.Session.State.ShouldBe(ProfilingSessionState.Completed);
        result.Value.Session.Name.ShouldBe("Manual snapshot");
        data.Value.Participations.ShouldHaveSingleItem();
        data.Value.Participations[0].Role.ShouldBe(ProfilingNodeRole.ExpectedParticipant);
    }

    [Fact]
    public async Task StopAsync_UnreachableNode_StopsLogicalSessionAndPreservesOriginalEnd()
    {
        // Arrange
        var harness = CreateHarness(new InMemoryProfilingStore(), CreateRegistration("node-a"));
        var startResult = await harness.Sut.StartAsync(
            new("load", Duration: TimeSpan.FromMinutes(1))
        );
        var originalEnd = startResult.Value.Session.EndsUtc;
        harness.Broadcasts.OutcomeSelector = _ => BroadcastDeliveryOutcome.Unreachable;

        // Act
        var result = await harness.Sut.StopAsync();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Session.State.ShouldBe(ProfilingSessionState.Stopped);
        result.Value.Session.EndsUtc.ShouldBe(originalEnd);
        result
            .Value.NodeOutcomes.ShouldHaveSingleItem()
            .Outcome.ShouldBe(BroadcastDeliveryOutcome.Unreachable);
    }

    [Fact]
    public async Task CollectGarbageAsync_WithoutActiveSession_DoesNotCreateSession()
    {
        // Arrange
        var harness = CreateHarness(new InMemoryProfilingStore(), CreateRegistration("node-a"));

        // Act
        var result = await harness.Sut.CollectGarbageAsync();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Session.ShouldBeNull();
        harness
            .Broadcasts.PublishedPayloads.ShouldHaveSingleItem()
            .ShouldBeOfType<ProfilingGarbageCollectionBroadcast>()
            .SessionId.ShouldBe(Guid.Empty);
        (await harness.Store.ListSessionsAsync()).Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task AddPhaseMarkerAsync_ActiveSession_TrimsAndAllowsDuplicateNames()
    {
        // Arrange
        var harness = CreateHarness(new InMemoryProfilingStore(), CreateRegistration("node-a"));
        var startResult = await harness.Sut.StartAsync(new("load"));

        // Act
        var first = await harness.Sut.AddPhaseMarkerAsync("  warm-up  ");
        var second = await harness.Sut.AddPhaseMarkerAsync("warm-up");
        var data = await harness.Store.GetSessionDataAsync(startResult.Value.Session.Identity.Key);

        // Assert
        first.IsSuccess.ShouldBeTrue();
        second.IsSuccess.ShouldBeTrue();
        data.Value.PhaseMarkers.Count.ShouldBe(2);
        data.Value.PhaseMarkers.ShouldAllBe(marker => marker.Name == "warm-up");
        harness.Broadcasts.PublishCount.ShouldBe(1);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AddPhaseMarkerAsync_InvalidName_FailsWithoutStoreMutation(string name)
    {
        // Arrange
        var harness = CreateHarness(new InMemoryProfilingStore(), CreateRegistration("node-a"));
        var startResult = await harness.Sut.StartAsync(new("load"));

        // Act
        var result = await harness.Sut.AddPhaseMarkerAsync(name);
        var data = await harness.Store.GetSessionDataAsync(startResult.Value.Session.Identity.Key);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(error => error is ProfilingValidationError);
        data.Value.PhaseMarkers.ShouldBeEmpty();
    }

    [Fact]
    public async Task RestartAsync_CopiesOnlyApprovedSessionParameters()
    {
        // Arrange
        var harness = CreateHarness(new InMemoryProfilingStore(), CreateRegistration("node-a"));
        var sourceResult = await harness.Sut.StartAsync(
            new(
                "baseline",
                TimeSpan.FromSeconds(5),
                TimeSpan.FromMinutes(2),
                ["memory", "local"]
            )
        );
        await harness.Store.UpdateSessionMetadataAsync(
            sourceResult.Value.Session.Identity.Key,
            new("baseline", ["memory", "local"], "do not copy", true)
        );
        await harness.Sut.StopAsync();

        // Act
        var restartResult = await harness.Sut.RestartAsync(
            sourceResult.Value.Session.Identity.Key
        );
        var source = (
            await harness.Store.FindSessionAsync(sourceResult.Value.Session.Identity.Key)
        ).Value;
        var replacement = restartResult.Value.Session;

        // Assert
        restartResult.IsSuccess.ShouldBeTrue();
        replacement.Identity.Key.ShouldNotBe(source.Identity.Key);
        replacement.Name.ShouldStartWith("baseline — restart ");
        replacement.SamplingInterval.ShouldBe(TimeSpan.FromSeconds(5));
        replacement.Duration.ShouldBe(TimeSpan.FromMinutes(2));
        replacement.Tags.ShouldBe(["memory", "local"]);
        replacement.Note.ShouldBeNull();
        replacement.IsPinned.ShouldBeFalse();
        source.State.ShouldBe(ProfilingSessionState.Stopped);
        source.Note.ShouldBe("do not copy");
        source.IsPinned.ShouldBeTrue();
    }

    [Fact]
    public async Task QueryLifecycle_ActiveDeleteAndClear_AreRejectedWithoutMutation()
    {
        // Arrange
        var harness = CreateHarness(new InMemoryProfilingStore(), CreateRegistration("node-a"));
        var startResult = await harness.Sut.StartAsync(new("active"));
        var sut = new ProfilingQueryService(
            CreateOptions(),
            harness.Store,
            harness.Sut
        );

        // Act
        var deleteResult = await sut.DeleteSessionAsync(startResult.Value.Session.Identity.Key);
        var clearResult = await sut.ClearAsync(true);

        // Assert
        deleteResult.Errors.ShouldContain(x => x is ProfilingInvalidStateError);
        clearResult.Errors.ShouldContain(x => x is ProfilingInvalidStateError);
        (await harness.Store.ListSessionsAsync()).Value.ShouldHaveSingleItem();
        (
            await harness.Store.FindSessionAsync(startResult.Value.Session.Identity.Key)
        ).Value.State.ShouldBe(ProfilingSessionState.Running);
    }

    [Fact]
    public async Task SnapshotHandler_DuplicateBroadcastId_CapturesOnce()
    {
        // Arrange
        var collector = new RecordingCollector();
        var sut = new ProfilingSnapshotBroadcastHandler(
            collector,
            new ProfilingBroadcastExecutionTracker()
        );
        var session = CreateSession();
        var payload = new ProfilingSnapshotBroadcast(
            ProfilingSessionBroadcast.From(session),
            ProfilingNodeRole.AdHocContributor
        );
        var broadcastId = Guid.NewGuid();
        var context = new BroadcastContext(
            broadcastId,
            ["default"],
            StartUtc,
            StartUtc.AddSeconds(1),
            null
        );

        // Act
        await sut.HandleAsync(payload, context, CancellationToken.None);
        await sut.HandleAsync(payload, context, CancellationToken.None);

        // Assert
        collector.CaptureCount.ShouldBe(1);
    }

    private static ControlHarness CreateHarness(
        IProfilingStore store,
        params BroadcastNodeRegistration[] registrations
    )
    {
        var options = CreateOptions();
        var broadcastingOptions = new BroadcastingOptions();
        var time = new FakeTimeProvider(StartUtc);
        var broadcasts = new RecordingProfilingBroadcastService("publisher")
        {
            Targets = registrations,
        };
        return new(
            new(
                options,
                time,
                store,
                broadcasts,
                new ProfilingNodeIdentityProvider(store),
                broadcastingOptions
            ),
            store,
            broadcasts,
            time
        );
    }

    private static ProfilingOptions CreateOptions() =>
        new()
        {
            Enabled = true,
            SamplingInterval = TimeSpan.FromSeconds(1),
            Duration = TimeSpan.FromSeconds(30),
            ParticipationDeadline = TimeSpan.FromSeconds(1),
            FinalizationGracePeriod = TimeSpan.FromSeconds(1),
        };

    private static BroadcastNodeRegistration CreateRegistration(string identity) =>
        new()
        {
            NodeIdentity = identity,
            Scopes = [BroadcastingOptions.DefaultScope],
            ProcessStartedUtc = StartUtc,
            RegisteredUtc = StartUtc,
            IsActive = true,
        };

    private static ProfilingSession CreateSession() =>
        new()
        {
            Identity = ProfilingSessionIdentity.Create(),
            Name = "load",
            State = ProfilingSessionState.Running,
            StartedUtc = StartUtc,
            EndsUtc = StartUtc.AddMinutes(1),
            SamplingInterval = TimeSpan.FromSeconds(1),
            Duration = TimeSpan.FromMinutes(1),
        };

    private static async Task<ProfilingNode> CreateNodeAsync(
        IProfilingStore store,
        string identity
    ) =>
        (
            await store.GetOrCreateNodeAsync(
                new(identity, StartUtc),
                new()
                {
                    Identity = ProfilingNodeIdentity.Create(),
                    Correlation = new(identity, StartUtc),
                    HostName = "test",
                    ProcessId = 1,
                }
            )
        ).Value;

    private static ProfilingNodeParticipation CreateParticipation(
        ProfilingSession session,
        ProfilingNode node,
        ProfilingNodeRole role,
        ProfilingParticipationState state
    ) =>
        new()
        {
            SessionId = session.Identity.Id,
            SessionKey = session.Identity.Key,
            NodeId = node.Identity.Id,
            NodeKey = node.Identity.Key,
            Role = role,
            State = state,
            JoinedUtc = StartUtc,
            CompletedUtc = StartUtc.AddSeconds(1),
        };

    private sealed record ControlHarness(
        ProfilingControlService Sut,
        IProfilingStore Store,
        RecordingProfilingBroadcastService Broadcasts,
        FakeTimeProvider Time
    );

    private sealed class RecordingProfilingBroadcastService(string senderIdentity)
        : IProfilingBroadcastService
    {
        public IReadOnlyList<BroadcastNodeRegistration> Targets { get; set; } = [];

        public Func<
            BroadcastNodeRegistration,
            BroadcastDeliveryOutcome
        > OutcomeSelector
        { get; set; } = _ => BroadcastDeliveryOutcome.Accepted;

        public int PublishCount { get; private set; }

        public List<ProfilingBroadcastTargetSnapshot> PublishedSnapshots { get; } = [];

        public List<object> PublishedPayloads { get; } = [];

        public Task<Result<ProfilingBroadcastTargetSnapshot>> PrepareTargetsAsync(
            IEnumerable<string> targetScopes = null,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(
                Result<ProfilingBroadcastTargetSnapshot>.Success(
                    new(
                        targetScopes ?? [BroadcastingOptions.DefaultScope],
                        this.Targets,
                        senderIdentity
                    )
                )
            );
        }

        public Task<Result<BroadcastResult>> PublishAsync<TBroadcast>(
            TBroadcast payload,
            ProfilingBroadcastTargetSnapshot targetSnapshot,
            BroadcastPublishOptions options = null,
            CancellationToken cancellationToken = default
        )
            where TBroadcast : IProfilingBroadcast
        {
            cancellationToken.ThrowIfCancellationRequested();
            this.PublishCount++;
            this.PublishedSnapshots.Add(targetSnapshot);
            this.PublishedPayloads.Add(payload);
            var now = StartUtc;
            return Task.FromResult(
                Result<BroadcastResult>.Success(
                    new()
                    {
                        BroadcastId = Guid.NewGuid(),
                        TargetScopes = targetSnapshot.TargetScopes,
                        StartedUtc = now,
                        CompletedUtc = now,
                        Nodes = targetSnapshot
                            .Targets.Select(target => new BroadcastNodeDeliveryResult(
                                target.NodeIdentity,
                                this.OutcomeSelector(target)
                            ))
                            .ToArray(),
                    }
                )
            );
        }
    }

    private sealed class RecordingCollector : IProfilingCollector
    {
        public int CaptureCount { get; private set; }

        public Task<Result> StartAsync(
            ProfilingSession session,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(Result.Success());

        public Task<Result> StopAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(Result.Success());

        public Task<Result<ProfilingSnapshot>> CaptureAsync(
            ProfilingSession session,
            ProfilingNodeRole role,
            CancellationToken cancellationToken = default
        )
        {
            this.CaptureCount++;
            return Task.FromResult(Result<ProfilingSnapshot>.Success(new ProfilingSnapshot()));
        }
    }

    private sealed class SharedProfilingStore : IProfilingStore
    {
        private readonly InMemoryProfilingStore inner = new();

        public ProfilingStoreCapabilities Capabilities { get; } = new(true);

        public Task<Result<ProfilingSessionResolution>> GetOrCreateActiveSessionAsync(
            ProfilingSessionCreateRequest request,
            CancellationToken cancellationToken = default
        ) => this.inner.GetOrCreateActiveSessionAsync(request, cancellationToken);

        public Task<Result<ProfilingSession>> GetActiveSessionAsync(
            CancellationToken cancellationToken = default
        ) => this.inner.GetActiveSessionAsync(cancellationToken);

        public Task<Result<ProfilingSession>> FindSessionAsync(
            string sessionKey,
            CancellationToken cancellationToken = default
        ) => this.inner.FindSessionAsync(sessionKey, cancellationToken);

        public Task<Result<IReadOnlyList<ProfilingSession>>> ListSessionsAsync(
            CancellationToken cancellationToken = default
        ) => this.inner.ListSessionsAsync(cancellationToken);

        public Task<Result<ProfilingSession>> UpdateSessionMetadataAsync(
            string sessionKey,
            ProfilingSessionMetadata metadata,
            CancellationToken cancellationToken = default
        ) => this.inner.UpdateSessionMetadataAsync(sessionKey, metadata, cancellationToken);

        public Task<Result<ProfilingSession>> TryTransitionSessionAsync(
            Guid sessionId,
            IReadOnlyCollection<ProfilingSessionState> expectedStates,
            ProfilingSessionState nextState,
            DateTimeOffset transitionedUtc,
            CancellationToken cancellationToken = default
        ) =>
            this.inner.TryTransitionSessionAsync(
                sessionId,
                expectedStates,
                nextState,
                transitionedUtc,
                cancellationToken
            );

        public Task<Result<ProfilingNode>> GetOrCreateNodeAsync(
            ProfilingNodeCorrelation correlation,
            ProfilingNode proposedNode,
            CancellationToken cancellationToken = default
        ) => this.inner.GetOrCreateNodeAsync(correlation, proposedNode, cancellationToken);

        public Task<Result<ProfilingNodeParticipation>> UpsertParticipationAsync(
            ProfilingNodeParticipation participation,
            CancellationToken cancellationToken = default
        ) => this.inner.UpsertParticipationAsync(participation, cancellationToken);

        public Task<Result<ProfilingRuntimeContext>> AddRuntimeContextAsync(
            ProfilingRuntimeContext context,
            CancellationToken cancellationToken = default
        ) => this.inner.AddRuntimeContextAsync(context, cancellationToken);

        public Task<Result<ProfilingSnapshot>> AddSnapshotAsync(
            ProfilingSnapshot snapshot,
            CancellationToken cancellationToken = default
        ) => this.inner.AddSnapshotAsync(snapshot, cancellationToken);

        public Task<Result<ProfilingPhaseMarker>> AddPhaseMarkerAsync(
            ProfilingPhaseMarker marker,
            CancellationToken cancellationToken = default
        ) => this.inner.AddPhaseMarkerAsync(marker, cancellationToken);

        public Task<Result<ProfilingActionMarker>> AddActionMarkerAsync(
            ProfilingActionMarker marker,
            CancellationToken cancellationToken = default
        ) => this.inner.AddActionMarkerAsync(marker, cancellationToken);

        public Task<Result<ProfilingSegment>> UpsertSegmentAsync(
            ProfilingSegment segment,
            CancellationToken cancellationToken = default
        ) => this.inner.UpsertSegmentAsync(segment, cancellationToken);

        public Task<Result<ProfilingMetricObservation>> AddMetricObservationAsync(
            ProfilingMetricObservation observation,
            CancellationToken cancellationToken = default
        ) => this.inner.AddMetricObservationAsync(observation, cancellationToken);

        public Task<Result<ProfilingSessionData>> GetSessionDataAsync(
            string sessionKey,
            CancellationToken cancellationToken = default
        ) => this.inner.GetSessionDataAsync(sessionKey, cancellationToken);

        public Task<Result<bool>> DeleteSessionAsync(
            string sessionKey,
            CancellationToken cancellationToken = default
        ) => this.inner.DeleteSessionAsync(sessionKey, cancellationToken);

        public Task<Result<int>> DeleteUnpinnedSessionsAsync(
            CancellationToken cancellationToken = default
        ) => this.inner.DeleteUnpinnedSessionsAsync(cancellationToken);

        public Task<Result<ProfilingClearResult>> ClearAsync(
            CancellationToken cancellationToken = default
        ) => this.inner.ClearAsync(cancellationToken);

        public Task<Result<int>> ApplyRetentionAsync(
            int maximumRetainedSessions,
            TimeSpan maximumSessionAge,
            DateTimeOffset utcNow,
            CancellationToken cancellationToken = default
        ) =>
            this.inner.ApplyRetentionAsync(
                maximumRetainedSessions,
                maximumSessionAge,
                utcNow,
                cancellationToken
            );
    }
}

file static class ProfilingControlStoreTestExtensions
{
    public static async Task<ProfilingSessionData> GetSessionDataOrDefaultAsync(
        this IProfilingStore store
    )
    {
        var sessions = await store.ListSessionsAsync();
        return sessions.Value.Count == 0
            ? null
            : (await store.GetSessionDataAsync(sessions.Value[0].Identity.Key)).Value;
    }
}
