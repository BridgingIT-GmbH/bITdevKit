// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Profiling;

/// <summary>
/// Defines the observable provider contract shared by in-memory and durable profiling stores.
/// </summary>
/// <remarks>
/// Infrastructure provider tests inherit this fixture through a normal project reference to
/// <c>Common.UnitTests</c>; the fixture is intentionally public and is not source-linked.
/// </remarks>
/// <example><code>public sealed class ProviderTests : ProfilingStoreContractTests { }</code></example>
public abstract class ProfilingStoreContractTests
{
    /// <summary>Creates a fresh isolated provider instance for one contract test.</summary>
    /// <returns>The store under test.</returns>
    protected abstract IProfilingStore CreateStore();

    /// <summary>Gets the multi-node capability expected from the provider.</summary>
    protected abstract bool ExpectedSupportsMultiNode { get; }

    [Fact]
    public void Capabilities_Provider_ReturnsExpectedMultiNodeSupport()
    {
        this.CreateStore().Capabilities.SupportsMultiNode.ShouldBe(this.ExpectedSupportsMultiNode);
    }

    [Fact]
    public async Task GetOrCreateActiveSessionAsync_CompetingRequests_ReturnOneSession()
    {
        // Arrange
        var store = this.CreateStore();
        var startedUtc = new DateTimeOffset(2026, 8, 7, 10, 0, 0, TimeSpan.Zero);
        var requests = Enumerable
            .Range(0, 32)
            .Select(index => CreateSessionRequest(startedUtc.AddMilliseconds(index)))
            .ToArray();

        // Act
        var results = await Task.WhenAll(
            requests.Select(request =>
                Task.Run(async () => await store.GetOrCreateActiveSessionAsync(request))
            )
        );

        // Assert
        results.ShouldAllBe(result => result.IsSuccess);
        results.Select(result => result.Value.Session.Identity.Id).Distinct().Count().ShouldBe(1);
        results.Count(result => result.Value.Created).ShouldBe(1);
        (await store.ListSessionsAsync()).Value.Count.ShouldBe(1);
    }

    [Fact]
    public async Task TryTransitionSessionAsync_RepeatedTerminalTransition_PreservesCompletion()
    {
        // Arrange
        var store = this.CreateStore();
        var startedUtc = new DateTimeOffset(2026, 8, 7, 10, 0, 0, TimeSpan.Zero);
        var completedUtc = startedUtc.AddSeconds(2);
        var session = await CreateSessionAsync(store, startedUtc);
        var first = await store.TryTransitionSessionAsync(
            session.Identity.Id,
            [ProfilingSessionState.Running],
            ProfilingSessionState.Stopped,
            completedUtc
        );

        // Act
        var repeated = await store.TryTransitionSessionAsync(
            session.Identity.Id,
            [ProfilingSessionState.Stopped],
            ProfilingSessionState.Stopped,
            completedUtc.AddMinutes(1)
        );

        // Assert
        first.IsSuccess.ShouldBeTrue();
        repeated.IsSuccess.ShouldBeTrue();
        repeated.Value.CompletedUtc.ShouldBe(completedUtc);
        (await store.FindSessionAsync(session.Identity.Key)).Value.CompletedUtc.ShouldBe(
            completedUtc
        );
    }

    [Fact]
    public async Task Records_AllSupportedKinds_RoundTripWithoutMutation()
    {
        // Arrange
        var store = this.CreateStore();
        var startedUtc = new DateTimeOffset(2026, 8, 7, 10, 0, 0, TimeSpan.Zero);
        var session = await CreateSessionAsync(store, startedUtc);
        var node = await CreateNodeAsync(store, startedUtc);
        var participation = CreateParticipation(session, node, startedUtc);
        var context = CreateContext(session, node, startedUtc);
        var snapshot = CreateSnapshot(session, node, startedUtc.AddSeconds(1));
        var phaseMarker = new ProfilingPhaseMarker(
            Guid.NewGuid(),
            session.Identity.Id,
            session.Identity.Key,
            "load",
            startedUtc.AddSeconds(1)
        );
        var actionMarker = new ProfilingActionMarker(
            Guid.NewGuid(),
            session.Identity.Id,
            node.Identity.Id,
            session.Identity.Key,
            node.Identity.Key,
            "gc",
            startedUtc.AddSeconds(1)
        );
        var segment = CreateSegment(session, node, startedUtc);
        var observation = CreateObservation(session, node, segment.Id, startedUtc.AddSeconds(1));

        // Act
        (await store.UpsertParticipationAsync(participation)).IsSuccess.ShouldBeTrue();
        (await store.AddRuntimeContextAsync(context)).IsSuccess.ShouldBeTrue();
        (await store.AddSnapshotAsync(snapshot)).IsSuccess.ShouldBeTrue();
        (await store.AddPhaseMarkerAsync(phaseMarker)).IsSuccess.ShouldBeTrue();
        (await store.AddActionMarkerAsync(actionMarker)).IsSuccess.ShouldBeTrue();
        (await store.UpsertSegmentAsync(segment)).IsSuccess.ShouldBeTrue();
        (await store.AddMetricObservationAsync(observation)).IsSuccess.ShouldBeTrue();
        var data = (await store.GetSessionDataAsync(session.Identity.Key)).Value;

        // Assert
        data.Session.Identity.ShouldBe(session.Identity);
        data.Participations.ShouldHaveSingleItem().ShouldBe(participation);
        data.RuntimeContexts.ShouldHaveSingleItem().ShouldBe(context);
        data.Snapshots.ShouldHaveSingleItem().ShouldBe(snapshot);
        data.PhaseMarkers.ShouldHaveSingleItem().ShouldBe(phaseMarker);
        data.ActionMarkers.ShouldHaveSingleItem().ShouldBe(actionMarker);
        data.Segments.ShouldHaveSingleItem().ShouldBe(segment);
        data.MetricObservations.ShouldHaveSingleItem().ShouldBe(observation);
    }

    [Fact]
    public async Task ImmutableRecords_DuplicateIdentity_IsIdempotentOnlyForSameValue()
    {
        // Arrange
        var store = this.CreateStore();
        var startedUtc = new DateTimeOffset(2026, 8, 7, 10, 0, 0, TimeSpan.Zero);
        var session = await CreateSessionAsync(store, startedUtc);
        var node = await CreateNodeAsync(store, startedUtc);
        var snapshot = CreateSnapshot(session, node, startedUtc.AddSeconds(1));

        // Act
        var first = await store.AddSnapshotAsync(snapshot);
        var repeated = await store.AddSnapshotAsync(snapshot);
        var changed = await store.AddSnapshotAsync(snapshot with { CpuUsagePercent = 99 });

        // Assert
        first.IsSuccess.ShouldBeTrue();
        repeated.IsSuccess.ShouldBeTrue();
        changed.IsFailure.ShouldBeTrue();
        (await store.GetSessionDataAsync(session.Identity.Key))
            .Value.Snapshots.ShouldHaveSingleItem()
            .ShouldBe(snapshot);
    }

    [Fact]
    public async Task DeletedSession_DelayedRecordsCannotRecreateState()
    {
        // Arrange
        var store = this.CreateStore();
        var startedUtc = new DateTimeOffset(2026, 8, 7, 10, 0, 0, TimeSpan.Zero);
        var request = CreateSessionRequest(startedUtc);
        var session = (await store.GetOrCreateActiveSessionAsync(request)).Value.Session;
        var node = await CreateNodeAsync(store, startedUtc);
        await StopAsync(store, session, startedUtc.AddSeconds(2));
        (await store.DeleteSessionAsync(session.Identity.Key)).IsSuccess.ShouldBeTrue();

        // Act
        var results = new IResult[]
        {
            await store.UpsertParticipationAsync(CreateParticipation(session, node, startedUtc)),
            await store.AddRuntimeContextAsync(CreateContext(session, node, startedUtc)),
            await store.AddSnapshotAsync(CreateSnapshot(session, node, startedUtc.AddSeconds(1))),
            await store.AddPhaseMarkerAsync(
                new(
                    Guid.NewGuid(),
                    session.Identity.Id,
                    session.Identity.Key,
                    "late",
                    startedUtc.AddSeconds(1)
                )
            ),
            await store.AddActionMarkerAsync(
                new(
                    Guid.NewGuid(),
                    session.Identity.Id,
                    node.Identity.Id,
                    session.Identity.Key,
                    node.Identity.Key,
                    "late",
                    startedUtc.AddSeconds(1)
                )
            ),
            await store.UpsertSegmentAsync(CreateSegment(session, node, startedUtc)),
            await store.AddMetricObservationAsync(
                CreateObservation(session, node, null, startedUtc.AddSeconds(1))
            ),
            await store.GetOrCreateActiveSessionAsync(request),
        };

        // Assert
        results.ShouldAllBe(result => result.IsFailure);
        (await store.ListSessionsAsync()).Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task UpsertSegmentAsync_CrossNodeOrCrossSessionParent_RejectsReference()
    {
        // Arrange
        var store = this.CreateStore();
        var startedUtc = new DateTimeOffset(2026, 8, 7, 10, 0, 0, TimeSpan.Zero);
        var firstSession = await CreateSessionAsync(store, startedUtc);
        var firstNode = await CreateNodeAsync(store, startedUtc);
        var secondNode = await CreateNodeAsync(store, startedUtc.AddMinutes(-1));
        var parent = CreateSegment(firstSession, firstNode, startedUtc);
        (await store.UpsertSegmentAsync(parent)).IsSuccess.ShouldBeTrue();
        var crossNode = CreateSegment(firstSession, secondNode, startedUtc) with
        {
            ParentSegmentId = parent.Id,
        };
        await StopAsync(store, firstSession, startedUtc.AddSeconds(1));
        var secondSession = await CreateSessionAsync(store, startedUtc.AddSeconds(2));
        var crossSession = CreateSegment(secondSession, firstNode, startedUtc.AddSeconds(2)) with
        {
            ParentSegmentId = parent.Id,
        };

        // Act
        var crossNodeResult = await store.UpsertSegmentAsync(crossNode);
        var crossSessionResult = await store.UpsertSegmentAsync(crossSession);

        // Assert
        crossNodeResult.IsFailure.ShouldBeTrue();
        crossSessionResult.IsFailure.ShouldBeTrue();
        crossNodeResult.Errors.ShouldContain(error => error is ProfilingValidationError);
        crossSessionResult.Errors.ShouldContain(error => error is ProfilingValidationError);
    }

    [Fact]
    public async Task ClearAsync_TerminalPinnedAndUnpinnedData_RemovesEverythingAtomically()
    {
        // Arrange
        var store = this.CreateStore();
        var startedUtc = new DateTimeOffset(2026, 8, 7, 10, 0, 0, TimeSpan.Zero);
        var session = await CreateSessionAsync(store, startedUtc);
        var node = await CreateNodeAsync(store, startedUtc);
        await store.AddSnapshotAsync(CreateSnapshot(session, node, startedUtc.AddSeconds(1)));
        await store.UpdateSessionMetadataAsync(session.Identity.Key, new("pinned", [], null, true));
        await StopAsync(store, session, startedUtc.AddSeconds(2));

        // Act
        var result = await store.ClearAsync();
        var emptyResult = await store.ClearAsync();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(new ProfilingClearResult(1, 1));
        emptyResult.Value.ShouldBe(new ProfilingClearResult(0, 0));
        (await store.ListSessionsAsync()).Value.ShouldBeEmpty();
        (await store.GetSessionDataAsync(session.Identity.Key)).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task ClearAsync_ActiveSession_RejectsWithoutChangingState()
    {
        // Arrange
        var store = this.CreateStore();
        var session = await CreateSessionAsync(
            store,
            new DateTimeOffset(2026, 8, 7, 10, 0, 0, TimeSpan.Zero)
        );

        // Act
        var result = await store.ClearAsync();

        // Assert
        result.IsFailure.ShouldBeTrue();
        (await store.FindSessionAsync(session.Identity.Key)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ImportSessionAsync_CompleteTerminalGraph_InsertsAtomicallyWithFreshIdentities()
    {
        // Arrange
        var store = this.CreateStore();
        var startedUtc = new DateTimeOffset(2026, 8, 7, 10, 0, 0, TimeSpan.Zero);
        var sessionIdentity = ProfilingSessionIdentity.Create();
        var nodeIdentity = ProfilingNodeIdentity.Create();
        var snapshotIdentity = ProfilingSnapshotIdentity.Create();
        var session = new ProfilingSession
        {
            Identity = sessionIdentity,
            Name = "imported",
            State = ProfilingSessionState.Completed,
            StartedUtc = startedUtc,
            EndsUtc = startedUtc.AddSeconds(30),
            CompletedUtc = startedUtc.AddSeconds(5),
            SamplingInterval = TimeSpan.FromSeconds(1),
            Duration = TimeSpan.FromSeconds(30),
        };
        var correlation = new ProfilingNodeCorrelation(
            $"import-{nodeIdentity.Key}",
            startedUtc.AddMinutes(-1)
        );
        var node = new ProfilingNode
        {
            Identity = nodeIdentity,
            Correlation = correlation,
            HostName = "import-host",
            ProcessId = 1234,
        };
        var data = new ProfilingSessionData
        {
            Session = session,
            Nodes = [node],
            Participations =
            [
                new()
                {
                    SessionId = sessionIdentity.Id,
                    SessionKey = sessionIdentity.Key,
                    NodeId = nodeIdentity.Id,
                    NodeKey = nodeIdentity.Key,
                    Role = ProfilingNodeRole.ExpectedParticipant,
                    State = ProfilingParticipationState.Completed,
                    JoinedUtc = startedUtc,
                    CompletedUtc = startedUtc.AddSeconds(5),
                    SuccessfulCaptureCount = 1,
                },
            ],
            RuntimeContexts =
            [
                new()
                {
                    SessionId = sessionIdentity.Id,
                    SessionKey = sessionIdentity.Key,
                    NodeId = nodeIdentity.Id,
                    NodeKey = nodeIdentity.Key,
                    ApplicationName = "Tests",
                    ProcessStartedUtc = correlation.ProcessStartedUtc,
                },
            ],
            Snapshots =
            [
                new()
                {
                    Identity = snapshotIdentity,
                    SessionId = sessionIdentity.Id,
                    SessionKey = sessionIdentity.Key,
                    NodeId = nodeIdentity.Id,
                    NodeKey = nodeIdentity.Key,
                    TimestampUtc = startedUtc.AddSeconds(1),
                    HostName = node.HostName,
                    ProcessId = node.ProcessId,
                    Sequence = 1,
                    ScheduledElapsed = TimeSpan.FromSeconds(1),
                    CaptureStartedElapsed = TimeSpan.FromSeconds(1),
                    CaptureDuration = TimeSpan.FromMilliseconds(2),
                },
            ],
        };

        // Act
        var result = await store.ImportSessionAsync(data);
        var duplicate = await store.ImportSessionAsync(data);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        duplicate.IsFailure.ShouldBeTrue();
        var stored = (await store.GetSessionDataAsync(sessionIdentity.Key)).Value;
        stored.Session.ShouldBe(session);
        stored.Nodes.Single().Identity.ShouldBe(nodeIdentity);
        stored.Snapshots.Single().Identity.ShouldBe(snapshotIdentity);
        stored.RuntimeContexts.Count.ShouldBe(1);
        stored.Participations.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ApplyRetentionAsync_OldUnpinnedSessions_RemovesOldestAndPreservesPinned()
    {
        // Arrange
        var store = this.CreateStore();
        var baseline = new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero);
        var oldest = await CreateTerminalSessionAsync(store, baseline);
        var pinned = await CreateTerminalSessionAsync(store, baseline.AddDays(1));
        await store.UpdateSessionMetadataAsync(pinned.Identity.Key, new("keep", [], null, true));
        var newest = await CreateTerminalSessionAsync(store, baseline.AddDays(2));

        // Act
        var result = await store.ApplyRetentionAsync(
            maximumRetainedSessions: 1,
            maximumSessionAge: TimeSpan.FromDays(30),
            utcNow: baseline.AddDays(3)
        );

        // Assert
        result.Value.ShouldBe(1);
        (await store.FindSessionAsync(oldest.Identity.Key)).IsFailure.ShouldBeTrue();
        (await store.FindSessionAsync(pinned.Identity.Key)).IsSuccess.ShouldBeTrue();
        (await store.FindSessionAsync(newest.Identity.Key)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ApplyRetentionAsync_SessionOlderThanMaximumAge_RemovesIt()
    {
        // Arrange
        var store = this.CreateStore();
        var startedUtc = new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero);
        var expired = await CreateTerminalSessionAsync(store, startedUtc);

        // Act
        var result = await store.ApplyRetentionAsync(
            maximumRetainedSessions: 20,
            maximumSessionAge: TimeSpan.FromDays(7),
            utcNow: startedUtc.AddDays(8)
        );

        // Assert
        result.Value.ShouldBe(1);
        (await store.FindSessionAsync(expired.Identity.Key)).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task ConcurrentLifecycleStress_StartStopClearFinalizeRetentionAndSnapshots_PreservesStoreInvariants()
    {
        // Arrange
        var store = this.CreateStore();
        var baseline = new DateTimeOffset(2026, 8, 8, 10, 0, 0, TimeSpan.Zero);

        for (var round = 0; round < 5; round++)
        {
            var startedUtc = baseline.AddMinutes(round);
            var session = await CreateSessionAsync(store, startedUtc);
            var node = await CreateNodeAsync(store, startedUtc, $"stress-node-{round}");
            var startGate = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously
            );
            var operations = new List<Task>();

            for (var sequence = 1; sequence <= 8; sequence++)
            {
                var snapshot = CreateSnapshot(
                    session,
                    node,
                    startedUtc.AddMilliseconds(sequence * 10),
                    sequence
                );
                operations.Add(
                    Task.Run(async () =>
                    {
                        await startGate.Task;
                        await store.AddSnapshotAsync(snapshot);
                    })
                );
            }

            operations.Add(
                Task.Run(async () =>
                {
                    await startGate.Task;
                    await store.GetOrCreateActiveSessionAsync(
                        CreateSessionRequest(startedUtc.AddMilliseconds(1))
                    );
                })
            );
            operations.Add(
                Task.Run(async () =>
                {
                    await startGate.Task;
                    await store.TryTransitionSessionAsync(
                        session.Identity.Id,
                        [ProfilingSessionState.Running],
                        ProfilingSessionState.Stopped,
                        startedUtc.AddSeconds(1)
                    );
                })
            );
            operations.Add(
                Task.Run(async () =>
                {
                    await startGate.Task;
                    await store.TryTransitionSessionAsync(
                        session.Identity.Id,
                        [ProfilingSessionState.Running],
                        ProfilingSessionState.CompletedWithWarnings,
                        startedUtc.AddSeconds(1)
                    );
                })
            );
            operations.Add(
                Task.Run(async () =>
                {
                    await startGate.Task;
                    await store.ClearAsync();
                })
            );
            operations.Add(
                Task.Run(async () =>
                {
                    await startGate.Task;
                    await store.ApplyRetentionAsync(
                        1,
                        TimeSpan.FromDays(1),
                        startedUtc.AddHours(1)
                    );
                })
            );

            // Act
            startGate.SetResult();
            await Task.WhenAll(operations);

            // Assert
            var sessions = (await store.ListSessionsAsync()).Value;
            sessions.Select(item => item.Identity.Id).Distinct().Count().ShouldBe(sessions.Count);
            sessions.Select(item => item.Identity.Key).Distinct().Count().ShouldBe(sessions.Count);
            sessions
                .Count(item => item.State == ProfilingSessionState.Running)
                .ShouldBeLessThanOrEqualTo(1);

            foreach (var storedSession in sessions)
            {
                var data = (await store.GetSessionDataAsync(storedSession.Identity.Key)).Value;
                data.Snapshots.ShouldAllBe(snapshot =>
                    snapshot.SessionId == storedSession.Identity.Id
                    && snapshot.SessionKey == storedSession.Identity.Key
                );
            }

            var active = (await store.GetActiveSessionAsync()).Value;
            if (active is not null)
            {
                await StopAsync(store, active, active.StartedUtc.AddSeconds(2));
            }

            (
                await store.ApplyRetentionAsync(1, TimeSpan.FromDays(1), startedUtc.AddHours(1))
            ).IsSuccess.ShouldBeTrue();
            (await store.ListSessionsAsync()).Value.Count.ShouldBeLessThanOrEqualTo(1);
            (await store.ClearAsync()).IsSuccess.ShouldBeTrue();
            (await store.ListSessionsAsync()).Value.ShouldBeEmpty();

            var delayed = await store.AddSnapshotAsync(
                CreateSnapshot(session, node, startedUtc.AddMilliseconds(90), sequence: 9)
            );
            delayed.IsFailure.ShouldBeTrue();
        }
    }

    [Fact]
    public async Task PublicKeyLookup_InternalIdentifier_RemainsProviderDetail()
    {
        // Arrange
        var store = this.CreateStore();
        var session = await CreateSessionAsync(
            store,
            new DateTimeOffset(2026, 8, 7, 10, 0, 0, TimeSpan.Zero)
        );

        // Act
        var result = await store.FindSessionAsync(session.Identity.Key);
        var invalid = await store.FindSessionAsync(session.Identity.Id.ToString("N"));

        // Assert
        result.Value.Identity.Key.ShouldBe(session.Identity.Key);
        invalid.IsFailure.ShouldBeTrue();
        invalid.Errors.ShouldContain(error => error is ProfilingInvalidKeyError);
    }

    protected static ProfilingSessionCreateRequest CreateSessionRequest(
        DateTimeOffset startedUtc
    ) =>
        new(
            ProfilingSessionIdentity.Create(),
            startedUtc.ToString(ProfilingOptions.DefaultSessionNameFormat),
            startedUtc,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(30),
            []
        );

    protected static async Task<ProfilingSession> CreateSessionAsync(
        IProfilingStore store,
        DateTimeOffset startedUtc
    ) =>
        (await store.GetOrCreateActiveSessionAsync(CreateSessionRequest(startedUtc))).Value.Session;

    protected static async Task<ProfilingSession> CreateTerminalSessionAsync(
        IProfilingStore store,
        DateTimeOffset startedUtc
    )
    {
        var session = await CreateSessionAsync(store, startedUtc);
        await StopAsync(store, session, startedUtc.AddSeconds(5));
        return session;
    }

    protected static async Task<ProfilingNode> CreateNodeAsync(
        IProfilingStore store,
        DateTimeOffset processStartedUtc,
        string broadcastIdentity = "test-node"
    )
    {
        var correlation = new ProfilingNodeCorrelation(broadcastIdentity, processStartedUtc);
        var proposed = new ProfilingNode
        {
            Identity = ProfilingNodeIdentity.Create(),
            Correlation = correlation,
            HostName = "test-host",
            ProcessId = 1234,
        };
        return (await store.GetOrCreateNodeAsync(correlation, proposed)).Value;
    }

    protected static ProfilingNodeParticipation CreateParticipation(
        ProfilingSession session,
        ProfilingNode node,
        DateTimeOffset joinedUtc
    ) =>
        new()
        {
            SessionId = session.Identity.Id,
            SessionKey = session.Identity.Key,
            NodeId = node.Identity.Id,
            NodeKey = node.Identity.Key,
            Role = ProfilingNodeRole.ExpectedParticipant,
            State = ProfilingParticipationState.Collecting,
            JoinedUtc = joinedUtc,
        };

    protected static ProfilingRuntimeContext CreateContext(
        ProfilingSession session,
        ProfilingNode node,
        DateTimeOffset processStartedUtc
    ) =>
        new()
        {
            SessionId = session.Identity.Id,
            SessionKey = session.Identity.Key,
            NodeId = node.Identity.Id,
            NodeKey = node.Identity.Key,
            ApplicationName = "Tests",
            RuntimeDescription = ".NET",
            ProcessStartedUtc = processStartedUtc,
        };

    protected static ProfilingSnapshot CreateSnapshot(
        ProfilingSession session,
        ProfilingNode node,
        DateTimeOffset timestampUtc,
        long sequence = 1
    ) =>
        new()
        {
            Identity = ProfilingSnapshotIdentity.Create(),
            SessionId = session.Identity.Id,
            SessionKey = session.Identity.Key,
            NodeId = node.Identity.Id,
            NodeKey = node.Identity.Key,
            TimestampUtc = timestampUtc,
            HostName = node.HostName,
            ProcessId = node.ProcessId,
            Sequence = sequence,
            ScheduledElapsed = timestampUtc - session.StartedUtc,
            CaptureStartedElapsed = timestampUtc - session.StartedUtc,
            CaptureDuration = TimeSpan.FromMilliseconds(5),
            CpuUsagePercent = 25,
        };

    protected static ProfilingSegment CreateSegment(
        ProfilingSession session,
        ProfilingNode node,
        DateTimeOffset startedUtc
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            SessionId = session.Identity.Id,
            SessionKey = session.Identity.Key,
            NodeId = node.Identity.Id,
            NodeKey = node.Identity.Key,
            Name = "operation",
            StartedUtc = startedUtc,
            Outcome = ProfilingSegmentOutcome.Open,
        };

    protected static ProfilingMetricObservation CreateObservation(
        ProfilingSession session,
        ProfilingNode node,
        Guid? segmentId,
        DateTimeOffset timestampUtc
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            SessionId = session.Identity.Id,
            SessionKey = session.Identity.Key,
            NodeId = node.Identity.Id,
            NodeKey = node.Identity.Key,
            SegmentId = segmentId,
            MetricIdentifier = "tests.counter",
            Kind = ProfilingMetricKind.Counter,
            Value = 1,
            TimestampUtc = timestampUtc,
        };

    protected static async Task StopAsync(
        IProfilingStore store,
        ProfilingSession session,
        DateTimeOffset stoppedUtc
    )
    {
        var result = await store.TryTransitionSessionAsync(
            session.Identity.Id,
            [ProfilingSessionState.Running],
            ProfilingSessionState.Stopped,
            stoppedUtc
        );
        result.IsSuccess.ShouldBeTrue();
    }
}

public sealed class InMemoryProfilingStoreContractTests : ProfilingStoreContractTests
{
    protected override IProfilingStore CreateStore() => new InMemoryProfilingStore();

    protected override bool ExpectedSupportsMultiNode => false;
}
