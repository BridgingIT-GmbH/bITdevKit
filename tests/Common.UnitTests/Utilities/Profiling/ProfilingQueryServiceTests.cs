// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Profiling;

using System.Text.Json;

public class ProfilingQueryServiceTests
{
    private static readonly DateTimeOffset StartedUtc = new(
        2026,
        8,
        7,
        10,
        0,
        0,
        TimeSpan.Zero
    );

    [Fact]
    public async Task GetSessionAsync_ExpectedAndAdHocContributors_ReturnsBoth()
    {
        // Arrange
        var fixture = await CreateFixtureAsync();
        var sut = CreateSut(fixture.Store);

        // Act
        var sessionResult = await sut.GetSessionAsync(fixture.Session.Identity.Key);
        var nodeResult = await sut.GetNodeSessionAsync(
            fixture.Session.Identity.Key,
            fixture.ExpectedNode.Identity.Key
        );

        // Assert
        sessionResult.IsSuccess.ShouldBeTrue();
        sessionResult.Value.Participations.Count.ShouldBe(2);
        sessionResult
            .Value.Participations.ShouldContain(x =>
                x.Role == ProfilingNodeRole.ExpectedParticipant
            );
        sessionResult
            .Value.Participations.ShouldContain(x => x.Role == ProfilingNodeRole.AdHocContributor);
        nodeResult.IsSuccess.ShouldBeTrue();
        nodeResult.Value.Snapshots.Count.ShouldBe(2);
        nodeResult.Value.LatestSnapshot.Identity.Key.ShouldBe("snap0002");
        nodeResult.Value.RuntimeContext.NodeKey.ShouldBe(fixture.ExpectedNode.Identity.Key);
        nodeResult.Value.PhaseMarkers.ShouldHaveSingleItem();
        nodeResult.Value.ActionMarkers.ShouldHaveSingleItem();
        nodeResult.Value.Segments.ShouldHaveSingleItem();
        nodeResult.Value.MetricObservations.ShouldHaveSingleItem();
        nodeResult.Value.SamplingStatus.ShouldBe(
            new ProfilingSamplingStatus(
                2,
                3,
                1,
                TimeSpan.FromMilliseconds(7),
                TimeSpan.FromMilliseconds(25)
            )
        );
    }

    [Fact]
    public async Task GetNodeSessionAsync_InvalidOrUnknownKeys_ReturnsTypedFailure()
    {
        // Arrange
        var fixture = await CreateFixtureAsync();
        var sut = CreateSut(fixture.Store);

        // Act
        var invalidSession = await sut.GetNodeSessionAsync(
            "INVALID!",
            fixture.ExpectedNode.Identity.Key
        );
        var unknownSession = await sut.GetNodeSessionAsync(
            "sess9999",
            fixture.ExpectedNode.Identity.Key
        );
        var unknownNode = await sut.GetNodeSessionAsync(
            fixture.Session.Identity.Key,
            "node9999"
        );

        // Assert
        invalidSession.Errors.ShouldContain(x => x is ProfilingInvalidKeyError);
        unknownSession.Errors.ShouldContain(x => x is NotFoundError);
        unknownNode.Errors.ShouldContain(x => x is NotFoundError);
    }

    [Fact]
    public async Task GetNodeSessionAsync_SerializedReadModel_ContainsNoInternalGuids()
    {
        // Arrange
        var fixture = await CreateFixtureAsync();
        var sut = CreateSut(fixture.Store);

        // Act
        var result = await sut.GetNodeSessionAsync(
            fixture.Session.Identity.Key,
            fixture.ExpectedNode.Identity.Key
        );
        var json = JsonSerializer.Serialize(
            result.Value,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)
        );

        // Assert
        json.ShouldNotContain(fixture.Session.Identity.Id.ToString());
        json.ShouldNotContain(fixture.ExpectedNode.Identity.Id.ToString());
        json.ShouldNotContain("\"sessionId\"");
        json.ShouldNotContain("\"nodeId\"");
        json.ShouldNotContain("\"parentSegmentId\"");
        json.ShouldNotContain("\"segmentId\"");
    }

    [Fact]
    public async Task UpdateMetadataAsync_DoesNotMutateStoredObservations()
    {
        // Arrange
        var fixture = await CreateFixtureAsync();
        var sut = CreateSut(fixture.Store);
        var before = (await sut.GetSessionAsync(fixture.Session.Identity.Key)).Value;

        // Act
        var updateResult = await sut.UpdateMetadataAsync(
            fixture.Session.Identity.Key,
            new("renamed", ["investigate"], "developer note", true)
        );
        var after = (await sut.GetSessionAsync(fixture.Session.Identity.Key)).Value;

        // Assert
        updateResult.IsSuccess.ShouldBeTrue();
        updateResult.Value.Name.ShouldBe("renamed");
        updateResult.Value.Tags.ShouldBe(["investigate"]);
        updateResult.Value.Note.ShouldBe("developer note");
        updateResult.Value.IsPinned.ShouldBeTrue();
        after.Snapshots.Select(x => x.Identity.Key).ShouldBe(
            before.Snapshots.Select(x => x.Identity.Key)
        );
        after
            .MetricObservations.Select(x => new { x.Id, x.Value })
            .ShouldBe(before.MetricObservations.Select(x => new { x.Id, x.Value }));
        after.PhaseMarkers.ShouldBe(before.PhaseMarkers);
        after.Segments.ShouldBe(before.Segments);
    }

    [Fact]
    public async Task ExportSnapshotsJsonAsync_UsesRawSnapshotArrayAndExcludesAuxiliaryData()
    {
        // Arrange
        var fixture = await CreateFixtureAsync();
        var sut = CreateSut(fixture.Store);

        // Act
        var selectedNode = await sut.ExportSnapshotsJsonAsync(
            fixture.Session.Identity.Key,
            fixture.ExpectedNode.Identity.Key
        );
        var completeSession = await sut.ExportSnapshotsJsonAsync(fixture.Session.Identity.Key);

        // Assert
        selectedNode.IsSuccess.ShouldBeTrue();
        completeSession.IsSuccess.ShouldBeTrue();
        using var selectedDocument = JsonDocument.Parse(selectedNode.Value);
        using var completeDocument = JsonDocument.Parse(completeSession.Value);
        selectedDocument.RootElement.ValueKind.ShouldBe(JsonValueKind.Array);
        selectedDocument.RootElement.GetArrayLength().ShouldBe(2);
        completeDocument.RootElement.GetArrayLength().ShouldBe(3);
        var first = selectedDocument.RootElement[0];
        first.GetProperty("identity").GetProperty("key").GetString().ShouldBe("snap0001");
        first.TryGetProperty("sessionId", out _).ShouldBeFalse();
        first.TryGetProperty("nodeId", out _).ShouldBeFalse();
        first.GetProperty("identity").TryGetProperty("id", out _).ShouldBeFalse();
        selectedNode.Value.ShouldNotContain(fixture.Session.Identity.Id.ToString());
        selectedNode.Value.ShouldNotContain(fixture.ExpectedNode.Identity.Id.ToString());
        selectedNode.Value.ShouldNotContain("phaseMarkers");
        selectedNode.Value.ShouldNotContain("segments");
        selectedNode.Value.ShouldNotContain("metricObservations");
        selectedNode.Value.ShouldNotContain("runtimeContexts");
        selectedNode.Value.ShouldNotContain("evaluation");
        selectedNode.Value.ShouldNotContain("developer note");
    }

    [Fact]
    public async Task CompareSnapshotsAsync_ProducesSignedDeltasAndSafePercentages()
    {
        // Arrange
        var fixture = await CreateFixtureAsync();
        var sut = CreateSut(fixture.Store);

        // Act
        var result = await sut.CompareSnapshotsAsync(
            fixture.Session.Identity.Key,
            fixture.ExpectedNode.Identity.Key,
            "snap0001",
            "snap0002"
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var cpu = result.Value.Metrics.Single(x => x.Identifier == "cpu-usage");
        cpu.EarlierValue.ShouldBe(0);
        cpu.LaterValue.ShouldBe(50);
        cpu.Difference.ShouldBe(50);
        cpu.PercentageDifference.ShouldBeNull();
        var workingSet = result.Value.Metrics.Single(x => x.Identifier == "working-set");
        workingSet.Difference.ShouldBe(25);
        workingSet.PercentageDifference.ShouldBe(25);
        var privateMemory = result.Value.Metrics.Single(x =>
            x.Identifier == "private-memory"
        );
        privateMemory.EarlierValue.ShouldBeNull();
        privateMemory.Difference.ShouldBeNull();
        privateMemory.PercentageDifference.ShouldBeNull();
    }

    [Fact]
    public async Task CompareSnapshotsAsync_WrongNodeOrOrder_ReturnsTypedFailure()
    {
        // Arrange
        var fixture = await CreateFixtureAsync();
        var sut = CreateSut(fixture.Store);

        // Act
        var wrongNode = await sut.CompareSnapshotsAsync(
            fixture.Session.Identity.Key,
            fixture.AdHocNode.Identity.Key,
            "snap0001",
            "snap0002"
        );
        var reversed = await sut.CompareSnapshotsAsync(
            fixture.Session.Identity.Key,
            fixture.ExpectedNode.Identity.Key,
            "snap0002",
            "snap0001"
        );

        // Assert
        wrongNode.Errors.ShouldContain(x => x is NotFoundError);
        reversed.Errors.ShouldContain(x => x is ProfilingValidationError);
    }

    [Fact]
    public async Task CompareSnapshotsAsync_NonFiniteMetric_IsSafelyUnavailable()
    {
        // Arrange
        var fixture = await CreateFixtureAsync();
        await fixture.Store.AddSnapshotAsync(
            CreateSnapshot(
                fixture.Session,
                fixture.ExpectedNode,
                "snap0004",
                3,
                cpu: double.NaN,
                workingSet: 150,
                privateMemory: 250
            )
        );
        var sut = CreateSut(fixture.Store);

        // Act
        var result = await sut.CompareSnapshotsAsync(
            fixture.Session.Identity.Key,
            fixture.ExpectedNode.Identity.Key,
            "snap0002",
            "snap0004"
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var cpu = result.Value.Metrics.Single(x => x.Identifier == "cpu-usage");
        cpu.EarlierValue.ShouldBe(50);
        cpu.LaterValue.ShouldBeNull();
        cpu.Difference.ShouldBeNull();
        cpu.PercentageDifference.ShouldBeNull();
    }

    [Fact]
    public async Task LifecycleAndEvaluationMethods_DelegateWithoutDuplicatingBehavior()
    {
        // Arrange
        var control = Substitute.For<IProfilingControlService>();
        var evaluation = Substitute.For<IProfilingEvaluationService>();
        var session = new ProfilingSession
        {
            Identity = new(Guid.NewGuid(), "sess0001"),
            Name = "restart",
        };
        control
            .RestartAsync("sess0001", Arg.Any<CancellationToken>())
            .Returns(Result<ProfilingControlResult>.Success(new(session, true, [])));
        control
            .DeleteSessionAsync("sess0001", Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));
        control
            .DeleteUnpinnedSessionsAsync(Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(2));
        control
            .ClearAsync(true, Arg.Any<CancellationToken>())
            .Returns(Result<ProfilingClearResult>.Success(new(3, 12)));
        var evaluationResult = new ProfilingEvaluationResult(
            new(
                ProfilingEvaluationMode.NodeSession,
                "sess0001",
                "node0001",
                [],
                null,
                null,
                0,
                false
            ),
            new(),
            [],
            [],
            []
        );
        evaluation
            .EvaluateAsync(Arg.Any<ProfilingEvaluationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<ProfilingEvaluationResult>.Success(evaluationResult));
        var sut = new ProfilingQueryService(
            EnabledOptions(),
            new InMemoryProfilingStore(),
            control,
            evaluation
        );

        // Act
        (await sut.RestartAsync("sess0001")).IsSuccess.ShouldBeTrue();
        (await sut.DeleteSessionAsync("sess0001")).Value.ShouldBeTrue();
        (await sut.DeleteUnpinnedSessionsAsync()).Value.ShouldBe(2);
        (await sut.ClearAsync(true)).Value.ShouldBe(new ProfilingClearResult(3, 12));
        (
            await sut.EvaluateAsync(new ProfilingEvaluationRequest("sess0001", "node0001"))
        ).Value.ShouldBe(evaluationResult);

        // Assert
        await control.Received(1).RestartAsync("sess0001", Arg.Any<CancellationToken>());
        await control.Received(1).DeleteSessionAsync("sess0001", Arg.Any<CancellationToken>());
        await control.Received(1).DeleteUnpinnedSessionsAsync(Arg.Any<CancellationToken>());
        await control.Received(1).ClearAsync(true, Arg.Any<CancellationToken>());
        await evaluation
            .Received(1)
            .EvaluateAsync(Arg.Any<ProfilingEvaluationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MissingInfrastructure_ReturnsSafeUnavailableResults()
    {
        // Arrange
        var sut = new ProfilingQueryService(EnabledOptions());

        // Act
        var list = await sut.ListSessionsAsync();
        var restart = await sut.RestartAsync("sess0001");
        var evaluation = await sut.EvaluateAsync(new("sess0001", "node0001"));

        // Assert
        list.Errors.ShouldContain(x => x is ProfilingUnavailableError);
        restart.Errors.ShouldContain(x => x is ProfilingUnavailableError);
        evaluation.Errors.ShouldContain(x => x is ProfilingUnavailableError);
    }

    private static ProfilingQueryService CreateSut(IProfilingStore store) =>
        new(EnabledOptions(), store);

    private static ProfilingOptions EnabledOptions() => new() { Enabled = true };

    private static async Task<QueryFixture> CreateFixtureAsync()
    {
        var store = new InMemoryProfilingStore();
        var session = (
            await store.GetOrCreateActiveSessionAsync(
                new(
                    new(Guid.NewGuid(), "sess0001"),
                    "load",
                    StartedUtc,
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(30),
                    ["local"]
                )
            )
        ).Value.Session;
        var expected = await CreateNodeAsync(
            store,
            "broadcast-expected",
            "node0001",
            1001
        );
        var adHoc = await CreateNodeAsync(store, "broadcast-adhoc", "node0002", 1002);
        await store.UpsertParticipationAsync(
            CreateParticipation(
                session,
                expected,
                ProfilingNodeRole.ExpectedParticipant,
                successful: 2,
                skipped: 3,
                failed: 1
            )
        );
        await store.UpsertParticipationAsync(
            CreateParticipation(
                session,
                adHoc,
                ProfilingNodeRole.AdHocContributor,
                successful: 1
            )
        );
        await store.AddRuntimeContextAsync(CreateContext(session, expected));
        await store.AddRuntimeContextAsync(CreateContext(session, adHoc));
        await store.AddSnapshotAsync(
            CreateSnapshot(
                session,
                expected,
                "snap0001",
                1,
                cpu: 0,
                workingSet: 100,
                privateMemory: null
            )
        );
        await store.AddSnapshotAsync(
            CreateSnapshot(
                session,
                expected,
                "snap0002",
                2,
                cpu: 50,
                workingSet: 125,
                privateMemory: 200
            )
        );
        await store.AddSnapshotAsync(
            CreateSnapshot(
                session,
                adHoc,
                "snap0003",
                1,
                cpu: 10,
                workingSet: 80,
                privateMemory: 90
            )
        );
        await store.AddPhaseMarkerAsync(
            new(
                Guid.NewGuid(),
                session.Identity.Id,
                session.Identity.Key,
                "load started",
                StartedUtc.AddMilliseconds(500)
            )
        );
        await store.AddActionMarkerAsync(
            new(
                Guid.NewGuid(),
                session.Identity.Id,
                expected.Identity.Id,
                session.Identity.Key,
                expected.Identity.Key,
                "gc",
                StartedUtc.AddMilliseconds(750)
            )
        );
        var segment = new ProfilingSegment
        {
            Id = Guid.NewGuid(),
            SessionId = session.Identity.Id,
            SessionKey = session.Identity.Key,
            NodeId = expected.Identity.Id,
            NodeKey = expected.Identity.Key,
            Name = "operation",
            StartedUtc = StartedUtc.AddMilliseconds(500),
            Outcome = ProfilingSegmentOutcome.Open,
        };
        await store.UpsertSegmentAsync(segment);
        await store.AddMetricObservationAsync(
            new()
            {
                Id = Guid.NewGuid(),
                SessionId = session.Identity.Id,
                SessionKey = session.Identity.Key,
                NodeId = expected.Identity.Id,
                NodeKey = expected.Identity.Key,
                SegmentId = segment.Id,
                MetricIdentifier = "tests.counter",
                Kind = ProfilingMetricKind.Counter,
                Value = 1,
                TimestampUtc = StartedUtc.AddSeconds(1),
            }
        );

        return new(store, session, expected, adHoc);
    }

    private static async Task<ProfilingNode> CreateNodeAsync(
        IProfilingStore store,
        string broadcastIdentity,
        string nodeKey,
        int processId
    )
    {
        var correlation = new ProfilingNodeCorrelation(broadcastIdentity, StartedUtc);
        return (
            await store.GetOrCreateNodeAsync(
                correlation,
                new()
                {
                    Identity = new(Guid.NewGuid(), nodeKey),
                    Correlation = correlation,
                    HostName = "test-host",
                    ProcessId = processId,
                }
            )
        ).Value;
    }

    private static ProfilingNodeParticipation CreateParticipation(
        ProfilingSession session,
        ProfilingNode node,
        ProfilingNodeRole role,
        long successful,
        long skipped = 0,
        long failed = 0
    ) =>
        new()
        {
            SessionId = session.Identity.Id,
            SessionKey = session.Identity.Key,
            NodeId = node.Identity.Id,
            NodeKey = node.Identity.Key,
            Role = role,
            State = ProfilingParticipationState.Collecting,
            JoinedUtc = StartedUtc,
            SuccessfulCaptureCount = successful,
            SkippedCaptureCount = skipped,
            FailedCaptureCount = failed,
        };

    private static ProfilingRuntimeContext CreateContext(
        ProfilingSession session,
        ProfilingNode node
    ) =>
        new()
        {
            SessionId = session.Identity.Id,
            SessionKey = session.Identity.Key,
            NodeId = node.Identity.Id,
            NodeKey = node.Identity.Key,
            ApplicationName = "Tests",
            RuntimeDescription = ".NET",
            ProcessStartedUtc = StartedUtc,
        };

    private static ProfilingSnapshot CreateSnapshot(
        ProfilingSession session,
        ProfilingNode node,
        string snapshotKey,
        long sequence,
        double cpu,
        long workingSet,
        long? privateMemory
    )
    {
        var scheduledElapsed = TimeSpan.FromSeconds(sequence);
        return new()
        {
            Identity = new(Guid.NewGuid(), snapshotKey),
            SessionId = session.Identity.Id,
            SessionKey = session.Identity.Key,
            NodeId = node.Identity.Id,
            NodeKey = node.Identity.Key,
            TimestampUtc = StartedUtc.Add(scheduledElapsed),
            HostName = node.HostName,
            ProcessId = node.ProcessId,
            Sequence = sequence,
            ScheduledElapsed = scheduledElapsed,
            CaptureStartedElapsed = scheduledElapsed.Add(TimeSpan.FromMilliseconds(25)),
            CaptureDuration = TimeSpan.FromMilliseconds(sequence == 1 ? 5 : 7),
            SkippedCaptureCount = sequence == 1 ? 1 : 3,
            FailedCaptureCount = sequence == 1 ? 0 : 1,
            CpuUsagePercent = cpu,
            WorkingSetBytes = workingSet,
            PrivateMemoryBytes = privateMemory,
        };
    }

    private sealed record QueryFixture(
        InMemoryProfilingStore Store,
        ProfilingSession Session,
        ProfilingNode ExpectedNode,
        ProfilingNode AdHocNode
    );
}